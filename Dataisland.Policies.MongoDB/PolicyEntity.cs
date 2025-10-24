using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Dataisland.Policies.MongoDB;

[Serializable]
public class PolicyEntity
{
    /// <summary>
    /// Unique id
    /// </summary>
    [BsonId]
    public ObjectId Id { get; private set; } = ObjectId.GenerateNewId();

    /// <summary>
    /// Create at
    /// </summary>
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    /// <summary>
    /// Modified at
    /// </summary>
    public DateTime ModifiedAt { get; private set; } = DateTime.UtcNow;

    /// <summary>
    /// Organization/Id
    /// </summary>
    public Principal Principal { get; set; } = null!;

    /// <summary>
    /// User/Id, Group/Id, etc
    /// </summary>
    public AssignedTo AssignedTo { get; set; } = null!;

    /// <summary>
    /// Name of policy
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Allow or Deny
    /// </summary>
    public PolicyEffect Effect { get; set; } = PolicyEffect.Allow;

    /// <summary>
    /// Actions, 'resource:action' | 'resource:*' | '*:action'
    /// Example: read, write, documents:read
    /// </summary>
    public PolicyAction[] Actions { get; set; } = null!;

    /// <summary>
    /// Resources,
    /// Example: organization://id
    /// </summary>
    public PolicyResource[] Resources { get; set; } = null!;
}