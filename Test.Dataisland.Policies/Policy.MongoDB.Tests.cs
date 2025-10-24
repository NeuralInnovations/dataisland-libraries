using Dataisland.MongoDB;
using Dataisland.Organizations;
using Dataisland.Organizations.Users;
using Dataisland.Policies;
using Dataisland.Policies.MongoDB;
using Dataisland.Users.Policies;

namespace Test.Dataisland.Policies;

public class PolicyTests
{
    public WebApplication App { get; set; }

    [SetUp]
    public async Task Setup()
    {
        // Create Web builder
        var builder = WebApplication.CreateBuilder();

// Services
        // Mongo database
        builder.Services.AddMongoDB(new MongoDBOptions
        {
            ConnectionString = "mongodb://localhost:27017/test",
        });
        builder.Services.AddPolicies()
            .UseMongoDB()
            .UseTable("Policies")
            ;
// App
        var app = builder.Build();

// Wait before ready
        await app.Services.RunMongoDBAsync();
        await app.Services.RunPolicyAsync();

        this.App = app;
    }


    [Test]
    public async Task ConnectToMongoDb()
    {
        // delete collection if exists
        await App.Services.GetMongoProvider().Database.DropCollectionAsync("Policies");

        var policy = App.Services.PolicyModel();

        await policy.FindAsync(x => x.UserId(new OrganizationId("1"), new UserId("1")));
        await policy.AddAsync(q => q.UserId(new OrganizationId("1"), new UserId("1")), [
            new Policy("Read Documents", PolicyEffect.Allow, ["s3:read"], ["document://*"]),
            new Policy("Read Documents", PolicyEffect.Allow, ["file:read"], ["document://*"]),
        ]);

        await policy.AddAsync(q => q.UserId(new OrganizationId("1"), new UserId("2")), [
            new Policy("Write Documents", PolicyEffect.Allow, ["s3:write"], ["document://*"]),
            new Policy("Write Documents", PolicyEffect.Allow, ["file:write"], ["document://*"]),
        ]);
        await policy.AddAsync(q => q.UserId(new OrganizationId("1"), new UserId("2")), [
            new Policy("Write Documents", PolicyEffect.Allow, ["folder:write"], ["document://*"]),
        ]);

        Assert.That((await policy.FindAsync(x => x.UserId(new OrganizationId("1"), new UserId("1")))).Count(),
            Is.EqualTo(2));
        Assert.That((await policy.FindAsync(x => x.UserId(new OrganizationId("1"), new UserId("2")))).Count(),
            Is.EqualTo(3));

        Assert.Pass();
    }
}