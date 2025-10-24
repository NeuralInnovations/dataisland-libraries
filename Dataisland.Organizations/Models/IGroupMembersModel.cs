using Dataisland.Organizations.Groups;
using Dataisland.Organizations.Users;

namespace Dataisland.Organizations.Models;

public interface IGroupMembersModel : IEnsureCreated
{
    Task<int> AddAsync(GroupId groupId, IEnumerable<UserId> userIds);
    Task<int> RemoveAsync(GroupId groupId, IEnumerable<UserId> userIds);
    Task<IEnumerable<UserId>> FindAsync(GroupId groupId);
}