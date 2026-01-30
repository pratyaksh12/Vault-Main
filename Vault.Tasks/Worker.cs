using Vault.Index.IServices;
using Vault.Models;
using UglyToad.PdfPig;
using Microsoft.EntityFrameworkCore.Internal;
using Vault.Interfaces;
using System.Threading.Tasks;
using System.IO.Compression;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using Elastic.Clients.Elasticsearch;
using System.Security.Cryptography;
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using Tesseract;
using System.Text.Json;

namespace Vault.Tasks;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IElasticSearchService _elasticService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TesseractEngine _ocrEngine;
    private const string _uploadPath = "/tmp/vault_ingest";
    private readonly string _storagePath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) + "/Vault_files";
    private readonly string _tessDataPath = Path.Combine(AppContext.BaseDirectory, "tessdata");


    public Worker(ILogger<Worker> logger, IElasticSearchService elasticService, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _elasticService = elasticService;
        _scopeFactory = scopeFactory;
        
        // Initialize OCR Engine once (Singleton pattern for performance)
        _ocrEngine = new TesseractEngine(_tessDataPath, "eng", EngineMode.Default);
    }
    
    public override void Dispose()
    {
        _ocrEngine?.Dispose();
        base.Dispose();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Directory.CreateDirectory(_uploadPath);
        Directory.CreateDirectory(_storagePath);
        Directory.CreateDirectory(_tessDataPath);
        _logger.LogInformation("Vault Ingestion working watching "+_uploadPath);
        
        // Ensure Index exists with correct mappings
        await _elasticService.CreateIndexAsync();

        if(!File.Exists(Path.Combine(_tessDataPath, "eng.traineddata")))
        {
            _logger.LogWarning("Tesseract 'eng.traineddata' is missing please install it and store it under tessdata folder under Vaut.Tasks");
        }

        using var watcher = new FileSystemWatcher(_uploadPath);
        watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite;
        watcher.Filter = "*.*";
        watcher.Created += async(sender, e) => await ProcessFileAsync(e.FullPath, stoppingToken);
        watcher.Renamed += async(sender, e) => await ProcessFileAsync(e.FullPath, stoppingToken);
        watcher.EnableRaisingEvents = true;

        try
        {            
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (TaskCanceledException)
        {
            watcher.EnableRaisingEvents = false;
            _logger.LogInformation("Logger as stopped working");
        }

    }


    private List<Document> CreateDocumentsFromFile(string filePath, string checksum)
    {
        var docs = new List<Document>();
        var ext = Path.GetExtension(filePath).ToLower();

        if(ext == ".pdf")
        {
            try
            {
                using var pdf = PdfDocument.Open(filePath);
                foreach (var page in pdf.GetPages())
                {
                    string text = page.Text;

                    if(string.IsNullOrWhiteSpace(text) || text.Length < 50)
                    {
                        _logger.LogInformation("Attempting OCR due to low density of characters on Page: "+page.Number);

                        var sb = new StringBuilder();
                        foreach (var image in page.GetImages())
                        {
                            if(image.TryGetPng(out byte[]? pngBytes))
                            {
                                sb.AppendLine(PerformOcr(pngBytes));
                            }
                        }

                        string ocrText = sb.ToString();
                        if (!string.IsNullOrWhiteSpace(ocrText))
                        {
                            text = ocrText;
                            _logger.LogInformation("OCR Succeeded. Text Length: {Length}", text.Length);
                        }
                        else 
                        {
                            _logger.LogWarning("OCR returned empty text for page {Page}", page.Number);
                        }
                    }
                    else
                    {
                        _logger.LogInformation("Using PDF Text (Length: {Length}) for page {Page}", text.Length, page.Number);
                    }

                    var entities = EntityExtractor.Extract(text);
                    var metadatajson = JsonSerializer.Serialize(entities);

                    docs.Add(new Document
                    {
                        Id = Guid.NewGuid().ToString(),
                        Path = filePath,
                        Content = text,
                        ProjectId = "default",
                        Status = 1,
                        ContentType = ext,
                        ContentLength = text.Length,
                        ExtractionDate = DateTime.UtcNow,
                        Metadata = metadatajson,
                        ParentId = Guid.Empty.ToString(),
                        PageNumber = page.Number ,
                        Checksum = checksum
                    });
                }
            }
            catch (Exception ex)
            {
                 _logger.LogError("Failed to parse PDF {Path}: {Message}", filePath, ex.Message);
            }
        }else if(ext == ".jpg" || ext == ".png" || ext == ".jpeg")
        {
            _logger.LogInformation("Processing image for OCR: {Path}", filePath);
            string text = PerformOcr(File.ReadAllBytes(filePath));

            var entities = EntityExtractor.Extract(text);
            var metadatajson = JsonSerializer.Serialize(entities);

            docs.Add(new Document
            {
                Id = Guid.NewGuid().ToString(),
                Path = filePath,
                Content = text,
                ProjectId = "default",
                Status = 1,
                ContentType = ext,
                ContentLength = text.Length,
                ExtractionDate = DateTime.UtcNow,
                Metadata = metadatajson,
                ParentId = Guid.Empty.ToString(),
                PageNumber = docs.Count,
                Checksum = checksum
            });
        }else if (ext == ".txt")
        {
            string text = File.ReadAllText(filePath);

            var entities = EntityExtractor.Extract(text);
            var metadatajson = JsonSerializer.Serialize(entities);

            docs.Add(new Document
            {
                Id = Guid.NewGuid().ToString(),
                Path = filePath,
                Content = text,
                ProjectId = "default",
                Status = 1,
                ContentType = ext,
                ContentLength = text.Length,
                ExtractionDate = DateTime.UtcNow,
                Metadata = metadatajson,
                ParentId = Guid.Empty.ToString(),
                PageNumber = docs.Count,
                Checksum = checksum
            });
        }
        return docs;
    }

    private string PerformOcr(byte[] imageBytes)
{
    try
    {
        using var image = Image.Load(imageBytes);

        image.Mutate(x => x
            .Grayscale()
            .Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(2500, 0)
            })
            .Contrast(1.2f)
        );

        image.Metadata.HorizontalResolution = 300;
        image.Metadata.VerticalResolution = 300;

        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        ms.Position = 0;

        using var pix = Pix.LoadFromMemory(ms.ToArray());
        using var page = _ocrEngine.Process(pix);

        var text = page.GetText()?.Trim();

        _logger.LogInformation(
            "OCR completed. Confidence: {Confidence}, Length: {Length}",
            page.GetMeanConfidence(),
            text?.Length ?? 0);

        return text ?? string.Empty;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "OCR failed.");
        return string.Empty;
    }
}

    private async Task ProcessFileAsync(string filePath, CancellationToken stoppingToken)
    {

        try
        {
            var fileName = Path.GetFileName(filePath);
            if(fileName.StartsWith(".") || fileName.StartsWith("__MACOSX")) return;

            await WaitForFileAccess(filePath, TimeSpan.FromMinutes(1), stoppingToken);
            
            if(!File.Exists(filePath)) return; 
            
            var fileInfo =  new FileInfo(filePath);
            
            _logger.LogInformation("starting to process file: " + fileInfo.Name);

            string checksum = await CalculateHashAsync(filePath, stoppingToken);

            if (string.Equals(fileInfo.Extension, ".zip", StringComparison.OrdinalIgnoreCase))
            {
                 // Move ZIP to storage first
                string targetPath = Path.Combine(_storagePath, fileInfo.Name);
                if (File.Exists(targetPath))
                {
                    if(await CalculateHashAsync(targetPath, stoppingToken) == checksum)
                    {
                        _logger.LogWarning("Duplicate file found. Skipping: "+ fileInfo.Name);
                        File.Delete(filePath);
                        return;
                    }
                    targetPath = Path.Combine(_storagePath, $"{Guid.NewGuid()}_{fileInfo.Name}");
                }

                File.Move(filePath, targetPath);
                _logger.LogInformation("Moved the ZIP file to storage: " + targetPath);

                await ProcessZipAsync(targetPath, stoppingToken);
                return;
            }

            string finalPath = Path.Combine(_storagePath, fileInfo.Name);
            if (File.Exists(finalPath))
            {
                if(await CalculateHashAsync(finalPath, stoppingToken) == checksum)
                {
                    _logger.LogWarning("Duplicate file found. Skipping: "+ fileInfo.Name);
                    File.Delete(filePath);
                    return;
                }

                finalPath = Path.Combine(_storagePath, $"{Guid.NewGuid()}_{fileInfo.Name}");
            }

            File.Move(filePath, finalPath);
            _logger.LogInformation("Moved the file to storage: " + finalPath);

            var docs = CreateDocumentsFromFile(finalPath, checksum);
            if(docs.Count == 0)
            {
                _logger.LogInformation("File was empty skipping: "+ fileInfo.Name);
                return;
            }

            
            using (var scope = _scopeFactory.CreateScope())
            {
                var repository = scope.ServiceProvider.GetRequiredService<IVaultRepository<Document>>();
                await repository.AddRangeAsync(docs);
                await repository.SaveChangesAsync();
                _logger.LogInformation("Saved metaData to DB");
                //TODO: Add a checker for checksum.
            }

            await _elasticService.BulkIndexAsync(docs);
            _logger.LogInformation("Indexed " + docs.Count + " pages to ElasticSearch.");

        }catch(Exception ex)
        {
            _logger.LogError(ex, "Filed Processing the file from: " + filePath);
        }
    }

    private async Task ProcessZipAsync(string zipFilePath, CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Processing processed zip file at location: " + zipFilePath);

            string extractionFolderName = $"{Path.GetFileNameWithoutExtension(zipFilePath)}_{Guid.NewGuid()}";
            string extractionPath = Path.Combine(_storagePath, "extracted", extractionFolderName);
            Directory.CreateDirectory(extractionPath);

            try
            {
                ZipFile.ExtractToDirectory(zipFilePath, extractionPath);

                var files = Directory.GetFiles(extractionPath, "*.*", SearchOption.AllDirectories)
                    .Where(f => !Path.GetFileName(f).StartsWith("._") && !Path.GetFileName(f).Contains("__MACOSX"))
                    .ToList();

                _logger.LogInformation("Extraction of files completed. Total files found: " + files.Count);

                var documents = new List<Document>();
                foreach (var item in files)
                {
                    
                    var ext = Path.GetExtension(item).ToLower();
                    if(ext == ".pdf" || ext == ".jpg" || ext == ".png" || ext == ".jpeg" || ext == ".txt")
                    {
                         string checksum = await CalculateHashAsync(item, stoppingToken);
                         var docs = CreateDocumentsFromFile(item, checksum);
                         if(docs is not null && docs.Any()) documents.AddRange(docs);
                    }
                }

                if (documents.Count != 0)
                {
                    using(var scope = _scopeFactory.CreateScope())
                    {
                        var repository = scope.ServiceProvider.GetRequiredService<IVaultRepository<Document>>();
                        await repository.AddRangeAsync(documents);
                        await repository.SaveChangesAsync();
                        _logger.LogInformation("Bulk information saved: " + documents.Count);
                    }

                    await _elasticService.BulkIndexAsync(documents);
                    _logger.LogInformation("Indexing completed for ZIP contents.");
                }

            }
            catch(Exception ex)
            {
                 _logger.LogError(ex, "Failed during extraction/processing of zip: " + zipFilePath);
                 
            }
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "Failed to process zip file: " + zipFilePath);
        }
    }

    private async Task<string> CalculateHashAsync(string filePath, CancellationToken stoppingToken)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = await sha256.ComputeHashAsync(stream, stoppingToken);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
     private string CalculateHash(string filePath)
    {
        return CalculateHashAsync(filePath, CancellationToken.None).GetAwaiter().GetResult();
    }


    private async Task WaitForFileAccess(string filePath, TimeSpan timeout, CancellationToken stoppingToken)
    {
        var startTime = DateTime.UtcNow;
        
        while (DateTime.UtcNow - startTime < timeout)
        {
            stoppingToken.ThrowIfCancellationRequested();
            try
            {
                using var fs = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
                return; // Success, file is accessible
            }
            catch (IOException)
            {
                // File is locked, wait and retry
                await Task.Delay(500, stoppingToken);
            }
        }
        throw new TimeoutException($"Timed out waiting for exclusive access to file: {filePath}");
    }
}