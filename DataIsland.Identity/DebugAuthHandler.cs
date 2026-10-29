using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DataIsland.Identity;

/// <summary>
/// Local-development authentication: the caller presents a shared secret and CHOOSES the subject,
/// e-mail and admin role it gets. Reaching this handler is equivalent to picking a user, so it is
/// only ever registered behind <see cref="DebugAuthGate"/>, and it re-checks the caller itself.
/// </summary>
public class DebugAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IServiceProvider serviceProvider)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Debug";
    public const string HeaderName = "X-Debug-Token";
    public const string UserIdHeaderName = "X-Debug-UserId";
    public const string DefaultUserId = "000000000000000000000001";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var tokenHeader))
            return Task.FromResult(AuthenticateResult.NoResult());

        var decision = serviceProvider.GetRequiredService<DebugAuthDecision>();

        // Second, independent gate: the registration decided that debug auth MAY exist in this
        // process, this decides that THIS caller may use it. A reachable Development instance with
        // a real secret would otherwise hand an administrator principal to anyone who can open its
        // port.
        if (!DebugAuthGate.IsAllowedCaller(Context, decision))
        {
            Logger.LogWarning(
                "Debug authentication refused for caller {RemoteAddress}: not loopback and not in {Key}",
                Context.Connection.RemoteIpAddress, DebugAuthGate.AllowedNetworksKey);
            return Task.FromResult(AuthenticateResult.Fail("Debug token is not allowed from this address"));
        }

        var token = tokenHeader.ToString();
        if (string.IsNullOrEmpty(token))
            return Task.FromResult(AuthenticateResult.Fail("Empty debug token"));

        if (decision.Secret is null || !FixedTimeEquals(token, decision.Secret))
            return Task.FromResult(AuthenticateResult.Fail("Invalid debug token"));

        var userId = Request.Headers.TryGetValue(UserIdHeaderName, out var userIdHeader)
            ? userIdHeader.ToString()
            : DefaultUserId;

        var email = Request.Headers.TryGetValue("X-Debug-Email", out var emailHeader)
            ? emailHeader.ToString()
            : "debug@ni.solutions";

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, "debug-user"),
            new Claim(ClaimTypes.Email, email),
            new Claim("email", email),
            new Claim(ClaimTypes.Role, "admin")
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    /// <summary>
    /// Compares in time independent of how much of the secret matched: a plain <c>!=</c> on a
    /// shared secret leaks its prefix to anyone who can measure the response.
    /// </summary>
    private static bool FixedTimeEquals(string token, string secret) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(token), Encoding.UTF8.GetBytes(secret));
}
