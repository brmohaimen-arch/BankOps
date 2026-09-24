using BankOps.Api;
using BankOps.Api.FeatureFlags;
using BankOps.Api.Secrets;
using BankOps.Contracts;
using BankOps.Modules.Audit.Contracts;
using BankOps.Modules.Audit.Infrastructure;
using BankOps.Modules.Settings.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentPrincipal, HttpContextCurrentPrincipal>();

// Read directly from configuration here rather than through ISecretResolver: the DI container
// that would resolve ISecretResolver doesn't exist yet at this point in startup, and this is
// functionally identical to what ConfigurationSecretResolver does (user-secrets in Development,
// never appsettings.json).
var dbConnectionString = builder.Configuration["Secrets:BankOpsDbConnectionString"]
    ?? throw new InvalidOperationException(
        "Missing Secrets:BankOpsDbConnectionString. For local dev: " +
        "dotnet user-secrets set \"Secrets:BankOpsDbConnectionString\" \"<connection string>\"");
builder.Services.AddSingleton(NpgsqlDataSource.Create(dbConnectionString));

builder.Services.AddScoped<IAuditWriter, PostgresAuditWriter>();
builder.Services.AddScoped<SettingsRepository>();

// NFR-OBS-01: "Structured logs, metrics and traces share request/incident/correlation IDs."
// Console exporter only for now — D-04 (which telemetry backend to actually reuse) is still an
// open Phase 0 decision; swapping to a real backend later is an exporter change here, not a
// call-site change anywhere else.
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("BankOps.Api"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddConsoleExporter())
    .WithLogging(logging => logging.AddConsoleExporter());

builder.Services.AddSingleton<IFeatureFlagService, ConfigurationFeatureFlagService>();
builder.Services.AddSingleton<ISecretResolver, ConfigurationSecretResolver>();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

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

// Dev-only: apps/web (Vite, localhost:5173) is a different origin than apps/api. A real
// deployment would likely serve both from the same origin (no CORS needed) or from an approved
// bank domain list — this stays narrow to the one dev origin, not a wildcard, even for a dev tool.
const string DevWebOrigin = "http://localhost:5173";
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(DevWebOrigin).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

var app = builder.Build();

// GlobalExceptionHandler must be first — it's the last-resort catch for anything below it.
app.UseExceptionHandler();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseMiddleware<CorrelationIdMiddleware>();

app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Makes Program reachable from WebApplicationFactory<Program> in tests/BankOps.Api.IntegrationTests
// — top-level statements otherwise generate an internal Program class invisible to another project.
public partial class Program;
