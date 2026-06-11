namespace Dataisland.Elasticsearch;

public static class VectorIndexMapping
{
    public const int EmbeddingDimensions = 1024;

    public static void Configure(IndexMappingBuilder m) => m
        .Keyword("doc_id")
        .Keyword("file_id")
        .TextWithKeyword("file_name")
        .Integer("page")
        .Text("text")
        .Text("metadata")
        .Text("summary")
        .Keyword("icd10_codes")
        .Keyword("document_date")
        .Integer("file_type")
        .DenseVector("embedding", EmbeddingDimensions);
}
