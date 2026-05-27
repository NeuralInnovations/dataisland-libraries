using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Bulk;
using Elastic.Clients.Elasticsearch.Core.Search;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Microsoft.Extensions.Logging;

namespace Dataisland.Elasticsearch;

public class ElasticClientImpl : IElasticClient
{
    private readonly ElasticsearchClient _client;
    private readonly ILogger<ElasticClientImpl> _logger;

    public ElasticClientImpl(ElasticsearchOptions options, ILogger<ElasticClientImpl> logger)
    {
        _logger = logger;

        var uri = new Uri(options.Url);
        var settings = new ElasticsearchClientSettings(uri)
            .RequestTimeout(TimeSpan.FromSeconds(options.RequestTimeoutSeconds))
            .PingTimeout(TimeSpan.FromSeconds(options.PingTimeoutSeconds));

        if (!string.IsNullOrWhiteSpace(options.Username) && !string.IsNullOrWhiteSpace(options.Password))
        {
            settings = settings.Authentication(
                new Elastic.Transport.BasicAuthentication(options.Username, options.Password));
        }

        if (uri.Scheme == Uri.UriSchemeHttps)
        {
            if (!string.IsNullOrWhiteSpace(options.CertificateFingerprint))
            {
                settings = settings.CertificateFingerprint(options.CertificateFingerprint);
            }
            else
            {
                logger.LogWarning(
                    "Elasticsearch URL uses HTTPS but no CertificateFingerprint is configured — " +
                    "certificate validation is disabled. Set Elasticsearch:CertificateFingerprint in production");
                settings = settings.ServerCertificateValidationCallback((_, _, _, _) => true);
            }
        }

        _client = new ElasticsearchClient(settings);
    }

    public async Task<bool> PingAsync(CancellationToken ct = default)
    {
        var response = await _client.PingAsync(ct);
        return response.IsValidResponse;
    }

    private const string IlmPolicyName = "dataisland-default";

