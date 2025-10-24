using Dataisland.MongoDB;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace Dataisland.Policies.MongoDB;

public static class MongoDBModelExtensions
{
    public static PolicyModelBuilder UseMongoDB(this PolicyModelBuilder builder)
    {
        builder.UseModel((sp, options) =>
        {
            BsonSerializer.TryRegisterSerializer(PolicyIdBsonSerializer.Default);
            BsonSerializer.TryRegisterSerializer(PolicyActionBsonSerializer.Default);
            BsonSerializer.TryRegisterSerializer(PolicyResourceBsonSerializer.Default);
            BsonSerializer.TryRegisterSerializer(AssignedToBsonSerializer.Default);
            BsonSerializer.TryRegisterSerializer(PrincipalBsonSerializer.Default);

            var database = sp.GetRequiredService<IMongoDBProvider>();

            var model = new MongoDBModel(database, options.TableName ?? "Policies");
            return model;
        });
        return builder;
    }
}

public class MongoDBModel(IMongoDBProvider provider, string collection) :
    RepositoryWithIndex<PolicyEntity>(collection, provider, new Indexes()),
    IPoliciesModel
{
    class Indexes : IndexesBuilder<PolicyEntity>
    {
        public Indexes()
        {
            Index(Ascending(x => x.Id));
            Index(Ascending(x => x.AssignedTo));
            Index(
                Ascending(x => x.Principal)
                    .Ascending(x => x.AssignedTo)
            );
        }
    }

    public async Task<IEnumerable<Policy>> FindAsync(Principal principal, AssignedTo assignedTo)
    {
        var assignedToValue = assignedTo.Value;
        var principalValue = principal.Value;
        var entities = await Collection
            .Find(x => x.AssignedTo == assignedToValue && x.Principal == principalValue)
            .ToListAsync();

        return entities
            .Select(entity => new Policy(entity.Name, entity.Effect, entity.Actions, entity.Resources));
    }

    public async Task<Policy?> FindAsync(PolicyId id)
    {
        var objectId = ObjectId.Parse(id);
        var result = await Collection
            .Find(x => x.Id == objectId)
            .FirstOrDefaultAsync();
        if (result != null)
        {
            return new Policy(result.Name, result.Effect, result.Actions, result.Resources)
            {
                Id = result.Id.ToString()
            };
        }

        return null;
    }

    public async Task<int> DeleteAsync(IEnumerable<PolicyId> policies)
    {
        var ids = policies.Select(id => ObjectId.Parse(id)).ToArray();
        if (ids.Length != 0)
        {
            var result = await Collection.DeleteManyAsync(x => ids.Contains(x.Id));
            return (int)result.DeletedCount;
        }

        return 0;
    }

    public async Task AddAsync(Principal principal, AssignedTo assignedTo, IEnumerable<Policy> policy)
    {
        var items = policy.Select(x => new PolicyEntity
        {
            AssignedTo = assignedTo,
            Principal = principal,
            Name = x.Name,
            Effect = x.Effect,
            Actions = x.Actions,
            Resources = x.Resources
        }).ToArray();

        if (items.Length != 0)
        {
            await Collection.InsertManyAsync(items);
        }
    }

    public Task EnsureCreatedAsync() => Task.CompletedTask;
}