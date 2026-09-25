using BankOps.Modules.Catalog.Domain;

namespace BankOps.Api.IntegrationTests;

// Pure unit tests for the cycle check behind FR-102 — no database, no host. Lives here rather than
// in a separate unit-test project only because this is the project CI already runs for apps/api.
public class DependencyGraphTests
{
    private static readonly Guid A = Guid.NewGuid(), B = Guid.NewGuid(), C = Guid.NewGuid(), D = Guid.NewGuid();

    private static Dictionary<Guid, IReadOnlyCollection<Guid>> Edges(params (Guid From, Guid To)[] edges) =>
        edges.GroupBy(e => e.From).ToDictionary(g => g.Key, g => (IReadOnlyCollection<Guid>)g.Select(e => e.To).ToList());

    [Fact]
    public void AcyclicChain_HasNoCycle()
    {
        var current = Edges((B, C), (C, D));
        Assert.Null(DependencyGraph.FindCycle(current, A, [B]));
    }

    [Fact]
    public void ClosingALoop_ReturnsThePath()
    {
        var current = Edges((B, C), (C, A));
        Assert.Equal([A, B, C, A], DependencyGraph.FindCycle(current, A, [B]));
    }

    [Fact]
    public void SelfDependency_IsACycle()
    {
        Assert.Equal([A, A], DependencyGraph.FindCycle(Edges(), A, [A]));
    }

    [Fact]
    public void DiamondSharedDependency_IsNotACycle()
    {
        // A -> B -> D and A -> C -> D: D reached twice, but no loop.
        var current = Edges((B, D), (C, D));
        Assert.Null(DependencyGraph.FindCycle(current, A, [B, C]));
    }

    [Fact]
    public void ReplacingEdges_IgnoresTheServicesOldEdges()
    {
        // A currently depends on B, and B -> A would be a loop — but the proposed set for A drops B.
        var current = Edges((A, B), (B, A));
        Assert.Null(DependencyGraph.FindCycle(current, A, [C]));
    }
}
