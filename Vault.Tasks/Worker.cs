using Vault.Index.IServices;
using Vault.Models;
using UglyToad.PdfPig;
using Microsoft.EntityFrameworkCore.Internal;
using Vault.Interfaces;
using System.Threading.Tasks;
using System.IO.Compression;

namespace Vault.Tasks;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IElasticSearchService _elasticService;
    private readonly FileSystemWatcher _watcher;
    private readonly IServiceScopeFactory _scopeFactory;
    private const string WatchPath = "/tmp/vault_ingest";

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
                    var doc = CreateDocumentFromFile(item);
                    if(doc is not null) documents.Add(doc);
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

    private Document? CreateDocumentFromFile(string filePath)
    {
        var content = GetFileContent(filePath);
        if(string.IsNullOrWhiteSpace(content)) return null;

        var fileInfo = new FileInfo(filePath);

        return new Document
        {
            Id = Guid.NewGuid().ToString(),
            Path = filePath,
            Content = content,
            ProjectId = "defaut",
            Status = 1, 
            ContentType = fileInfo.Extension,
            ContentLength = fileInfo.Length,
            ExtractionDate = DateTime.UtcNow,
            Metadata = "{}", 
            ParentId = Guid.Empty.ToString() 
        };
    }

    private void OnError(object sender, ErrorEventArgs e)
    {
        _logger.LogError(e.GetException(), "File Watcher Error");
    }
    private async Task ProcessFileAsync(string filePath)
    {
        
        try{
            await WaitForFileAccess(filePath);

            // read content
            var doc = CreateDocumentFromFile(filePath);

            if(doc is null) return;

            var fileInfo = new FileInfo(filePath);

            //Send it to elasticsearch
            using (var scope = _scopeFactory.CreateScope()){
                var repository = scope.ServiceProvider.GetRequiredService<IVaultRepository<Document>>();

                await repository.AddAsync(doc);
                await repository.SaveChangesAsync();
                _logger.LogInformation("Saved to DB: {Id}: "+ doc.Id);
            }

            await _elasticService.IndexDocumentAsync(doc);
            _logger.LogInformation("Indexing of the file completed; {Id}: " + doc.Id);

        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "Failed to process file aT: " + filePath);
        }
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