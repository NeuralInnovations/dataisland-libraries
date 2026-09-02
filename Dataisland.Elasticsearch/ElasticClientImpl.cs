using System.Text.Json;
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
    private static readonly TimeSpan FileTypeUpdateRequestTimeout = TimeSpan.FromMinutes(5);

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
        if (!await IndexExistsAsync(indexName, ct))
        {
            await CreateIndexAsync(indexName, VectorIndexMapping.Configure, ct);
            return;
        }

        await EnsureVectorIndexMappingsAsync(indexName, ct);
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

    public async Task<long> UpdateFileTypeByFileIdsAsync(
        string indexName,
        IReadOnlyCollection<string> fileIds,
        int fileType,
        CancellationToken ct = default)
    {
        var ids = fileIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (ids.Length == 0 || !await IndexExistsAsync(indexName, ct))
            return 0;

        await EnsureVectorIndexMappingsAsync(indexName, ct);

        var body = new
        {
            script = new
            {
                source = "ctx._source.file_type = params.fileType",
                lang = "painless",
                @params = new { fileType }
            },
            query = new
            {
                terms = new Dictionary<string, string[]>
                {
                    ["file_id"] = ids
                }
            }
        };

        var path = new Elastic.Transport.EndpointPath(
            Elastic.Transport.HttpMethod.POST,
            $"/{Uri.EscapeDataString(indexName)}/_update_by_query?conflicts=proceed&refresh=false&timeout=5m&scroll_size=500");
        var requestConfiguration = new Elastic.Transport.RequestConfiguration
        {
            RequestTimeout = FileTypeUpdateRequestTimeout,
            MaxRetryTimeout = FileTypeUpdateRequestTimeout,
            DisableDirectStreaming = true
        };
        var response = await _client.Transport.RequestAsync<Elastic.Transport.StringResponse>(
            in path,
            Elastic.Transport.PostData.String(JsonSerializer.Serialize(body)),
            null, requestConfiguration, ct);

        if (!response.ApiCallDetails.HasSuccessfulStatusCode)
            throw new InvalidOperationException(
                BuildFileTypeUpdateError(response, ids.Length, indexName));

        try
        {
            using var document = JsonDocument.Parse(response.Body);
            return document.RootElement.TryGetProperty("updated", out var updated) && updated.TryGetInt64(out var value)
                ? value
                : 0;
        }
        catch (JsonException)
        {
            return 0;
        }
    }

    // Push regenerated metadata into a file's existing chunks. The chunk vector is computed from
    // `text`, which a metadata regeneration never touches, so the whole point here is that nothing
    // has to be re-parsed or re-embedded: only the scoring/filtering fields change. Mirrors
    // UpdateFileTypeByFileIdsAsync, which does the same for file_type.
    public async Task<long> UpdateFileMetadataByFileIdAsync(
        string indexName,
        string fileId,
        IReadOnlyCollection<string>? icd10Codes,
        string? summary,
        string? metadata,
        string? documentDate,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(fileId) || !await IndexExistsAsync(indexName, ct))
            return 0;

        await EnsureVectorIndexMappingsAsync(indexName, ct);

        // Only assign what was actually regenerated: a null field means "generation produced
        // nothing for this", and overwriting a good stored value with null would lose data.
        var assignments = new List<string>();
        var parameters = new Dictionary<string, object?>();

        if (icd10Codes is not null)
        {
            assignments.Add("ctx._source.icd10_codes = params.icd10Codes;");
            parameters["icd10Codes"] = icd10Codes.ToArray();
        }
        if (summary is not null)
        {
            assignments.Add("ctx._source.summary = params.summary;");
            parameters["summary"] = summary;
        }
        if (metadata is not null)
        {
            assignments.Add("ctx._source.metadata = params.metadata;");
            parameters["metadata"] = metadata;
        }
        if (documentDate is not null)
        {
            assignments.Add("ctx._source.document_date = params.documentDate;");
            parameters["documentDate"] = documentDate;
        }

        if (assignments.Count == 0)
            return 0;

        var body = new
        {
            script = new
            {
                source = string.Join(" ", assignments),
                lang = "painless",
                @params = parameters
            },
            query = new
            {
                term = new Dictionary<string, string>
                {
                    ["file_id"] = fileId
                }
            }
        };

        var path = new Elastic.Transport.EndpointPath(
            Elastic.Transport.HttpMethod.POST,
            $"/{Uri.EscapeDataString(indexName)}/_update_by_query?conflicts=proceed&refresh=true&timeout=5m&scroll_size=500");
        var requestConfiguration = new Elastic.Transport.RequestConfiguration
        {
            RequestTimeout = FileTypeUpdateRequestTimeout,
            MaxRetryTimeout = FileTypeUpdateRequestTimeout,
            DisableDirectStreaming = true
        };
        var response = await _client.Transport.RequestAsync<Elastic.Transport.StringResponse>(
            in path,
            Elastic.Transport.PostData.String(JsonSerializer.Serialize(body)),
            null, requestConfiguration, ct);

        if (!response.ApiCallDetails.HasSuccessfulStatusCode)
            throw new InvalidOperationException(
                BuildFileTypeUpdateError(response, 1, indexName));

        try
        {
            using var document = JsonDocument.Parse(response.Body);
            return document.RootElement.TryGetProperty("updated", out var updated) && updated.TryGetInt64(out var value)
                ? value
                : 0;
        }
        catch (JsonException)
        {
            return 0;
        }
    }

    private static string BuildFileTypeUpdateError(
        Elastic.Transport.StringResponse response,
        int fileCount,
        string indexName)
    {
        var details = response.ApiCallDetails;
        var status = details.HttpStatusCode?.ToString() ?? "<none>";
        var exception = details.OriginalException is null
            ? "<none>"
            : $"{details.OriginalException.GetType().Name}: {details.OriginalException.Message}";
        var body = !string.IsNullOrWhiteSpace(response.Body)
            ? response.Body
            : DecodeResponseBody(details.ResponseBodyInBytes);
        var uri = details.Uri?.ToString() ?? "<unknown>";

        return
            $"Failed to update file_type for {fileCount} file(s) in {indexName}: HTTP {status}; " +
            $"uri={uri}; exception={exception}; body={TruncateForLog(body)}; debug={TruncateForLog(details.DebugInformation)}";
    }

    private static string DecodeResponseBody(byte[]? body) =>
        body is { Length: > 0 }
            ? System.Text.Encoding.UTF8.GetString(body)
            : "<empty>";

    private static string TruncateForLog(string? value, int maxLength = 2000)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "<empty>";

        return value.Length <= maxLength
            ? value
            : value[..maxLength] + "...";
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
        string[] indices, float[] queryVector, int k, string? fileIdFilter = null, int[]? fileTypeFilters = null, CancellationToken ct = default)
    {
        var filters = BuildSearchFilters<T>(fileIdFilter, fileTypeFilters);
        var response = await _client.SearchAsync<T>(s =>
            s.Index(string.Join(",", indices))
                .Knn(knn =>
                {
                    // When filtering by file_id, use more candidates to ensure good recall
                    // within a single file (unfiltered: k*2 is standard; filtered: k*5 compensates for filter)
                    var numCandidates = filters.Length == 0 ? k * 2 : Math.Max(k * 5, 100);
                    knn.Field(new Field("embedding"))
                        .QueryVector(queryVector)
                        .k(k)
                        .NumCandidates(numCandidates);

                    // Server-side filters are applied within KNN search, not client-side.
                    if (filters.Length > 0)
                        knn.Filter(f => f.Bool(b => b.Filter(filters)));
                }), ct);

        if (!response.IsValidResponse) return [];

        return response.Hits
            .Select(h => new SearchHit<T>(h.Id!, (float)(h.Score ?? 0), h.Source!))
            .ToList();
    }

    public async Task<IReadOnlyList<SearchHit<T>>> MultiSearchAsync<T>(
        string[] indices, float[][] queryVectors, int k, string? fileIdFilter = null, int[]? fileTypeFilters = null, CancellationToken ct = default)
    {
        var allResults = new List<SearchHit<T>>();

        foreach (var vector in queryVectors)
        {
            var hits = await KnnSearchAsync<T>(indices, vector, k, fileIdFilter, fileTypeFilters, ct);
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
        string[] indices, string[] queries, int[]? fileTypeFilters = null, CancellationToken ct = default)
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

        var filters = BuildSearchFilters<T>(null, fileTypeFilters);
        var response = await _client.SearchAsync<T>(s => s
            .Index(string.Join(",", indices))
            .Size(metadataResultWindow)
            // De-duplicate at Elasticsearch level: one top hit per file_id.
            .Collapse(new FieldCollapse { Field = new Field("file_id") })
            .Query(q => q
                .Bool(b => b
                    .Filter(filters)
                    .MinimumShouldMatch(1)
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

    private async Task EnsureVectorIndexMappingsAsync(string indexName, CancellationToken ct)
    {
        const string mappingJson = """
            {
              "properties": {
                "file_type": { "type": "integer" }
              }
            }
            """;

        try
        {
            var path = new Elastic.Transport.EndpointPath(
                Elastic.Transport.HttpMethod.PUT,
                $"/{Uri.EscapeDataString(indexName)}/_mapping");
            var response = await _client.Transport.RequestAsync<Elastic.Transport.StringResponse>(
                in path,
                Elastic.Transport.PostData.String(mappingJson),
                null, null, ct);

            if (!response.ApiCallDetails.HasSuccessfulStatusCode)
                _logger.LogWarning(
                    "Failed to ensure Elasticsearch file_type mapping for {IndexName}: HTTP {Status}",
                    indexName,
                    response.ApiCallDetails.HttpStatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not ensure Elasticsearch file_type mapping for {IndexName}", indexName);
        }
    }

    private static Action<QueryDescriptor<T>>[] BuildSearchFilters<T>(string? fileIdFilter, int[]? fileTypeFilters)
    {
        var filters = new List<Action<QueryDescriptor<T>>>();

        if (!string.IsNullOrWhiteSpace(fileIdFilter))
            filters.Add(q => q.Term(new TermQuery(new Field("file_id")) { Value = fileIdFilter }));

        var normalizedFileTypes = fileTypeFilters?
            .Distinct()
            .ToArray();
        if (normalizedFileTypes is { Length: > 0 })
        {
            var typeClauses = normalizedFileTypes
                .Select<int, Action<QueryDescriptor<T>>>(type =>
                    q => q.Term(new TermQuery(new Field("file_type")) { Value = type }))
                .ToArray();

            filters.Add(q => q.Bool(b => b
                .Should(typeClauses)
                .MinimumShouldMatch(1)));
        }

        return filters.ToArray();
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
