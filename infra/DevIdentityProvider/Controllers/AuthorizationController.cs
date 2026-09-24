using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace BankOps.DevIdentityProvider.Controllers;

// The interactive counterpart to TokenController's client_credentials flow. Real users come from
// D-01's actual IdP eventually (Bank AD/Entra, authorization_code + PKCE) — this exists so
// apps/web has a genuine browser login round-trip to build and test against now. Consent is
// skipped entirely: every client registered here is a trusted first-party dev tool, not a
// third-party app a real user would need to approve data sharing with.
[ApiController]
public class AuthorizationController : ControllerBase
{
    [HttpGet("~/connect/authorize")]
    [HttpPost("~/connect/authorize")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Authorize()
    {
        var request = HttpContext.GetOpenIddictServerRequest() ??
            throw new InvalidOperationException("The OpenIddict request cannot be retrieved.");

        var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (!result.Succeeded || result.Principal is null)
        {
            // A relative path, not Request.GetEncodedUrl()'s absolute URL — LocalRedirect (used
            // once the user actually logs in) rejects absolute URLs even for the same host, by
            // design, to close off open-redirect attacks. Keep the whole round-trip relative.
            return Challenge(
                authenticationSchemes: CookieAuthenticationDefaults.AuthenticationScheme,
                properties: new AuthenticationProperties
                {
                    RedirectUri = Request.Path + Request.QueryString,
                });
        }

        var identity = new ClaimsIdentity(
            authenticationType: "OpenIddict",
            nameType: Claims.Name,
            roleType: Claims.Role);

        identity.SetClaim(Claims.Subject, result.Principal.FindFirstValue(ClaimTypes.Email));
        identity.SetClaim(Claims.Name, result.Principal.FindFirstValue("name"));
        identity.SetClaim(Claims.Role, result.Principal.FindFirstValue(ClaimTypes.Role));
        identity.SetScopes(request.GetScopes());
        identity.SetDestinations(_ => [Destinations.AccessToken, Destinations.IdentityToken]);

        return SignIn(new ClaimsPrincipal(identity), OpenIddict.Server.AspNetCore.OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [HttpGet("~/login")]
    [AllowAnonymous]
    public IActionResult Login([FromQuery] string? returnUrl) =>
        Content(LoginPage(returnUrl, error: null), "text/html");

    [HttpPost("~/login")]
    [AllowAnonymous]
    public async Task<IActionResult> LoginSubmit(
        [FromForm] string email, [FromForm] string password, [FromForm] string? returnUrl)
    {
        var user = DevUserStore.Validate(email, password);
        if (user is null)
        {
            return Content(LoginPage(returnUrl, error: "Invalid email or password."), "text/html");
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Email, user.Email),
            new("name", user.DisplayName),
            new(ClaimTypes.Role, user.Role),
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

        return LocalRedirect(string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl);
    }

    [HttpGet("~/logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Content("Signed out.", "text/plain");
    }

    private static string LoginPage(string? returnUrl, string? error)
    {
        var encodedReturnUrl = WebUtility.HtmlEncode(returnUrl ?? "/");
        var errorHtml = error is null ? "" : $"<p style=\"color:red\">{WebUtility.HtmlEncode(error)}</p>";
        return $"""
            <!DOCTYPE html>
            <html><body>
              <h1>BankOps dev sign-in</h1>
              <p>Dev-only users: admin@bankops.dev / admin123, viewer@bankops.dev / viewer123</p>
              {errorHtml}
              <form method="post" action="/login">
                <input type="hidden" name="returnUrl" value="{encodedReturnUrl}" />
                <label>Email <input type="email" name="email" required /></label><br/>
                <label>Password <input type="password" name="password" required /></label><br/>
                <button type="submit">Sign in</button>
              </form>
            </body></html>
            """;
    }
}
