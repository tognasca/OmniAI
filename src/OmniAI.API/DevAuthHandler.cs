using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace OmniAI.API
{
    // DevAuthHandler.cs
    public class DevAuthHandler(
     IOptionsMonitor<AuthenticationSchemeOptions> options,
     ILoggerFactory logger,
     UrlEncoder encoder)
     : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new[]
            {
            new Claim(ClaimTypes.NameIdentifier, "dev-user-id"),
            new Claim(ClaimTypes.Name,           "Dev User"),
            new Claim(ClaimTypes.Email,          "dev@omni.local"),
            new Claim(ClaimTypes.Role,           "Admin"),
        };
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

}
