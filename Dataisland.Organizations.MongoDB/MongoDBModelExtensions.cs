using Dataisland.MongoDB;
using Dataisland.Organizations.MongoDB.Models;
using Dataisland.Organizations.MongoDB.Serializers;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson.Serialization;

namespace Dataisland.Organizations.MongoDB;

public static class MongoDBModelExtensions
{
    public static OrganizationsModelBuilder UseMongoDB(this OrganizationsModelBuilder builder)
    {
        builder.UseModel((sp, options) =>
        {
            BsonSerializer.TryRegisterSerializer(OrganizationIdBsonSerializer.Default);
            BsonSerializer.TryRegisterSerializer(GroupIdBsonSerializer.Default);
            BsonSerializer.TryRegisterSerializer(UserIdBsonSerializer.Default);
            BsonSerializer.TryRegisterSerializer(ApiKeyAssignedToBsonSerializer.Default);

            var database = sp.GetRequiredService<IMongoDBProvider>();

            var model = new MongoDBModels(database, options);
            return model;
        });
        return builder;
    }
}