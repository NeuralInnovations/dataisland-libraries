using Dataisland.Organizations.Groups;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace Dataisland.Organizations.MongoDB.Serializers;

internal sealed class GroupIdBsonSerializer : SerializerBase<GroupId>
{
    public static readonly GroupIdBsonSerializer Default = new();

    public override void Serialize(
        BsonSerializationContext context,
        BsonSerializationArgs args,
        GroupId value
    )
    {
        context.Writer.WriteObjectId(ObjectId.Parse(value.Value));
    }

    public override GroupId Deserialize(
        BsonDeserializationContext context,
        BsonDeserializationArgs args
    )
    {
        var s = context.Reader.ReadObjectId();
        return new GroupId(s.ToString());
    }
}