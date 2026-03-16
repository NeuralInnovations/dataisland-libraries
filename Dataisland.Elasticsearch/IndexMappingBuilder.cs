using Elastic.Clients.Elasticsearch.Mapping;

namespace Dataisland.Elasticsearch;

/// <summary>
/// Fluent builder for ES index field mappings.
/// Callers define their domain-specific schema; the library handles index creation.
/// </summary>
public class IndexMappingBuilder
{
    internal Properties Properties { get; } = new();

    public IndexMappingBuilder Text(string fieldName)
    {
        Properties.Add(fieldName, new TextProperty());
        return this;
    }

    public IndexMappingBuilder Keyword(string fieldName)
    {
        Properties.Add(fieldName, new KeywordProperty());
        return this;
    }

    public IndexMappingBuilder Integer(string fieldName)
    {
        Properties.Add(fieldName, new IntegerNumberProperty());
        return this;
    }

    public IndexMappingBuilder DenseVector(string fieldName, int dimensions, DenseVectorSimilarity similarity = DenseVectorSimilarity.Cosine)
    {
        Properties.Add(fieldName, new DenseVectorProperty
        {
            Dims = dimensions,
            Index = true,
            Similarity = similarity
        });
        return this;
    }
}
