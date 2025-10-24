namespace Dataisland.Organizations.Models;

public interface IModels : IEnsureCreated
{
    IOrganizationsModel Organizations { get; }
    IOrganizationMembersModel OrganizationMembers { get; }
    IGroupsModel Groups { get; }
    IGroupMembersModel GroupMembers { get; }
    IUsersModel Users { get; }
    IApiKeyModel ApiKeys { get; }
}