    public async Task EnsureIlmPolicyAsync(CancellationToken ct = default)
    {
        try
        {
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

            var path = new Elastic.Transport.EndpointPath(
                Elastic.Transport.HttpMethod.PUT, $"/_ilm/policy/{IlmPolicyName}");
            var response = await _client.Transport.RequestAsync<Elastic.Transport.StringResponse>(
                in path,
                Elastic.Transport.PostData.String(policyJson),
                null, null, ct);

            if (!response.ApiCallDetails.HasSuccessfulStatusCode)
                _logger.LogWarning("Failed to create ILM policy: {Status}", response.ApiCallDetails.HttpStatusCode);
            else
                _logger.LogInformation("ILM policy '{PolicyName}' ensured", IlmPolicyName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not ensure ILM policy — ILM may not be available");
        }
    }

    public async Task<bool> CreateIndexAsync(string indexName, Action<IndexMappingBuilder> configureMappings, CancellationToken ct = default)
    {
        var exists = await _client.Indices.ExistsAsync(indexName, ct);
        if (exists.Exists) return false;

        var builder = new IndexMappingBuilder();
        configureMappings(builder);

        var response = await _client.Indices.CreateAsync(indexName, c => c
            .Settings(s => s
                .Lifecycle(lc => lc.Name(IlmPolicyName)))
            .Mappings(m => m
                .Properties(builder.Properties)
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

    public async Task EnsureVectorIndexAsync(string indexName, CancellationToken ct = default)
    {
        await CreateIndexAsync(indexName, VectorIndexMapping.Configure, ct);
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

    public async Task<int> CopyByFileIdAsync(string sourceIndex, string targetIndex, string fileId, CancellationToken ct = default)
    {
        if (!await IndexExistsAsync(sourceIndex, ct))
            return 0;

        await EnsureVectorIndexAsync(targetIndex, ct);

        var hits = await SearchByTermAsync<VectorChunkDocument>(
            [sourceIndex], "file_id", fileId, size: 10000, ct);
        if (hits.Count == 0)
            return 0;

        var request = new BulkRequest(targetIndex)
        {
            Operations = new BulkOperationsCollection(
                hits.Select(h =>
                    (IBulkOperation)new BulkIndexOperation<VectorChunkDocument>(h.Source) { Id = h.Id }
                )
            )
        };

        var response = await _client.BulkAsync(request, ct);
        if (!response.IsValidResponse || response.Errors)
        {
            var errors = string.Join("; ", response.ItemsWithErrors.Select(i => i.Error?.Reason));
            throw new InvalidOperationException(
                $"Failed to copy Elasticsearch chunks for file {fileId} from {sourceIndex} to {targetIndex}: {errors}");
        }

        return hits.Count;
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
        string[] indices, float[] queryVector, int k, string? fileIdFilter = null, CancellationToken ct = default)
    {
        var response = await _client.SearchAsync<T>(s =>
            s.Index(string.Join(",", indices))
                .Knn(knn =>
                {
                    // When filtering by file_id, use more candidates to ensure good recall
                    // within a single file (unfiltered: k*2 is standard; filtered: k*5 compensates for filter)
                    var numCandidates = string.IsNullOrEmpty(fileIdFilter) ? k * 2 : Math.Max(k * 5, 100);
                    knn.Field(new Field("embedding"))
                        .QueryVector(queryVector)
                        .k(k)
                        .NumCandidates(numCandidates);

                    // Server-side file_id filter — applied within KNN search, not client-side
                    if (!string.IsNullOrEmpty(fileIdFilter))
                        knn.Filter(f => f.Term(new TermQuery(new Field("file_id")) { Value = fileIdFilter }));
                }), ct);

        if (!response.IsValidResponse) return [];

        return response.Hits
            .Select(h => new SearchHit<T>(h.Id!, (float)(h.Score ?? 0), h.Source!))
            .ToList();
    }

    public async Task<IReadOnlyList<SearchHit<T>>> MultiSearchAsync<T>(
        string[] indices, float[][] queryVectors, int k, string? fileIdFilter = null, CancellationToken ct = default)
    {
        var allResults = new List<SearchHit<T>>();

        foreach (var vector in queryVectors)
        {
            var hits = await KnnSearchAsync<T>(indices, vector, k, fileIdFilter, ct);
            allResults.AddRange(hits);
        }

        return allResults
            .GroupBy(h => h.Id)
            .Select(g => g.OrderByDescending(h => h.Score).First())
            .OrderByDescending(h => h.Score)
            .Take(k)
            .ToList();
    }

    private static readonly System.Text.RegularExpressions.Regex IcdCodePattern =
        new(@"^[A-Z]\d{2}(\.\d{1,2})?$", System.Text.RegularExpressions.RegexOptions.Compiled);

    public async Task<IReadOnlyList<SearchHit<T>>> SearchByMetadataAsync<T>(
        string[] indices, string[] queries, CancellationToken ct = default)
    {
        const int metadataResultWindow = 250;

        var normalizedQueries = queries
            .Where(q => !string.IsNullOrWhiteSpace(q))
            .Select(q => q.Trim())
            .Where(q => !q.Equals("Output:", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalizedQueries.Length == 0)
            return [];

        // Extract ICD-10 codes from queries for exact keyword matching
        var icdCodes = normalizedQueries.Where(q => IcdCodePattern.IsMatch(q)).ToArray();
        // Extract ICD-10 root codes (e.g., J20.9 -> J20) for prefix matching
        var icdRoots = icdCodes.Select(c => c.Contains('.') ? c.Split('.')[0] : c).Distinct().ToArray();

        var response = await _client.SearchAsync<T>(s => s
            .Index(string.Join(",", indices))
            .Size(metadataResultWindow)
            // De-duplicate at Elasticsearch level: one top hit per file_id.
            .Collapse(new FieldCollapse { Field = new Field("file_id") })
            .Query(q => q
                .Bool(b => b
                    .Should(
                        // Per-query matching across file-level signals first, chunk text second.
                        normalizedQueries.Select(query =>
                            (Action<QueryDescriptor<T>>)(sq =>
                                sq.Bool(bq => bq
                                    .Should(
                                        // Metadata is the strongest file-level signal.
                                        smm => smm.Match(m => m
                                            .Field(new Field("metadata"))
                                            .Query(query)
                                            .Fuzziness(new Fuzziness("AUTO"))
                                            .Boost(3.0f)),
                                        // Prefer exact phrase hit in protocol title.
                                        smm => smm.MatchPhrase(mp => mp
                                            .Field(new Field("file_name"))
                                            .Query(query)
                                            .Boost(3.5f)),
                                        // Keep fuzzy title match for typo tolerance.
                                        smm => smm.Match(m => m
                                            .Field(new Field("file_name"))
                                            .Query(query)
                                            .Fuzziness(new Fuzziness("AUTO"))
                                            .Boost(2.5f)),
                                        // Summary is still useful but secondary to metadata/title.
                                        smm => smm.Match(m => m
                                            .Field(new Field("summary"))
                                            .Query(query)
                                            .Fuzziness(new Fuzziness("AUTO"))
                                            .Boost(2.0f)),
                                        // Raw chunk text is the weakest signal for file discovery.
                                        smm => smm.Match(m => m
                                            .Field(new Field("text"))
                                            .Query(query)
                                            .Boost(0.75f)),
                                        // file_name.keyword exact hit for indexes with multi-field mapping.
                                        smm => smm.Term(new TermQuery(new Field("file_name.keyword"))
                                        {
                                            Value = query,
                                            Boost = 4.5f
                                        })
                                    )
                                )
                            )
                        )
                        // ICD-10 exact match on keyword field (strongest signal for protocol matching).
                        .Concat(icdCodes.Select(code =>
                            (Action<QueryDescriptor<T>>)(sq =>
                                sq.Term(new TermQuery(new Field("icd10_codes")) { Value = code, Boost = 5.0f }))
                        ))
                        // ICD-10 root prefix match (e.g., J20 matches J20.0, J20.1, etc.).
                        .Concat(icdRoots.Select(root =>
                            (Action<QueryDescriptor<T>>)(sq =>
                                sq.Prefix(p => p.Field(new Field("icd10_codes")).Value(root).Boost(4.0f)))
                        ))
                        .ToArray()
                    )
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
