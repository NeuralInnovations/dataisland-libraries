using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace Dataisland.MongoDB;

public abstract class RepositoryWithIndex<TDocument>(
    string collectionName,
    IMongoDBProvider provider,
    IndexesBuilder<TDocument> indexes
)
    : Repository<TDocument>(collectionName, provider), IRepositoryApplyIndex
{
    async Task IRepositoryApplyIndex.ApplyAsync(ILogger logger)
    {
        var definitions = indexes.Definitions;
        var collection = Collection;
        var indices = (await collection.Indexes.ListAsync()).ToList();
        foreach (var index in indices)
        {
            logger.LogInformation("[Index] Found {Name}", index["name"].AsString);
        }

        var created = new List<(BsonDocument Document, CreateIndexModel<TDocument> Model)>();

        foreach (var def in definitions)
        {
            var definition = def.Definition;
            var options = def.Options;

            var indexOptions = options.Options;

            var document = definition.Render(new RenderArgs<TDocument>(
                    documentSerializer: collection.DocumentSerializer,
                    serializerRegistry: BsonSerializer.SerializerRegistry
                )
            );

            indexOptions.Name ??= GenerateIndexName(document, indexOptions);

            var i = indices.FindIndex(x => x["name"].AsString == indexOptions.Name);
            if (i < 0)
            {
                created.Add((document, new CreateIndexModel<TDocument>(definition, indexOptions)));
            }
            else
            {
                indices.RemoveAt(i);
            }
        }

        try
        {
            foreach (var index in indices)
            {
                var name = index["name"].AsString;

                if (name != "_id_")
                {
                    logger.LogInformation("[Index] Delete {Name}", index["key"]);
                    await collection.Indexes.DropOneAsync(name);
                }
            }

            foreach (var (document, model) in created)
            {
                logger.LogInformation("[Index] Create {Name} {Document}", model.Options.Name, document);
            }

            if (created.Any())
            {
                await collection.Indexes.CreateManyAsync(created.Select(x => x.Model));
            }
        }
        catch (Exception e)
        {
            logger.LogCritical(e, $"[Index] Failed to create index");
            throw;
        }
    }

    private static string GenerateIndexName(BsonDocument document, CreateIndexOptions options)
    {
        var name = string.Join("_", document.Elements.Select(x => $"{x.Name}.{x.Value}"));

        if (options.Unique.HasValue)
        {
            name += "_U";
        }

        if (options.ExpireAfter.HasValue)
        {
            name += $"_E{options.ExpireAfter.Value.TotalSeconds:F0}";
        }

        return name;
    }
}