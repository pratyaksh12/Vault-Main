using Vault.Index.IServices;
using Vault.Models;
using UglyToad.PdfPig;
using Microsoft.EntityFrameworkCore.Internal;
using Vault.Interfaces;
using System.Threading.Tasks;
using System.IO.Compression;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using Elastic.Clients.Elasticsearch;

namespace Vault.Tasks;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IElasticSearchService _elasticService;
    private readonly FileSystemWatcher _watcher;
    private readonly IServiceScopeFactory _scopeFactory;
    private const string WatchPath = "/tmp/vault_ingest";
    private readonly string PermenantValuePath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) + "/Vault_files";


    public Worker(ILogger<Worker> logger, IElasticSearchService elasticService, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _elasticService = elasticService;
        _scopeFactory = scopeFactory;
        Directory.CreateDirectory(WatchPath);
        _watcher = new FileSystemWatcher(WatchPath);
        _watcher.Created += OnCreated;
        _watcher.Error += OnError;
        _watcher.EnableRaisingEvents = true;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Vault Ingestion working watching "+WatchPath);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        };

    }

    private void OnCreated(object sender, FileSystemEventArgs e)
    {
        _logger.LogInformation("New File Detected at "+ e.FullPath);

        string extension = Path.GetExtension(e.FullPath).ToLower();

        if(extension == ".zip")
        {
            _ = ProcessZipAsync(e.FullPath);
        }
        else
        {
           _ = ProcessFileAsync(e.FullPath); 
        }
        
    }

    private async Task ProcessZipAsync(string fullPath)
    {
        try
        {
            await WaitForFileAccess(fullPath);
            _logger.LogInformation("Processing zip file at location: " + fullPath);

            string extractionPath = Path.Combine(Path.GetTempPath(), "vault_extract_" + Guid.NewGuid());
            Directory.CreateDirectory(extractionPath);

            try
            {
                ZipFile.ExtractToDirectory(fullPath, extractionPath);

                var files = Directory.GetFiles(extractionPath, "*.*", SearchOption.AllDirectories).Where(f => Path.GetExtension(f).Equals(".pdf", StringComparison.CurrentCultureIgnoreCase) && !Path.GetFileName(f).StartsWith("_.") && !Path.GetFileName(f).Contains("__MACOSX")).ToList();

                _logger.LogInformation("Extraction of files completed. Total files found: " + files.Count);

                var documents = new List<Document>();
                foreach (var item in files)
                {
                    var docs = CreateDocumentsFromFile(item);
                    if(docs is not null && docs.Any()) documents.AddRange(docs);
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
                    _logger.LogInformation("Indexing completed.");
                }

            }
            finally
            {
                if(Directory.Exists(extractionPath)) Directory.Delete(extractionPath, true);
            }
        }catch(Exception ex)
        {
            _logger.LogError(ex, "Failed to process zip file: " + fullPath);
        }
    }

    private List<Document> CreateDocumentsFromFile(string filePath)
    {
        var list = new List<Document>();

        try
        {
            var fileInfo = new FileInfo(filePath);
            string extension = Path.GetExtension(filePath).ToLower();

            if(extension == ".pdf")
            {
                using var pdf = PdfDocument.Open(filePath);
                foreach (var item in pdf.GetPages())
                {
                    var text = ContentOrderTextExtractor.GetText(item);

                    if(string.IsNullOrWhiteSpace(text)) continue;

                    list.Add(new Document
                    {
                        Id = Guid.NewGuid().ToString(),
                        Path = filePath,
                        Content = text,
                        ProjectId = "default",
                        Status = 1,
                        ContentType = extension,
                        ContentLength = text.Length,
                        ExtractionDate = DateTime.UtcNow,
                        Metadata = "{}",
                        ParentId = Guid.Empty.ToString(),
                        PageNumber = item.Number 
                    });
                }
            }
            else
            {
                //handling Text/Other
                var content = File.ReadAllText(filePath);
                if (!string.IsNullOrWhiteSpace(filePath))
                {
                    list.Add(new Document
                    {
                        Id = Guid.NewGuid().ToString(),
                        Path = filePath,
                        Content = content,
                        ProjectId = "default",
                        Status = 1,
                        ContentType = extension,
                        ContentLength = content.Length,
                        ExtractionDate = DateTime.UtcNow,
                        Metadata = "{}",
                        ParentId = Guid.Empty.ToString(),
                        PageNumber = 1
                    });
                }
            }
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "Error Processing File" + filePath);
        }

        return list;
    }

    private void OnError(object sender, ErrorEventArgs e)
    {
        _logger.LogError(e.GetException(), "File Watcher Error");
    }
    private async Task ProcessFileAsync(string tempFilePath)
    {

        try
        {
            await WaitForFileAccess(tempFilePath);
            
            if(!File.Exists(tempFilePath)) return; 

            string checksum = CalculateHash(tempFilePath);
            
            using (var scope = _scopeFactory.CreateScope())
            {
                var repo = scope.ServiceProvider.GetRequiredService<IVaultRepository<Document>>();
                //TODO: Add a checker for checksum.
            }

            string fileName = Path.GetFileName(tempFilePath);
            Directory.CreateDirectory(PermenantValuePath);
            string finalPath = Path.Combine(PermenantValuePath, fileName);

            if (File.Exists(finalPath))
            {
                string existingHash = CalculateHash(finalPath);
                if(existingHash == checksum)
                {
                    File.Delete(tempFilePath);
                }
            }
            else
            {
                string uniqueName = $"{Path.GetFileNameWithoutExtension(fileName)}_{Guid.NewGuid().ToString().Substring(0,8)}{Path.GetExtension(fileName)}";
                finalPath = Path.Combine(PermenantValuePath, uniqueName);
                File.Move(tempFilePath, finalPath);
            }

            //process from final path

            var docs = CreateDocumentsFromFile(finalPath);
            if(docs == null || docs.Count == 0)
            {
                return;
            }

            foreach(var item in docs)
            {
                item.Checksum = checksum;
            }

            using(var scope = _scopeFactory.CreateScope())
            {
                var repository = scope.ServiceProvider.GetRequiredService<IVaultRepository<Document>>();

                await repository.AddRangeAsync(docs);
                await repository.SaveChangesAsync();
                _logger.LogInformation("Saved document to DB from Path: " + finalPath);
            }

            await _elasticService.BulkIndexAsync(docs);
            _logger.LogInformation("Indexed " + docs.Count + " pages to ElasticSearch.");

        }catch(Exception ex)
        {
            _logger.LogError(ex, "Filed Processing the file from: " + tempFilePath);
        }
    }

    private string CalculateHash(string filePath)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = sha256.ComputeHash(stream);
        return Convert.ToHexStringLower(hash);
    }

    private string GetFileContent(string filePath)
    {
        try
        {
            string extension = Path.GetExtension(filePath).ToLower();
            if(extension == ".pdf")
            {
                using var pdf = PdfDocument.Open(filePath);
                return string.Join(" ", pdf.GetPages().Select(p => UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor.ContentOrderTextExtractor.GetText(p)));
            }
            else
            {
                return File.ReadAllText(filePath);
            }
        }catch(Exception ex)
        {
            _logger.LogError(ex, "Error Reading the file");
            return string.Empty;
        }
    }

    private async Task WaitForFileAccess(string filePath)
    {
        for(int i = 0; i < 10; i++)
        {
            try
            {
                using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
                return;
            }
            catch (IOException)
            {
                await Task.Delay(500);
            }
        }
    }
}