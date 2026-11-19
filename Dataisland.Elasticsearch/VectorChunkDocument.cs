using System.Text.Json.Serialization;

namespace Dataisland.Elasticsearch;

public class VectorChunkDocument
{
    [JsonPropertyName("doc_id")]
    public string DocId { get; set; } = "";

    [JsonPropertyName("file_id")]
    public string FileId { get; set; } = "";

    [JsonPropertyName("file_name")]
    public string? FileName { get; set; }

    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; set; }

    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; } = "";

    [JsonPropertyName("embedding")]
    public float[] Embedding { get; set; } = [];

    [JsonPropertyName("metadata")]
    public string? Metadata { get; set; }

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("icd10_codes")]
    public string[] Icd10Codes { get; set; } = [];

    [JsonPropertyName("document_date")]
    public string? DocumentDate { get; set; }

    [JsonPropertyName("file_type")]
    public int? FileType { get; set; }

    /// <summary>
    /// Every type the file answers to, written only for a file that has more than one (a clinic document
    /// that is both a protocol and a patient route). Null for an ordinary file, whose one type is
    /// <see cref="FileType"/>. A type filter matches either field.
    /// </summary>
    [JsonPropertyName("file_types")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int[]? FileTypes { get; set; }
}
