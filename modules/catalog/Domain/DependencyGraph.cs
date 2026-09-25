namespace BankOps.Modules.Catalog.Domain;

// FR-102: "reject or flag cyclic/invalid dependencies at publish." Pure graph logic, no I/O — the
// repository loads the current published service-to-service edges and asks this whether replacing
// one service's outgoing edges would close a loop.
public static class DependencyGraph
{
    // Returns the cycle as a path of service ids starting and ending at `serviceId`
    // (e.g. [A, B, C, A]), or null when the proposed edges keep the graph acyclic.
    public static IReadOnlyList<Guid>? FindCycle(
        IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>> currentEdges,
        Guid serviceId,
        IReadOnlyCollection<Guid> proposedTargets)
    {
        // Any cycle introduced by this publish must pass through serviceId, since only its outgoing
        // edges change. So it's enough to search from each proposed target for a path back to it.
        IReadOnlyCollection<Guid> Next(Guid node) =>
            node == serviceId
                ? proposedTargets
                : currentEdges.TryGetValue(node, out var targets) ? targets : [];

        var visited = new HashSet<Guid>();
        var path = new List<Guid> { serviceId };

        bool Search(Guid node)
        {
            if (node == serviceId)
            {
                return true;
            }

            if (!visited.Add(node))
            {
                return false;
            }

            path.Add(node);
            foreach (var next in Next(node))
            {
                if (Search(next))
                {
                    return true;
                }
            }

            path.RemoveAt(path.Count - 1);
            return false;
        }

        foreach (var target in proposedTargets)
        {
            if (Search(target))
            {
                path.Add(serviceId);
                return path;
            }
        }

        return null;
    }
}
