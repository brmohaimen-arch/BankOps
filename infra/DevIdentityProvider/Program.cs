using BankOps.DevIdentityProvider;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

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
        options.AllowClientCredentialsFlow();
        options.RegisterScopes("bankops.api");

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
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthorization();
app.MapControllers();

app.Run();
