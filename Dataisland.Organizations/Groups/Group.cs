namespace Dataisland.Organizations.Groups;

[Serializable]
public class Group
{
    public GroupId Id { get; set; }
    public OrganizationId OrganizationId { get; set; }
    public string Name { get; set; } = "New Group";
}