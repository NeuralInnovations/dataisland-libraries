using Dataisland.Startup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dataisland.Policies;

public static class PoliciesProviderExtensions
{
    public static PolicyModelBuilder AddPolicies(
        this IServiceCollection services
    )
    {
        var builder = new PolicyModelBuilder();
        services.AddStartupChecker<IPoliciesModel>();

        services.AddSingleton<IOptions<PolicyOptions>>(sp => new OptionsWrapper<PolicyOptions>(builder.Build()));
        services.AddSingleton<IPoliciesModel>(sp =>
        {
            var options = builder.Build();
            if (builder.Model == null)
                throw new InvalidOperationException(
                    $"{nameof(IPoliciesModel)} was not configured. Call {nameof(AddPolicies)}.{nameof(PolicyModelBuilder.UseModel)}(...) options."
                );
            var model = builder.Model(sp, options) ?? null;
            if (model == null)
                throw new InvalidOperationException(
                    $"{nameof(IPoliciesModel)} was not configured. Call {nameof(AddPolicies)}.{nameof(PolicyModelBuilder.UseModel)}(...) options."
                );
            return model;
        });
        return builder;
    }

    public static async Task RunPolicyAsync(this IServiceProvider provider)
    {
        // Wait for all DbContexts to be created
        await Task.WhenAll(
            tasks: provider
                .GetServices<IPoliciesModel>()
                .Select(x => x.EnsureCreatedAsync())
        );

        provider.Configure<IPoliciesModel>();
    }

    public static Task RunPolicyAsync(this IHostStartup startup)
    {
        return startup.Services.RunPolicyAsync();
    }

    public static Task<IEnumerable<Policy>> FindAsync(
        this IPoliciesModel model,
        Func<PolicyQuery, PolicyQuery> query
    )
    {
        var q = new PolicyQuery(default, default);
        var result = query(q);
        return model.FindAsync(result.Principal, result.AssignedTo);
    }

    public static Task AddAsync(
        this IPoliciesModel model,
        Func<PolicyQuery, PolicyQuery> query,
        IEnumerable<Policy> policies
    )
    {
        var q = new PolicyQuery(default, default);
        var result = query(q);
        return model.AddAsync(result.Principal, result.AssignedTo, policies);
    }

    public static IPoliciesModel PolicyModel(this IServiceProvider provider)
        => provider.GetRequiredService<IPoliciesModel>();
}