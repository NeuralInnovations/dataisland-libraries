namespace Dataisland.Elasticsearch;

public interface IElasticClient
{
    // Connectivity
    Task<bool> PingAsync(CancellationToken ct = default);

    // Lifecycle
    Task EnsureIlmPolicyAsync(CancellationToken ct = default);

    // Index management
    Task<bool> CreateIndexAsync(string indexName, Action<IndexMappingBuilder> configureMappings, CancellationToken ct = default);
    Task<bool> DeleteIndexAsync(string indexName, CancellationToken ct = default);
    Task<bool> IndexExistsAsync(string indexName, CancellationToken ct = default);
    Task EnsureVectorIndexAsync(string indexName, CancellationToken ct = default);
    Task ReindexAsync(string sourceIndex, string targetIndex, CancellationToken ct = default);

    // Document operations
    Task IndexDocumentAsync<T>(string indexName, string docId, T document, CancellationToken ct = default);
    Task BulkIndexAsync<T>(string indexName, IEnumerable<(string Id, T Document)> documents, CancellationToken ct = default);
    Task<int> CopyByFileIdAsync(string sourceIndex, string targetIndex, string fileId, CancellationToken ct = default);
    Task DeleteDocumentAsync(string indexName, string docId, CancellationToken ct = default);
    Task DeleteByFileIdAsync(string indexName, string fileId, CancellationToken ct = default);

    // Search
    Task<IReadOnlyList<SearchHit<T>>> KnnSearchAsync<T>(
        string[] indices, float[] queryVector, int k, string? fileIdFilter = null, CancellationToken ct = default);

    Task<IReadOnlyList<SearchHit<T>>> MultiSearchAsync<T>(
        string[] indices, float[][] queryVectors, int k, string? fileIdFilter = null, CancellationToken ct = default);

    Task<IReadOnlyList<SearchHit<T>>> SearchByMetadataAsync<T>(
        string[] indices, string[] queries, CancellationToken ct = default);

    Task<IReadOnlyList<SearchHit<T>>> SearchByTermAsync<T>(
        string[] indices, string field, string value, int size = 100, CancellationToken ct = default);

    Task<IReadOnlyList<SearchHit<T>>> SearchByTextAsync<T>(
        string[] indices, string query, int size = 10000, CancellationToken ct = default);

    Task<IReadOnlyList<SearchHit<T>>> FindEmptyMetadataAsync<T>(
        string[] indices, int size = 10000, CancellationToken ct = default);
}

public record SearchHit<T>(string Id, float Score, T Source);
