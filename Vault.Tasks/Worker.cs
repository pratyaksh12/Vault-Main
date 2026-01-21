using Vault.Index.IServices;
using Vault.Models;

namespace Vault.Tasks;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IElasticSearchService _elasticService;
    private readonly FileSystemWatcher _watcher;
    private const string WatchPath = "/tmp/vault_ingest";

    public Worker(ILogger<Worker> logger, IElasticSearchService elasticService)
    {
        _logger = logger;
        _elasticService = elasticService;
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
        _ = ProcessFileAsync(e.FullPath);
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
            var content = await File.ReadAllTextAsync(filePath);
            var FileInfo = new FileInfo(filePath);

            var doc = new Document
            {
                Id = Guid.NewGuid().ToString(),
                Path = filePath,
                Content = content,
                ProjectId = "defaut",
                Status = 1 // Parsed
            };

            //Send it to elasticsearch
            await _elasticService.IndexDocumentAsync(doc);

            _logger.LogInformation("Sucessfully Indexed File "+ filePath);
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "Failed to process file aT: " + filePath);
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