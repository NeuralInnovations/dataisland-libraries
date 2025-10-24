using Dataisland.MongoDB;
using Dataisland.Organizations;
using Dataisland.Organizations.MongoDB;
using Dataisland.Organizations.Users;

namespace Test.Dataisland.Policies;

public class OrganizationTests
{
    private WebApplicationBuilder _builder;

    [SetUp]
    public void Setup()
    {
        _builder = WebApplication.CreateBuilder();
        _builder.Services.AddMongoDB(new MongoDBOptions
        {
            ConnectionString = "mongodb://localhost:27017/test",
        });
    }


    [Test]
    public async Task ConnectToMongoDb()
    {
        var options = new OrganizationsOptions
        {
            OrganizationTable = "A_Organizations",
            OrganizationMembersTable = "A_OrganizationMembers",
            GroupsTable = "A_Groups",
            GroupMembersTable = "A_GroupMembers",
            UsersTable = "A_Users",
        };

        _builder.Services.AddOrganizations()
            .UseMongoDB()
            .Options(o => o.CopyFrom(options))
            ;
// App
        var app = _builder.Build();

// Wait before ready
        await app.Services.RunMongoDBAsync();
        await app.Services.RunOrganizationsAsync();

        // delete collection if exists
        foreach (var table in options.Tables())
        {
            await app.Services.GetMongoProvider().Database.DropCollectionAsync(table);
        }

        var organizations = app.Services.OrganizationsModel();
        var organizationMembers = app.Services.OrganizationMembersModel();
        var groups = app.Services.GroupsModel();
        var groupMembers = app.Services.GroupMembersModel();
        var users = app.Services.UsersModel();

        // Create organization
        var organization = await organizations.CreateAsync("test");
        Assert.That(organization, Is.Not.Null);

        // Organization exists
        var exists = await organizations.ExistsAsync(organization.Id);
        Assert.That(exists, Is.True);

        // Find organization
        var find = await organizations.FindAsync(organization.Id);
        Assert.That(find, Is.Not.Null);

        // Delete organization
        var delete = await organizations.DeleteAsync(organization.Id);
        Assert.That(delete, Is.True);

        // Create user
        var user = await users.CreateAsync();

        // Add member
        var members = await organizationMembers.AddAsync(organization.Id, new[] { user.Id });
        Assert.That(members.Count(), Is.EqualTo(1));

        // Find member by organization
        var findMembers = await organizationMembers.FindAsync(organization.Id);
        Assert.That(findMembers.Count(), Is.EqualTo(1));

        // Find member by user
        var findMembersByUser = await organizationMembers.FindAsync(user.Id);
        Assert.That(findMembersByUser.Count(), Is.EqualTo(1));

        // Find member by organization and user
        var findMember = await organizationMembers.FindAsync(organization.Id, user.Id);
        Assert.That(findMember, Is.Not.Null);

        // Change status
        var changeStatus =
            await organizationMembers.ChangeStatusAsync(organization.Id, new[] { user.Id }, OrganizationMemberStatus.Suspended);
        Assert.That(changeStatus, Is.EqualTo(1));

        // Remove member
        var remove = await organizationMembers.RemoveAsync(organization.Id, new[] { user.Id });
        Assert.That(remove, Is.EqualTo(1));

        // Create group
        var group = await groups.CreateAsync(organization.Id, "test");
        Assert.That(group, Is.Not.Null);

        // Find groups
        var findGroups = await groups.FindAsync(organization.Id);
        Assert.That(findGroups.Count(), Is.EqualTo(1));

        // Find group
        var findGroup = await groups.FindAsync(group.Id);
        Assert.That(findGroup, Is.Not.Null);

        // Delete group
        var deleteGroup = await groups.DeleteAsync(group.Id);
        Assert.That(deleteGroup, Is.True);

        // Add group member
        var addMember = await groupMembers.AddAsync(group.Id, new[] { user.Id });
        Assert.That(addMember, Is.EqualTo(1));

        // Find group members
        var findGroupMembers = await groupMembers.FindAsync(group.Id);
        Assert.That(findGroupMembers.Count(), Is.EqualTo(1));

        // Remove group member
        var removeMember = await groupMembers.RemoveAsync(group.Id, new[] { user.Id });
        Assert.That(removeMember, Is.EqualTo(1));
    }

    [Test]
    public async Task ComplexScenarios()
    {
        var options = new OrganizationsOptions
        {
            OrganizationTable = "CS_Organizations",
            OrganizationMembersTable = "CS_OrganizationMembers",
            GroupsTable = "CS_Groups",
            GroupMembersTable = "CS_GroupMembers",
            UsersTable = "CS_Users",
        };

        _builder.Services.AddOrganizations()
            .UseMongoDB()
            .Options(o => o.CopyFrom(options))
            ;
// App
        var app = _builder.Build();

// Wait before ready
        await app.Services.RunMongoDBAsync();
        await app.Services.RunOrganizationsAsync();

        // delete collection if exists

        foreach (var table in options.Tables())
        {
            await app.Services.GetMongoProvider().Database.DropCollectionAsync(table);
        }

        var organizations = app.Services.OrganizationsModel();
        var organizationMembers = app.Services.OrganizationMembersModel();
        var groups = app.Services.GroupsModel();
        var groupMembers = app.Services.GroupMembersModel();
        var users = app.Services.UsersModel();

        // Create users
        var userIds = new List<UserId>();
        for (var i = 0; i < 10; i++)
        {
            userIds.Add((await users.CreateAsync()).Id);
        }

        // Create organizations
        var organizationIds = new List<OrganizationId>();
        for (var i = 0; i < 3; i++)
        {
            var organization = await organizations.CreateAsync($"test-{i}");
            organizationIds.Add(organization.Id);
        }

        // Add members to organizations
        foreach (var organizationId in organizationIds)
        {
            await organizationMembers.AddAsync(organizationId, userIds.Take(5));
        }

        // Create groups and add members
        foreach (var organizationId in organizationIds)
        {
            for (var i = 0; i < 2; i++)
            {
                var group = await groups.CreateAsync(organizationId, $"group-{i}");
                await groupMembers.AddAsync(group.Id, userIds.Skip(5).Take(5));
            }
        }

        // Assertions
        foreach (var organizationId in organizationIds)
        {
            var members = await organizationMembers.FindAsync(organizationId);
            Assert.That(members.Count(), Is.EqualTo(5));

            var orgGroups = await groups.FindAsync(organizationId);
            Assert.That(orgGroups.Count(), Is.EqualTo(2));

            foreach (var group in orgGroups)
            {
                var membersInGroup = await groupMembers.FindAsync(group.Id);
                Assert.That(membersInGroup.Count(), Is.EqualTo(5));
            }
        }
    }
}