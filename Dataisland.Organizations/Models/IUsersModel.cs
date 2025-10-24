using Dataisland.Organizations.Users;

namespace Dataisland.Organizations.Models;

public interface IUsersModel : IEnsureCreated
{
    Task<User> CreateAsync();
    Task<User?> FindAsync(UserId id);
}