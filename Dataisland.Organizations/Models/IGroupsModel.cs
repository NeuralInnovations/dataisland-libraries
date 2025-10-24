using Dataisland.Organizations.Groups;

namespace Dataisland.Organizations.Models;

public interface IGroupsModel : IEnsureCreated
{
    Task<Group> CreateAsync(OrganizationId id, string name);
    Task<IEnumerable<Group>> FindAsync(OrganizationId id);
    Task<Group?> FindAsync(GroupId id);
    Task<bool> DeleteAsync(GroupId id);
}