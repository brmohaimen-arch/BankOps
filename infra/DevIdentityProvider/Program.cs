using BankOps.DevIdentityProvider;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// The login-session scheme for the interactive /login page — separate from the tokens OpenIddict
// itself issues. "Are you logged in" (cookie) and "here is a token proving who you are" (OIDC) are
// different concerns even though one dev tool happens to handle both here.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options => options.LoginPath = "/login");
builder.Services.AddAuthorization();

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseInMemoryDatabase("bankops-dev-idp");
    options.UseOpenIddict();
});

builder.Services.AddOpenIddict()
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore().UseDbContext<AppDbContext>();
    })
    .AddServer(options =>
    {
        options.SetTokenEndpointUris("connect/token");
        options.SetAuthorizationEndpointUris("connect/authorize");
        options.AllowClientCredentialsFlow();
        options.AllowAuthorizationCodeFlow().RequireProofKeyForCodeExchange();
        options.RegisterScopes("bankops.api", Scopes.OpenId, Scopes.Profile);

        // Plain signed JWTs, not encrypted JWE — closer to what D-01's actual IdP (Entra) issues,
        // and lets apps/api validate with standard AddJwtBearer() against this issuer's JWKS
        // instead of needing OpenIddict's own validation stack just for a dev-only token shape.
        options.DisableAccessTokenEncryption();

        // Dev-only: ephemeral signing/encryption keys regenerated on every restart.
        // A real deployment (and D-01's actual IdP) would never do this.
        options.AddDevelopmentEncryptionCertificate();
        options.AddDevelopmentSigningCertificate();

        options.UseAspNetCore()
            .EnableTokenEndpointPassthrough()
            .EnableAuthorizationEndpointPassthrough()
            .DisableTransportSecurityRequirement(); // plain HTTP is fine for a localhost dev tool
    });

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await context.Database.EnsureCreatedAsync();

    var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();

    async Task EnsureClientAsync(string clientId, string secret)
    {
        if (await manager.FindByClientIdAsync(clientId) is not null)
        {
            return;
        }

        await manager.CreateAsync(new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            ClientSecret = secret,
            DisplayName = clientId,
            Permissions =
            {
                Permissions.Endpoints.Token,
                Permissions.GrantTypes.ClientCredentials,
                Permissions.Prefixes.Scope + "bankops.api"
            }
        });
    }

    // Dev-only credentials, not secrets worth protecting — this issuer only exists on localhost
    // and is rebuilt from scratch on every restart (in-memory store).
    await EnsureClientAsync("bankops-dev-admin", "dev-admin-secret");
    await EnsureClientAsync("bankops-dev-viewer", "dev-viewer-secret");

    // apps/web — a public SPA client, no secret (PKCE substitutes for one), for the real browser
    // login round-trip. Vite's default dev port; change here if apps/web pins a different one.
    if (await manager.FindByClientIdAsync("bankops-web") is null)
    {
        await manager.CreateAsync(new OpenIddictApplicationDescriptor
        {
            ClientId = "bankops-web",
            ClientType = ClientTypes.Public,
            DisplayName = "BankOps web (dev)",
            RedirectUris = { new Uri("http://localhost:5173/callback") },
            PostLogoutRedirectUris = { new Uri("http://localhost:5173/") },
            Permissions =
            {
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.Token,
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.ResponseTypes.Code,
                Permissions.Prefixes.Scope + "bankops.api",
                Permissions.Scopes.Profile,
            },
            Requirements = { Requirements.Features.ProofKeyForCodeExchange },
        });
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
