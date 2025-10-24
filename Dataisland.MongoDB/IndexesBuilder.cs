using System.Linq.Expressions;
using MongoDB.Driver;

namespace Dataisland.MongoDB;

public record IndexDefinition<TDocument>(
    IndexKeysDefinition<TDocument> Definition,
    CreateIndexOptionsBuilder<TDocument> Options
);

public abstract class IndexesBuilder<TDocument>
{
    protected CreateIndexOptionsBuilder<TDocument> Index(IndexKeysDefinition<TDocument> definition)
    {
        var options = new CreateIndexOptionsBuilder<TDocument>();

        Definitions.Add(new IndexDefinition<TDocument>(definition, options));

        return options;
    }

    public List<IndexDefinition<TDocument>> Definitions { get; } = new();

    protected IndexKeysDefinition<TDocument> Ascending(Expression<Func<TDocument, object>> field) =>
        Builders<TDocument>.IndexKeys.Ascending(field);

    protected IndexKeysDefinition<TDocument> Ascending(FieldDefinition<TDocument> field) =>
        Builders<TDocument>.IndexKeys.Ascending(field);

    protected IndexKeysDefinition<TDocument> Descending(Expression<Func<TDocument, object>> field) =>
        Builders<TDocument>.IndexKeys.Descending(field);

    protected IndexKeysDefinition<TDocument> Descending(FieldDefinition<TDocument> field) =>
        Builders<TDocument>.IndexKeys.Descending(field);

    protected IndexKeysDefinition<TDocument> Text(Expression<Func<TDocument, object>> field) =>
        Builders<TDocument>.IndexKeys.Text(field);

    protected IndexKeysDefinition<TDocument> Text(FieldDefinition<TDocument> field) =>
        Builders<TDocument>.IndexKeys.Text(field);

    public FilterDefinitionBuilder<T> FilterOf<T>() => Builders<T>.Filter;

    public FilterDefinitionBuilder<TDocument> Filter => Builders<TDocument>.Filter;

    public ExpressionFilterDefinition<TDocument> Expression(Expression<Func<TDocument, bool>> expression) =>
        new ExpressionFilterDefinition<TDocument>(expression);
}