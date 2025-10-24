namespace Dataisland.Organizations;

public class OrganizationsOptions
{
    public string OrganizationTable { get; set; } = "Organizations";
    public string OrganizationMembersTable { get; set; } = "OrganizationMembers";
    public string GroupsTable { get; set; } = "Groups";
    public string GroupMembersTable { get; set; } = "GroupMembers";
    public string UsersTable { get; set; } = "Users";
    public string ApiKeyTable { get; set; } = "ApiKeys";

    public void CopyFrom(OrganizationsOptions options)
    {
        OrganizationTable = options.OrganizationTable;
        OrganizationMembersTable = options.OrganizationMembersTable;
        GroupsTable = options.GroupsTable;
        GroupMembersTable = options.GroupMembersTable;
        UsersTable = options.UsersTable;
        ApiKeyTable = options.ApiKeyTable;
    }

    public string[] Tables() =>
    [
        OrganizationTable,
        OrganizationMembersTable,
        GroupsTable,
        GroupMembersTable,
        UsersTable,
        ApiKeyTable,
    ];
}