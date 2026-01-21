using Vault.Models;

namespace Vault.Index.IServices;

public interface IElasticSearchService
{
    Task CreateIndexAsync();
    Task IndexDocumentAsync(Document document);
    Task BulkIndexAsync(IEnumerable<Document> documents);
}
