using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DataIsland.Identity;

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

        var token = tokenHeader.ToString();
        if (string.IsNullOrEmpty(token))
            return Task.FromResult(AuthenticateResult.Fail("Empty debug token"));

        var secret = serviceProvider.GetRequiredService<DebugAuthSecret>();
        if (token != secret.Secret)
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
}
