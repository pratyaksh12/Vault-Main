using System;
using System.Security.Cryptography.X509Certificates;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using Vault.Index.IServices;
using Vault.Models;

namespace Vault.Index.Services;

public class ElasticSearchService : IElasticSearchService
{
    private readonly ElasticsearchClient _client;
    private const string IndexName = "vault-documents";
    public ElasticSearchService(string cloudId, string apiKey)
    {
        var clientSettings = new ElasticsearchClientSettings(cloudId, new Base64ApiKey(apiKey))
             .DefaultIndex(IndexName);
        _client = new ElasticsearchClient(clientSettings);
    }

    public ElasticSearchService(Uri uri)
    {
        var clientSettings = new ElasticsearchClientSettings(uri)
            .DefaultIndex(IndexName)
            .DisableDirectStreaming();
        _client = new ElasticsearchClient(clientSettings);
    }
    public async Task BulkIndexAsync(IEnumerable<Document> documents)
    {
        var response = await _client.BulkAsync(b => b
            .Index(IndexName)
            .IndexMany(documents)
        );

        if (response.Errors)
        {
            foreach (var item in response.ItemsWithErrors)
            {
                Console.WriteLine(item.Error);
            }
        }
    }

    public async Task CreateIndexAsync()
    {
        var exists = await _client.Indices.ExistsAsync(IndexName);

        if (!exists.Exists)
        {
            await _client.Indices.CreateAsync<Document>(IndexName, c => c.Mappings(
                m => m.Properties(
                    p => p.Keyword(d => d.Id)
                    .Text(d => d.Content)
                    .Keyword(d => d.Path)
                    .Keyword(d => d.ProjectId)
                )
            ));
        }
    }

    public async Task IndexDocumentAsync(Document document)
    {
        var response = await _client.IndexAsync(document);
        if (!response.IsValidResponse)
        {
            throw new Exception("Failed to index the document: {" + response.DebugInformation +"}");
        }
    }
}
