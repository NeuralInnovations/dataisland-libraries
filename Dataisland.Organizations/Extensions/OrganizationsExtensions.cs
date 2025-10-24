using Dataisland.Organizations.Models;
using Dataisland.Startup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dataisland.Organizations;

public static class OrganizationsExtensions
{
    public static OrganizationsModelBuilder AddOrganizations(
        this IServiceCollection services
    )
    {
        var builder = new OrganizationsModelBuilder();
        services.AddStartupChecker<IModels>();

        services.AddSingleton<IOptions<OrganizationsOptions>>(sp =>
            new OptionsWrapper<OrganizationsOptions>(builder.Build()));
        services.AddSingleton<IModels>(sp =>
        {
            var options = builder.Build();
            if (builder.ModelFactory == null)
                throw new InvalidOperationException(
                    $"{nameof(IOrganizationsModel)} was not configured. Call {nameof(AddOrganizations)}.{nameof(OrganizationsModelBuilder.UseModel)}(...) options."
                );
            var model = builder.ModelFactory(sp, options) ?? null;
            if (model == null)
                throw new InvalidOperationException(
                    $"{nameof(IOrganizationsModel)} was not configured. Call {nameof(AddOrganizations)}.{nameof(OrganizationsModelBuilder.UseModel)}(...) options."
                );
            return model;
        });

        services.AddSingleton<IOrganizationsModel>(s => s.GetRequiredService<IModels>().Organizations);
        services.AddSingleton<IOrganizationMembersModel>(s => s.GetRequiredService<IModels>().OrganizationMembers);
        services.AddSingleton<IGroupsModel>(s => s.GetRequiredService<IModels>().Groups);
        services.AddSingleton<IGroupMembersModel>(s => s.GetRequiredService<IModels>().GroupMembers);
        services.AddSingleton<IUsersModel>(s => s.GetRequiredService<IModels>().Users);

        return builder;
    }

    public static IOrganizationsModel OrganizationsModel(this IServiceProvider provider)
        => provider.GetRequiredService<IOrganizationsModel>();

    public static IOrganizationMembersModel OrganizationMembersModel(this IServiceProvider provider)
        => provider.GetRequiredService<IOrganizationMembersModel>();

    public static IGroupsModel GroupsModel(this IServiceProvider provider)
        => provider.GetRequiredService<IGroupsModel>();

    public static IGroupMembersModel GroupMembersModel(this IServiceProvider provider)
        => provider.GetRequiredService<IGroupMembersModel>();

    public static IUsersModel UsersModel(this IServiceProvider provider)
        => provider.GetRequiredService<IUsersModel>();

    public static async Task RunOrganizationsAsync(this IServiceProvider provider)
    {
        // Wait for all DbContexts to be created
        await Task.WhenAll(
            tasks: provider
                .GetServices<IModels>()
                .Select(x => x.EnsureCreatedAsync())
        );

        provider.Configure<IModels>();
    }
}