using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Serializers = MongoDB.Bson.Serialization.Serializers;

namespace Dataisland.Policies.MongoDB;

internal sealed class PolicyIdBsonSerializer : Serializers.SerializerBase<PolicyId>
{
    public static readonly PolicyIdBsonSerializer Default = new PolicyIdBsonSerializer();

    public override void Serialize(
        BsonSerializationContext context,
        BsonSerializationArgs args,
        PolicyId value
    )
    {
        context.Writer.WriteObjectId(ObjectId.Parse(value));
    }

    public override PolicyId Deserialize(
        BsonDeserializationContext context,
        BsonDeserializationArgs args
    )
    {
        var s = context.Reader.ReadObjectId();
        return (PolicyId)s.ToString();
    }
}

internal sealed class PolicyActionBsonSerializer : Serializers.SerializerBase<PolicyAction>
{
    public static readonly PolicyActionBsonSerializer Default = new PolicyActionBsonSerializer();

    public override void Serialize(
        BsonSerializationContext context,
        BsonSerializationArgs args,
        PolicyAction value
    )
    {
        context.Writer.WriteString(value);
    }

    public override PolicyAction Deserialize(
        BsonDeserializationContext context,
        BsonDeserializationArgs args
    )
    {
        var s = context.Reader.ReadString();
        return (PolicyAction)s;
    }
}

internal sealed class
    PolicyResourceBsonSerializer : Serializers.SerializerBase<PolicyResource>
{
    public static readonly PolicyResourceBsonSerializer Default = new PolicyResourceBsonSerializer();

    public override void Serialize(
        BsonSerializationContext context,
        BsonSerializationArgs args,
        PolicyResource value
    )
    {
        context.Writer.WriteString(value);
    }

    public override PolicyResource Deserialize(
        BsonDeserializationContext context,
        BsonDeserializationArgs args
    )
    {
        var s = context.Reader.ReadString();
        return (PolicyResource)s;
    }
}

internal sealed class AssignedToBsonSerializer : Serializers.SerializerBase<AssignedTo>
{
    public static readonly AssignedToBsonSerializer Default = new AssignedToBsonSerializer();

    public override void Serialize(
        BsonSerializationContext context,
        BsonSerializationArgs args,
        AssignedTo value
    )
    {
        context.Writer.WriteString(value);
    }

    public override AssignedTo Deserialize(
        BsonDeserializationContext context,
        BsonDeserializationArgs args
    )
    {
        var s = context.Reader.ReadString();
        return (AssignedTo)s;
    }
}

internal sealed class PrincipalBsonSerializer : Serializers.SerializerBase<Principal>
{
    public static readonly PrincipalBsonSerializer Default = new PrincipalBsonSerializer();

    public override void Serialize(
        BsonSerializationContext context,
        BsonSerializationArgs args,
        Principal value
    )
    {
        context.Writer.WriteString(value);
    }

    public override Principal Deserialize(
        BsonDeserializationContext context,
        BsonDeserializationArgs args
    )
    {
        var s = context.Reader.ReadString();
        return (Principal)s;
    }
}