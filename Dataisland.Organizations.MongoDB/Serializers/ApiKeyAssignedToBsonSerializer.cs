using Dataisland.Organizations.ApiKeys;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace Dataisland.Organizations.MongoDB.Serializers;

public class ApiKeyAssignedToBsonSerializer : SerializerBase<ApiKeyAssignedTo>
{
    public static readonly ApiKeyAssignedToBsonSerializer Default = new();

    public override void Serialize(
        BsonSerializationContext context,
        BsonSerializationArgs args,
        ApiKeyAssignedTo value
    )
    {
        context.Writer.WriteObjectId(ObjectId.Parse(value.Value));
    }

    public override ApiKeyAssignedTo Deserialize(
        BsonDeserializationContext context,
        BsonDeserializationArgs args
    )
    {
        var s = context.Reader.ReadObjectId();
        return new ApiKeyAssignedTo(s.ToString());
    }
}