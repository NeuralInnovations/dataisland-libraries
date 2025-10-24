using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace Dataisland.Organizations.MongoDB.Serializers;

internal sealed class OrganizationIdBsonSerializer : SerializerBase<OrganizationId>
{
    public static readonly OrganizationIdBsonSerializer Default = new();

    public override void Serialize(
        BsonSerializationContext context,
        BsonSerializationArgs args,
        OrganizationId value
    )
    {
        context.Writer.WriteObjectId(ObjectId.Parse(value.Value));
    }

    public override OrganizationId Deserialize(
        BsonDeserializationContext context,
        BsonDeserializationArgs args
    )
    {
        var s = context.Reader.ReadObjectId();
        return new OrganizationId(s.ToString());
    }
}