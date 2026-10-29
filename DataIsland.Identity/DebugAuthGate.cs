using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace DataIsland.Identity;

/// <summary>
/// Whether the <see cref="DebugAuthHandler"/> scheme may be used at all, and why not when it may
/// not. One place on purpose: the scheme registration
/// (<see cref="IdentityExtensions.AddDataIslandIdentity"/>) and every authorization policy that
/// lists the scheme have to agree, and a second hand-kept copy of the condition is exactly how a
/// bypass stays alive in one of them.
/// </summary>
/// <remarks>
/// Debug auth mints an administrator principal whose subject and e-mail the CALLER chooses, so a
/// caller that reaches it picks which user it is. Every condition below therefore fails closed, and
/// they are independent on purpose — no single mistake (a wrong environment name, a leaked secret,
/// an exposed port) is enough on its own:
/// <list type="number">
/// <item><c>Auth:Debug</c> must be explicitly on. The shipped configuration has it OFF.</item>
/// <item>The environment must be Development.</item>
/// <item>The secret must be long enough and must not be one of the placeholders that are committed
/// to source (<c>LOCAL_…</c>). A secret published in a repository is not a secret.</item>
/// <item>The request must come from loopback, or from a network explicitly listed in
/// <c>Auth:DebugAllowedNetworks</c> (<see cref="IsAllowedCaller"/>). Debug auth exists for a
/// developer on their own machine: `dotnet run` and `kubectl port-forward` both arrive from
/// loopback, traffic through an ingress or a service does not. A container stack published to the
/// host is the one legitimate exception, and it has to name its bridge network to get it.</item>
/// </list>
/// </remarks>
public static class DebugAuthGate
{
    public const string EnabledKey = "Auth:Debug";
    public const string SecretKey = "DataIslandAuth:DebugSecret";

    /// <summary>
    /// Comma-separated CIDRs beyond loopback that may use debug auth. Empty — as everywhere except
    /// a developer's own container stack — means loopback only.
    /// </summary>
    public const string AllowedNetworksKey = "Auth:DebugAllowedNetworks";

    /// <summary>Prefix of the placeholder secrets committed to <c>appsettings.yml</c>.</summary>
    public const string CommittedSecretPrefix = "LOCAL_";

    public const int MinimumSecretLength = 24;

    public static DebugAuthDecision Evaluate(IConfiguration configuration, string? environmentName = null)
    {
        if (!configuration.GetValue<bool>(EnabledKey))
            return new DebugAuthDecision(false, null, $"{EnabledKey} is off");

        // Production unless told otherwise, and DOTNET_ENVIRONMENT counts as much as
        // ASPNETCORE_ENVIRONMENT: a host that reads one and a gate that reads the other is how a
        // bypass survives the guard that was supposed to close it.
        var environment = environmentName
                          ?? configuration["ASPNETCORE_ENVIRONMENT"]
                          ?? configuration["DOTNET_ENVIRONMENT"]
                          ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                          ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
                          ?? "Production";
        if (!string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase))
            return new DebugAuthDecision(false, null,
                $"the environment is {environment}, not Development");

        var secret = configuration[SecretKey];
        if (string.IsNullOrWhiteSpace(secret))
            return new DebugAuthDecision(false, null, $"{SecretKey} is not set");

        secret = secret.Trim();
        if (secret.StartsWith(CommittedSecretPrefix, StringComparison.OrdinalIgnoreCase))
            return new DebugAuthDecision(false, null,
                $"{SecretKey} is still a committed {CommittedSecretPrefix}… placeholder");

        if (secret.Length < MinimumSecretLength)
            return new DebugAuthDecision(false, null,
                $"{SecretKey} is shorter than {MinimumSecretLength} characters");

        return new DebugAuthDecision(true, secret, "enabled for local development",
            ParseNetworks(configuration[AllowedNetworksKey]));
    }

    /// <summary>
    /// Loopback, plus any network the deployment listed on purpose. An unknown remote address
    /// counts as not allowed: a request that reached the process through an ingress, a service or a
    /// container bridge carries that hop's address, so it is refused even when the secret is right.
    /// </summary>
    public static bool IsAllowedCaller(HttpContext context, DebugAuthDecision decision)
    {
        var remote = context.Connection.RemoteIpAddress;
        if (remote is null)
            return false;

        if (remote.IsIPv4MappedToIPv6)
            remote = remote.MapToIPv4();

        if (IPAddress.IsLoopback(remote))
            return true;

        foreach (var network in decision.AllowedNetworks)
            if (network.Contains(remote))
                return true;

        return false;
    }

    /// <summary>A CIDR that does not parse is dropped, never widened into "allow everything".</summary>
    private static IReadOnlyList<IPNetwork> ParseNetworks(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
            return [];

        var networks = new List<IPNetwork>();
        foreach (var raw in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (IPNetwork.TryParse(raw, out var network))
                networks.Add(network);

        return networks;
    }
}

/// <param name="Enabled">Whether the Debug scheme may be registered and listed in a policy.</param>
/// <param name="Secret">The accepted secret, non-null exactly when <paramref name="Enabled"/>.</param>
/// <param name="Reason">Why, in words fit for a startup log line.</param>
/// <param name="AllowedNetworks">Networks beyond loopback that may authenticate this way.</param>
public sealed record DebugAuthDecision(
    bool Enabled,
    string? Secret,
    string Reason,
    IReadOnlyList<IPNetwork>? AllowedNetworks = null)
{
    public IReadOnlyList<IPNetwork> AllowedNetworks { get; } = AllowedNetworks ?? [];
}
