using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Search;
using Elastic.Transport;
using Vault.Core.Models;
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
                    .Text(d => d.Path) // Changed to Text for partial matching
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
            throw new Exception("Failed to index the document: {" + response.DebugInformation + "}");
        }
    }

    public async Task<IEnumerable<SearchResult>> SearchDocumentAsync(string query)
    {
        var response = await _client.SearchAsync<Document>(s => s
            .Index(IndexName)
            .Query(q => q
                .MultiMatch(m => m
                    .Fields(Infer.Fields<Document>(p => p.Content, p => p.Path))
                    .Query(query)
                    .Fuzziness(new Fuzziness("AUTO"))
                )
            )
            .Highlight(h => h
                .Fields(f => f
                    .Add(Infer.Field<Document>(p => p.Content), new HighlightField
                    {
                        PreTags = new[] { "<em>" },
                        PostTags = new[] { "</em>" },
                        FragmentSize = 300
                    })
                )
            )
        );

        if (!response.IsValidResponse)
        {
            return new List<SearchResult>();
        }

        return response.Hits.Select(hit => new SearchResult
        {
            Id = hit.Source?.Id ?? "",
            Path = hit.Source?.Path ?? "",
            PageNumber = hit.Source?.PageNumber ?? 1,
            Snippet = hit.Highlight != null && hit.Highlight.ContainsKey("content") 
                ? string.Join(" ... ", hit.Highlight["content"]) 
                : (hit.Source?.Content.Length > 300 ? hit.Source.Content.Substring(0, 300) + "..." : hit.Source?.Content ?? "")
        });
    }

    public async Task<PageResult<SearchResult>> SearchDocumentAsync(string query, int page = 1, int pageSize = 10)
    {
        var from = (page - 1) * pageSize;

        var response = await _client.SearchAsync<Document>(s => s
        .Index(IndexName)
        .From(from)
        .Size(pageSize)
        .Query(q =>q
            .MultiMatch(m => m
                .Fields(Infer.Fields<Document>(p => p.Content, page => page.Path))
                .Query(query)
                .Fuzziness(new Fuzziness("AUTO")
                )
            )
        )
        .Highlight(h => h
            .Fields(f => f
                .Add(Infer.Field<Document>(p => p.Content), new HighlightField
                {
                    PreTags = new[] { "<em>"},
                    PostTags = new[] {"</em>"},
                    FragmentSize = 300
                })
            )
        ));

        if (!response.IsValidResponse)
        {
            return new PageResult<SearchResult>{Items = new List<SearchResult>(), TotalCount = 0};
        }

        var items = response.Hits.Select(hit => new SearchResult
        {
            Id = hit.Source?.Id ?? "",
            Path = hit.Source?.Path ?? "",
            PageNumber = hit.Source?.PageNumber ?? 1,
            Snippet = hit.Highlight != null && hit.Highlight.ContainsKey("content") ? string.Join("...", hit.Highlight["content"]) : (hit.Source?.Content.Length > 300 ? hit.Source?.Content.Substring(0, 150) + "..." : hit.Source?.Content?? "")
        }).ToList();

        return new PageResult<SearchResult>{
            Items = items,
            TotalCount = response.Total,
            Page = page,
            PageSize = pageSize
        };

    }
}
