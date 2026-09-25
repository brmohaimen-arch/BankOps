using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BankOps.DbMigrator.Seeds;
using BankOps.IntegrationTests;
using BankOps.Modules.Catalog.Domain;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BankOps.Api.IntegrationTests;

// modules/catalog through apps/api end-to-end: real disposable Postgres, real OIDC tokens, real
// API host — same setup as SettingsAndAuditTests. Covers FR-101 (register), FR-102 (versioned
// publish, cycles rejected), RBAC, audit, and the synthetic seed data rendering through the API.
public class CatalogTests : IClassFixture<DevIdentityProviderFixture>, IAsyncLifetime
{
    private readonly DevIdentityProviderFixture _devIdp;
    private readonly DisposableDatabaseFixture _database = new();
    private WebApplicationFactory<Program>? _factory;
    private HttpClient _client = null!;

    public CatalogTests(DevIdentityProviderFixture devIdp) => _devIdp = devIdp;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();

        // See AuthTests/README.md: environment variables, not ConfigureAppConfiguration.
        Environment.SetEnvironmentVariable("Oidc__Authority", _devIdp.Authority);
        Environment.SetEnvironmentVariable("Secrets__BankOpsDbConnectionString", _database.ConnectionString);

        _factory = new WebApplicationFactory<Program>();
        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        _factory?.Dispose();
        await _database.DisposeAsync();
    }

    private async Task<HttpClient> AsAsync(string clientId, string clientSecret)
    {
        var token = await _devIdp.GetAccessTokenAsync(clientId, clientSecret);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client;
    }

    private Task<HttpClient> AdminAsync() => AsAsync("bankops-dev-admin", "dev-admin-secret");
    private Task<HttpClient> ViewerAsync() => AsAsync("bankops-dev-viewer", "dev-viewer-secret");

    private static async Task<ServiceDto> CreateAsync(HttpClient client, string code, string criticality = "HIGH")
    {
        var response = await client.PostAsJsonAsync("/api/v1/catalog/services", new
        {
            code, name = $"{code} service", criticality, environment = "PROD", ownerRef = "team:test",
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ServiceDto>())!;
    }

    private static Task<HttpResponseMessage> PublishAsync(HttpClient client, Guid serviceId, long expectedVersion, params Guid[] targets) =>
        client.PutAsJsonAsync($"/api/v1/catalog/services/{serviceId}/dependencies", new
        {
            expectedGraphVersion = expectedVersion,
            dependencies = targets.Select(t => new { targetKind = "SERVICE", targetId = t, relation = "DEPENDS_ON" }),
        });

    [Fact]
    public async Task RegisterPublishAndRead_DependenciesAndDependentsReflectCurrentVersion()
    {
        var admin = await AdminAsync();
        var a = await CreateAsync(admin, "SVC-A", "CRITICAL");
        var b = await CreateAsync(admin, "SVC-B");
        var c = await CreateAsync(admin, "SVC-C", "LOW");
        Assert.Equal(1, a.GraphVersion);

        Assert.Equal(HttpStatusCode.OK, (await PublishAsync(admin, a.Id, 1, b.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PublishAsync(admin, b.Id, 1, c.Id)).StatusCode);

        var bDetail = await admin.GetFromJsonAsync<ServiceDetailDto>($"/api/v1/catalog/services/{b.Id}");
        Assert.Equal(2, bDetail!.Service.GraphVersion);
        Assert.Equal("SVC-C", Assert.Single(bDetail.Dependencies).TargetCode);
        Assert.Equal("SVC-A", Assert.Single(bDetail.Dependents).Code);

        // Re-publishing A with a different set moves A's edge off B: B must stop listing A as a dependent.
        Assert.Equal(HttpStatusCode.OK, (await PublishAsync(admin, a.Id, 2, c.Id)).StatusCode);
        bDetail = await admin.GetFromJsonAsync<ServiceDetailDto>($"/api/v1/catalog/services/{b.Id}");
        Assert.Empty(bDetail!.Dependents);

        var list = await admin.GetFromJsonAsync<List<ServiceDto>>("/api/v1/catalog/services");
        Assert.Equal(["SVC-A", "SVC-B", "SVC-C"], list!.Select(s => s.Code)); // CRITICAL, HIGH, LOW order
    }

    [Fact]
    public async Task Publish_RejectsCycleStaleVersionUnknownTargetAndUnsupportedKind()
    {
        var admin = await AdminAsync();
        var a = await CreateAsync(admin, "SVC-A");
        var b = await CreateAsync(admin, "SVC-B");
        Assert.Equal(HttpStatusCode.OK, (await PublishAsync(admin, a.Id, 1, b.Id)).StatusCode);

        var cycle = await PublishAsync(admin, b.Id, 1, a.Id);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, cycle.StatusCode);
        var cycleBody = await cycle.Content.ReadFromJsonAsync<ErrorDto>();
        Assert.Equal("DEPENDENCY_CYCLE", cycleBody!.Code);
        Assert.Equal(["SVC-B", "SVC-A", "SVC-B"], cycleBody.Cycle);

        var stale = await PublishAsync(admin, a.Id, 1, b.Id); // A is at version 2 now
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        var unknown = await PublishAsync(admin, a.Id, 2, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, unknown.StatusCode);
        Assert.Equal("DEPENDENCY_TARGET_NOT_FOUND", (await unknown.Content.ReadFromJsonAsync<ErrorDto>())!.Code);

        var unsupported = await admin.PutAsJsonAsync($"/api/v1/catalog/services/{a.Id}/dependencies", new
        {
            expectedGraphVersion = 2,
            dependencies = new[] { new { targetKind = "DATABASE", targetId = Guid.NewGuid(), relation = "USES" } },
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, unsupported.StatusCode);
        Assert.Equal("TARGET_KIND_NOT_SUPPORTED_YET", (await unsupported.Content.ReadFromJsonAsync<ErrorDto>())!.Code);

        // None of the rejected publishes changed anything.
        var aDetail = await admin.GetFromJsonAsync<ServiceDetailDto>($"/api/v1/catalog/services/{a.Id}");
        Assert.Equal(2, aDetail!.Service.GraphVersion);
        Assert.Equal("SVC-B", Assert.Single(aDetail.Dependencies).TargetCode);
    }

    [Fact]
    public async Task ConcurrentPublishes_CannotTogetherCreateACycle()
    {
        // A -> B and B -> A are each fine alone; published at the same moment, only the graph-wide
        // advisory lock in CatalogRepository stops both from passing their cycle check.
        var admin = await AdminAsync();
        for (var round = 0; round < 10; round++)
        {
            var a = await CreateAsync(admin, $"RACE-A-{round}");
            var b = await CreateAsync(admin, $"RACE-B-{round}");

            var results = await Task.WhenAll(PublishAsync(admin, a.Id, 1, b.Id), PublishAsync(admin, b.Id, 1, a.Id));

            Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK);
            Assert.Single(results, r => r.StatusCode == HttpStatusCode.UnprocessableEntity);
        }
    }

    [Fact]
    public async Task Create_ValidatesFieldsAndRejectsDuplicateCode()
    {
        var admin = await AdminAsync();

        var invalid = await admin.PostAsJsonAsync("/api/v1/catalog/services", new
        {
            code = "bad code", name = "", criticality = "SEVERE", environment = "PROD",
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var body = await invalid.Content.ReadFromJsonAsync<ErrorDto>();
        Assert.Equal("VALIDATION_FAILED", body!.Code);
        Assert.Contains("code", body.Errors!.Keys);
        Assert.Contains("name", body.Errors.Keys);
        Assert.Contains("criticality", body.Errors.Keys);

        await CreateAsync(admin, "SVC-A");
        var duplicate = await admin.PostAsJsonAsync("/api/v1/catalog/services", new
        {
            code = "SVC-A", name = "Again", criticality = "LOW", environment = "PROD",
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task WritesAreAudited()
    {
        var admin = await AdminAsync();
        var a = await CreateAsync(admin, "SVC-A");
        var b = await CreateAsync(admin, "SVC-B");
        await PublishAsync(admin, a.Id, 1, b.Id);

        var audit = await admin.GetFromJsonAsync<List<AuditEntryDto>>("/api/v1/audit?resourceType=catalog.services");
        Assert.Equal(2, audit!.Count(e => e.Action == "catalog.service.create"));
        var publish = Assert.Single(audit!, e => e.Action == "catalog.dependencies.publish");
        Assert.Equal(a.Id, publish.ResourceId);
    }

    [Fact]
    public async Task ViewerCanReadButNotWrite()
    {
        var admin = await AdminAsync();
        var a = await CreateAsync(admin, "SVC-A");

        var viewer = await ViewerAsync();
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync("/api/v1/catalog/services")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync($"/api/v1/catalog/services/{a.Id}")).StatusCode);

        var create = await viewer.PostAsJsonAsync("/api/v1/catalog/services", new
        {
            code = "SVC-X", name = "x", criticality = "LOW", environment = "PROD",
        });
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await PublishAsync(viewer, a.Id, 1)).StatusCode);
    }

    [Fact]
    public async Task UnknownService_Returns404_AndAnonymousIsRejected()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/v1/catalog/services")).StatusCode);

        var admin = await AdminAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/v1/catalog/services/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task SyntheticSeed_IsIdempotentAcyclicAndServedByTheApi()
    {
        Assert.Equal(CatalogSyntheticData.ServiceCount, await CatalogSyntheticData.SeedAsync(_database.ConnectionString));
        Assert.Equal(0, await CatalogSyntheticData.SeedAsync(_database.ConnectionString)); // second run is a no-op

        var admin = await AdminAsync();
        var services = await admin.GetFromJsonAsync<List<ServiceDto>>("/api/v1/catalog/services");
        Assert.Equal(CatalogSyntheticData.ServiceCount, services!.Count);
        Assert.All(services, s => Assert.StartsWith("Synthetic", s.Site));

        var edges = new Dictionary<Guid, IReadOnlyCollection<Guid>>();
        var totalEdges = 0;
        foreach (var service in services)
        {
            var detail = await admin.GetFromJsonAsync<ServiceDetailDto>($"/api/v1/catalog/services/{service.Id}");
            edges[service.Id] = detail!.Dependencies.Select(d => d.TargetId).ToList();
            totalEdges += detail.Dependencies.Count;
        }

        Assert.Equal(CatalogSyntheticData.DependencyCount, totalEdges);
        foreach (var (serviceId, targets) in edges)
        {
            Assert.Null(DependencyGraph.FindCycle(edges, serviceId, targets));
        }

        // And the publish endpoint's cycle check holds against the seeded graph too.
        var core = services.Single(s => s.Code == "CORE-BANKING");
        var atm = services.Single(s => s.Code == "ATM-NETWORK");
        var cycle = await PublishAsync(admin, core.Id, core.GraphVersion, atm.Id);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, cycle.StatusCode);
    }

    [Fact]
    public async Task Navigation_ShowsCatalogToEveryoneAndAdminScreensOnlyToAdmins()
    {
        var viewer = await ViewerAsync();
        var viewerModules = (await viewer.GetFromJsonAsync<CapabilitiesDto>("/api/v1/me/capabilities"))!.Modules;
        Assert.Equal(["dashboard", "catalog"], viewerModules.Select(m => m.Id));

        var admin = await AdminAsync();
        var adminModules = (await admin.GetFromJsonAsync<CapabilitiesDto>("/api/v1/me/capabilities"))!.Modules;
        Assert.Equal(["dashboard", "catalog", "appearance", "audit"], adminModules.Select(m => m.Id));
    }

    private record ModuleDto(string Id, string Route);
    private record CapabilitiesDto(List<ModuleDto> Modules);
    private record ServiceDto(Guid Id, string Code, string Name, string Criticality, string? Site, long GraphVersion);
    private record DependencyDto(string TargetKind, Guid TargetId, string? TargetCode, string Relation, bool IsCritical);
    private record DependentDto(Guid ServiceId, string Code, string Relation);
    private record ServiceDetailDto(ServiceDto Service, List<DependencyDto> Dependencies, List<DependentDto> Dependents);
    private record ErrorDto(string Code, List<string>? Cycle, Dictionary<string, string[]>? Errors);
    private record AuditEntryDto(Guid Id, string Action, string ResourceType, Guid? ResourceId);
}
