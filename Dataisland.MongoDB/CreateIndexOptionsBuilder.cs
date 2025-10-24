using MongoDB.Driver;

namespace Dataisland.MongoDB;

public class CreateIndexOptionsBuilder<TDocument>
{
    public readonly CreateIndexOptions<TDocument> Options = new();

    public CreateIndexOptionsBuilder<TDocument> Name(string name)
    {
        Options.Name = name;

        return this;
    }

    public CreateIndexOptionsBuilder<TDocument> Expire(TimeSpan expiration)
    {
        Options.ExpireAfter = expiration;

        return this;
    }

    public CreateIndexOptionsBuilder<TDocument> Partial(FilterDefinition<TDocument> filter)
    {
        Options.PartialFilterExpression = filter;

        return this;
    }

    public CreateIndexOptionsBuilder<TDocument> Sparse()
    {
        Options.Sparse = true;

        return this;
    }

    public CreateIndexOptionsBuilder<TDocument> Unique()
    {
        Options.Unique = true;

        return this;
    }
}