using Dataisland.MongoDB;
using Dataisland.Organizations.Models;

namespace Dataisland.Organizations.MongoDB.Models;

public class MongoDBModels(IMongoDBProvider provider, OrganizationsOptions options) : IModels
{
    public IOrganizationsModel Organizations { get; } =
        new OrganizationMongoDBModel(provider, options.OrganizationTable);

    public IOrganizationMembersModel OrganizationMembers { get; } =
        new OrganizationMembersMongoDBModel(provider, options.OrganizationMembersTable);

    public IGroupsModel Groups { get; } = new GroupsMongoDBModel(provider, options.GroupsTable);
    public IGroupMembersModel GroupMembers { get; } = new GroupMembersMongoDBModel(provider, options.GroupMembersTable);
    public IUsersModel Users { get; } = new UsersMongoDBModel(provider, options.UsersTable);
    public IApiKeyModel ApiKeys { get; } = new ApiKeyMongoDBModel(provider, options.ApiKeyTable);

    public async Task EnsureCreatedAsync()
    {
        await Users.EnsureCreatedAsync();
        await Organizations.EnsureCreatedAsync();
        await OrganizationMembers.EnsureCreatedAsync();
        await Groups.EnsureCreatedAsync();
        await GroupMembers.EnsureCreatedAsync();
        await ApiKeys.EnsureCreatedAsync();
    }
}