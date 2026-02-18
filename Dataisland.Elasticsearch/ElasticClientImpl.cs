using System.Net.Http.Headers;
using System.Text;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Bulk;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Microsoft.Extensions.Logging;

namespace Dataisland.Elasticsearch;

public class ElasticClientImpl : IElasticClient
{
    private readonly ElasticsearchClient _client;
    private readonly ElasticsearchOptions _options;
    private readonly ILogger<ElasticClientImpl> _logger;

    public ElasticClientImpl(ElasticsearchOptions options, ILogger<ElasticClientImpl> logger)
    {
        _options = options;
        _logger = logger;

        var settings = new ElasticsearchClientSettings(new Uri(options.Url));

        if (!string.IsNullOrWhiteSpace(options.Username) && !string.IsNullOrWhiteSpace(options.Password))
        {
            settings = settings.Authentication(
                new Elastic.Transport.BasicAuthentication(options.Username, options.Password));
        }

        _client = new ElasticsearchClient(settings);
    }

    private const string IlmPolicyName = "dataisland-default";

    public async Task EnsureIlmPolicyAsync(CancellationToken ct = default)
    {
        try
        {
            using var httpClient = new HttpClient();
            var url = $"{_options.Url.TrimEnd('/')}/_ilm/policy/{IlmPolicyName}";

            var request = new HttpRequestMessage(HttpMethod.Put, url);
            if (!string.IsNullOrWhiteSpace(_options.Username) && !string.IsNullOrWhiteSpace(_options.Password))
            {
                var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.Username}:{_options.Password}"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            }

            const string policyJson = """
                {
                    "policy": {
                        "phases": {
                            "warm": {
                                "min_age": "30d",
                                "actions": {
                                    "forcemerge": { "max_num_segments": 1 }
                                }
                            },
                            "delete": {
                                "min_age": "180d",
                                "actions": {
                                    "delete": {}
                                }
                            }
                        }
                    }
                }
                """;

            request.Content = new StringContent(policyJson, Encoding.UTF8, "application/json");
            var response = await httpClient.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
                _logger.LogWarning("Failed to create ILM policy: {Status}", response.StatusCode);
            else
                _logger.LogInformation("ILM policy '{PolicyName}' ensured", IlmPolicyName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not ensure ILM policy — ILM may not be available");
        }
    }

    public async Task<bool> CreateIndexAsync(string indexName, int dimensions = 1024, CancellationToken ct = default)
    {
        var exists = await _client.Indices.ExistsAsync(indexName, ct);
        if (exists.Exists) return false;

        var response = await _client.Indices.CreateAsync(indexName, c => c
            .Settings(s => s
                .Lifecycle(lc => lc.Name(IlmPolicyName)))
            .Mappings(m => m
                .Properties(new Properties
                {
                    { "text", new TextProperty() },
                    { "doc_id", new KeywordProperty() },
                    { "file_id", new KeywordProperty() },
                    { "file_name", new KeywordProperty() },
                    { "page", new IntegerNumberProperty() },
                    { "metadata", new TextProperty() },
                    { "summary", new TextProperty() },
                    { "embedding", new DenseVectorProperty
                        {
                            Dims = dimensions,
                            Index = true,
                            Similarity = DenseVectorSimilarity.Cosine
                        }
                    }
                })
            ), ct);

        if (!response.IsValidResponse)
            _logger.LogError("Failed to create index {IndexName}: {Error}", indexName, response.DebugInformation);

        return response.IsValidResponse;
    }

    public async Task<bool> DeleteIndexAsync(string indexName, CancellationToken ct = default)
    {
        var response = await _client.Indices.DeleteAsync(indexName, ct);
        return response.IsValidResponse;
    }

    public async Task<bool> IndexExistsAsync(string indexName, CancellationToken ct = default)
    {
        var response = await _client.Indices.ExistsAsync(indexName, ct);
        return response.Exists;
    }

    public async Task ReindexAsync(string sourceIndex, string targetIndex, CancellationToken ct = default)
    {
        await _client.ReindexAsync(r => r
            .Source(s => s.Indices(Indices.Parse(sourceIndex)))
            .Dest(d => d.Index(targetIndex)), ct);
    }

    public async Task IndexDocumentAsync<T>(string indexName, string docId, T document, CancellationToken ct = default)
    {
        await _client.IndexAsync(document, i => i.Index(indexName).Id(docId), ct);
    }

    public async Task BulkIndexAsync<T>(string indexName, IEnumerable<(string Id, T Document)> documents, CancellationToken ct = default)
    {
        var request = new BulkRequest(indexName)
        {
            Operations = new BulkOperationsCollection(
                documents.Select(d =>
                    (IBulkOperation)new BulkIndexOperation<T>(d.Document) { Id = d.Id }
                )
            )
        };

        var response = await _client.BulkAsync(request, ct);

        if (response.Errors)
            _logger.LogError("Bulk index errors in {IndexName}: {Errors}",
                indexName, string.Join("; ", response.ItemsWithErrors.Select(i => i.Error?.Reason)));
    }

    public async Task DeleteDocumentAsync(string indexName, string docId, CancellationToken ct = default)
    {
        await _client.DeleteAsync(new DeleteRequest(indexName, docId), ct);
    }

    public async Task DeleteByFileIdAsync(string indexName, string fileId, CancellationToken ct = default)
    {
        await _client.DeleteByQueryAsync(indexName, d => d
            .Query(q => q.Term(new TermQuery(new Field("file_id")) { Value = fileId })), ct);
    }

    public async Task<IReadOnlyList<SearchHit<T>>> KnnSearchAsync<T>(
        string[] indices, float[] queryVector, int k, CancellationToken ct = default)
    {
        var response = await _client.SearchAsync<T>(s => s
            .Index(string.Join(",", indices))
            .Knn(knn => knn
                .Field(new Field("embedding"))
                .QueryVector(queryVector)
                .k(k)
                .NumCandidates(k * 2)
            ), ct);

        if (!response.IsValidResponse) return [];

        return response.Hits
            .Select(h => new SearchHit<T>(h.Id!, (float)(h.Score ?? 0), h.Source!))
            .ToList();
    }

    public async Task<IReadOnlyList<SearchHit<T>>> MultiSearchAsync<T>(
        string[] indices, float[][] queryVectors, int k, CancellationToken ct = default)
    {
        var allResults = new List<SearchHit<T>>();

        foreach (var vector in queryVectors)
        {
            var hits = await KnnSearchAsync<T>(indices, vector, k, ct);
            allResults.AddRange(hits);
        }

        return allResults
            .GroupBy(h => h.Id)
            .Select(g => g.OrderByDescending(h => h.Score).First())
            .OrderByDescending(h => h.Score)
            .Take(k)
            .ToList();
    }

    public async Task<IReadOnlyList<SearchHit<T>>> SearchByMetadataAsync<T>(
        string[] indices, string[] queries, CancellationToken ct = default)
    {
        var response = await _client.SearchAsync<T>(s => s
            .Index(string.Join(",", indices))
            .Query(q => q
                .Bool(b => b
                    .Should(queries.Select(query =>
                        (Action<QueryDescriptor<T>>)(sq =>
                            sq.MultiMatch(mm => mm
                                .Query(query)
                                .Fields(new[] { "text", "metadata", "summary" })
                            )
                        )
                    ).ToArray())
                )
            ), ct);

        if (!response.IsValidResponse) return [];

        return response.Hits
            .Select(h => new SearchHit<T>(h.Id!, (float)(h.Score ?? 0), h.Source!))
            .ToList();
    }

    public async Task<IReadOnlyList<SearchHit<T>>> SearchByTermAsync<T>(
        string[] indices, string field, string value, int size = 100, CancellationToken ct = default)
    {
        var response = await _client.SearchAsync<T>(s => s
            .Index(string.Join(",", indices))
            .Size(size)
            .Query(q => q.Term(new TermQuery(new Field(field)) { Value = value })), ct);

        if (!response.IsValidResponse) return [];

        return response.Hits
            .Select(h => new SearchHit<T>(h.Id!, (float)(h.Score ?? 0), h.Source!))
            .ToList();
    }

    public async Task<IReadOnlyList<SearchHit<T>>> SearchByTextAsync<T>(
        string[] indices, string query, int size = 10000, CancellationToken ct = default)
    {
        var response = await _client.SearchAsync<T>(s => s
            .Index(string.Join(",", indices))
            .Size(size)
            .Query(q => q
                .Bool(b => b
                    .Should(
                        sq => sq.MatchPhrase(mp => mp.Field(new Field("file_name")).Query(query)),
                        sq => sq.MatchPhrase(mp => mp.Field(new Field("text")).Query(query))
                    )
                    .MinimumShouldMatch(1)
                )
            ), ct);

        if (!response.IsValidResponse) return [];

        return response.Hits
            .Select(h => new SearchHit<T>(h.Id!, (float)(h.Score ?? 0), h.Source!))
            .ToList();
    }

    public async Task<IReadOnlyList<SearchHit<T>>> FindEmptyMetadataAsync<T>(
        string[] indices, int size = 10000, CancellationToken ct = default)
    {
        var response = await _client.SearchAsync<T>(s => s
            .Index(string.Join(",", indices))
            .Size(size)
            .Query(q => q
                .Bool(b => b
                    .Should(
                        sq => sq.Bool(bb => bb.MustNot(mn => mn.Exists(e => e.Field(new Field("metadata"))))),
                        sq => sq.Bool(bb => bb.MustNot(mn => mn.Exists(e => e.Field(new Field("summary")))))
                    )
                    .MinimumShouldMatch(1)
                )
            ), ct);

        if (!response.IsValidResponse) return [];

        return response.Hits
            .Select(h => new SearchHit<T>(h.Id!, (float)(h.Score ?? 0), h.Source!))
            .ToList();
    }
}
