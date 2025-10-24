using Dataisland.Organizations.Users;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace Dataisland.Organizations.MongoDB.Serializers;

internal sealed class UserIdBsonSerializer : SerializerBase<UserId>
{
    public static readonly UserIdBsonSerializer Default = new();

    public override void Serialize(
        BsonSerializationContext context,
        BsonSerializationArgs args,
        UserId value
    )
    {
        context.Writer.WriteObjectId(ObjectId.Parse(value.Value));
    }

    public override UserId Deserialize(
        BsonDeserializationContext context,
        BsonDeserializationArgs args
    )
    {
        var s = context.Reader.ReadObjectId();
        return new UserId(s.ToString());
    }
}