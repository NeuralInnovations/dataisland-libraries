using Dataisland.Organizations.ApiKeys;

namespace Dataisland.Organizations.MongoDB.Entities;

[Serializable]
public class ApiKeyEntity : EntityId
{
    public ApiKeyAssignedTo AssignedTo { get; set; }
    public string Key { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAt { get; set; }
}