using System.Security.Claims;
using Microsoft.AspNetCore; // GetOpenIddictServerRequest() lives here, not OpenIddict.Server.AspNetCore
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace BankOps.DevIdentityProvider.Controllers;

[ApiController]
public class TokenController : ControllerBase
{
    private readonly IOpenIddictApplicationManager _applicationManager;

    public TokenController(IOpenIddictApplicationManager applicationManager) =>
        _applicationManager = applicationManager;

    [HttpPost("~/connect/token"), IgnoreAntiforgeryToken, AllowAnonymous]
    public async Task<IActionResult> Exchange()
    {
        var request = HttpContext.GetOpenIddictServerRequest() ??
            throw new InvalidOperationException("The OpenIddict request cannot be retrieved.");

        if (!request.IsClientCredentialsGrantType())
        {
            return Forbid(
                authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                properties: new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.UnsupportedGrantType,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                        "This dev token endpoint only supports the client_credentials grant."
                }));
        }

        var application = await _applicationManager.FindByClientIdAsync(request.ClientId!) ??
            throw new InvalidOperationException("The application details cannot be found.");

        var identity = new ClaimsIdentity(
            authenticationType: "OpenIddict",
            nameType: Claims.Name,
            roleType: Claims.Role);

        identity.SetClaim(Claims.Subject, await _applicationManager.GetClientIdAsync(application));
        identity.SetClaim(Claims.Name, await _applicationManager.GetDisplayNameAsync(application));

        // Dev-only role mapping: which client_id you authenticate as decides your role claim.
        // Real role/permission resolution belongs to modules/identity once it exists —
        // this is just enough to let apps/api build and test RBAC against a real bearer token.
        var clientId = await _applicationManager.GetClientIdAsync(application);
        var role = clientId switch
        {
            "bankops-dev-admin" => "admin",
            "bankops-dev-viewer" => "viewer",
            _ => "viewer"
        };
        identity.SetClaim(Claims.Role, role);

        identity.SetScopes(request.GetScopes());
        identity.SetDestinations(_ => [Destinations.AccessToken]);

        return SignIn(new ClaimsPrincipal(identity), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }
}
