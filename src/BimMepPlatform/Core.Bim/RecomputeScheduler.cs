namespace BimMep.Core.Bim;

public sealed class CircularDependencyException : Exception
{
    public IReadOnlyList<Guid> Cycle { get; }

    public CircularDependencyException(IReadOnlyList<Guid> cycle)
        : base("Cycle de dependance geometrique detecte — impossible de recalculer le modele en l'etat.")
    {
        Cycle = cycle;
    }
}

public sealed record RecomputeReport(
    IReadOnlyList<Guid> RecomputedInOrder,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Moteur de recalcul en cascade du modele BIM (docs §5.4 — "Propagation parametrique").
///
/// Principe : modifier un parametre marque l'element "dirty" (BimElement.SetParameter). Ce
/// scheduler recoit l'ensemble des elements potentiellement impactes, construit l'ordre topologique
/// de leurs dependances (un raccord doit se recalculer APRES la gaine qui vient de changer de
/// section, un reseau doit se recalculer APRES tous ses membres), puis appelle Recompute() une seule
/// fois par element dans cet ordre — jamais deux fois, jamais dans le desordre. C'est l'equivalent du
/// "regeneration cycle" de Revit, implemente ici en in-process, sans aucun appel reseau.
///
/// Un cycle de dependance (A depend de B qui depend de A) est une erreur de modelisation : elle est
/// detectee et remontee explicitement (CircularDependencyException) plutot que resolue silencieusement
/// par un ordre arbitraire, pour eviter des recalculs incoherents non reproductibles.
/// </summary>
public sealed class RecomputeScheduler
{
    /// <summary>
    /// Recalcule <paramref name="seed"/> puis, en cascade, tous les elements en aval qui en dependent,
    /// dans l'ordre topologique. <paramref name="allElements"/> est l'univers de resolution (le reseau,
    /// ou le projet entier pour un recalcul complet).
    /// </summary>
    public RecomputeReport RunFrom(IEnumerable<IRecomputable> seed, IReadOnlyDictionary<Guid, IRecomputable> allElements)
    {
        var toVisit = new Queue<Guid>(seed.Select(s => s.Id));
        var impacted = new HashSet<Guid>(toVisit);

        // 1) Etendre l'ensemble impacte a toute la fermeture transitive aval (BFS sur les dependants).
        while (toVisit.Count > 0)
        {
            var currentId = toVisit.Dequeue();
            if (!allElements.TryGetValue(currentId, out var current)) continue;

            foreach (var downstreamId in FindDownstream(current))
            {
                if (impacted.Add(downstreamId))
                    toVisit.Enqueue(downstreamId);
            }
        }

        // 2) Tri topologique de l'ensemble impacte (Kahn) restreint aux dependances internes a l'ensemble.
        var order = TopologicalSort(impacted, allElements);

        // Marquer "dirty" tout l'ensemble impacte : un element en aval doit se regenerer meme s'il
        // n'a subi aucune modification directe, puisque c'est un objet dont il depend qui a change
        // (docs §5.4). Sans cette etape, BimElement.Recompute() court-circuiterait silencieusement
        // les elements non explicitement modifies (IsDirty=false) et la cascade n'aurait aucun effet.
        foreach (var id in impacted)
        {
            if (allElements.TryGetValue(id, out var element))
                element.MarkDirty();
        }

        // 3) Execution dans l'ordre, une seule fois chacun.
        var warnings = new List<string>();
        foreach (var id in order)
        {
            if (!allElements.TryGetValue(id, out var element)) continue;
            var outcome = element.Recompute();
            if (outcome.Warning is not null)
                warnings.Add($"{id}: {outcome.Warning}");
        }

        return new RecomputeReport(order, warnings);
    }

    private static IEnumerable<Guid> FindDownstream(IRecomputable element) =>
        element is BimElement be ? be.DownstreamDependents : Array.Empty<Guid>();

    private static List<Guid> TopologicalSort(HashSet<Guid> impacted, IReadOnlyDictionary<Guid, IRecomputable> all)
    {
        var inDegree = impacted.ToDictionary(id => id, _ => 0);
        var edges = new Dictionary<Guid, List<Guid>>();

        foreach (var id in impacted)
        {
            if (!all.TryGetValue(id, out var element)) continue;
            foreach (var upstreamId in element.UpstreamDependencies.Where(impacted.Contains))
            {
                edges.TryAdd(upstreamId, new List<Guid>());
                edges[upstreamId].Add(id);
                inDegree[id]++;
            }
        }

        var ready = new Queue<Guid>(impacted.Where(id => inDegree[id] == 0));
        var order = new List<Guid>();

        while (ready.Count > 0)
        {
            var id = ready.Dequeue();
            order.Add(id);
            if (!edges.TryGetValue(id, out var downstream)) continue;
            foreach (var next in downstream)
            {
                if (--inDegree[next] == 0)
                    ready.Enqueue(next);
            }
        }

        if (order.Count != impacted.Count)
        {
            var cycle = impacted.Except(order).ToList();
            throw new CircularDependencyException(cycle);
        }

        return order;
    }
}
