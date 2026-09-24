using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Dev-only issuer (infra/DevIdentityProvider) until D-01's actual IdP (Bank AD/Entra) is wired
// up in a later phase. Authority/audience below are placeholders for that swap, not production
// config — see infra/DevIdentityProvider/README.md.
var oidcAuthority = builder.Configuration["Oidc:Authority"] ?? "http://localhost:5026/";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = oidcAuthority;
        options.RequireHttpsMetadata = false; // dev-only issuer runs on plain HTTP
        // Without this, Microsoft.IdentityModel silently rewrites short claim types (e.g. "role")
        // to legacy WS-Identity URIs, which then don't match RoleClaimType below and every
        // [Authorize(Roles = ...)] check quietly fails with 403. Keep claims exactly as issued.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = oidcAuthority,
            ValidateAudience = false, // dev IdP doesn't set an audience claim yet
            ValidateLifetime = true,
            RoleClaimType = "role",
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
