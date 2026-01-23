using System.Collections.Generic;
using System.Threading.Tasks;
using Vault.Core.Models;
using Vault.Models;

namespace Vault.Index.IServices;

public interface IElasticSearchService
{
    Task CreateIndexAsync();
    Task IndexDocumentAsync(Document document);
    Task BulkIndexAsync(IEnumerable<Document> documents);
    Task<IEnumerable<SearchResult>> SearchDocumentAsync(string query);
    Task<PageResult<SearchResult>> SearchDocumentAsync(string query, int page = 1, int pageSize = 10);
    
}
