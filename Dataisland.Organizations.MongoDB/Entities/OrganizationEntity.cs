namespace Dataisland.Organizations.MongoDB.Entities;

[Serializable]
public class OrganizationEntity : EntityId
{
    public OrganizationStatus Status { get; set; }
    public OrganizationProfile Profile { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
}