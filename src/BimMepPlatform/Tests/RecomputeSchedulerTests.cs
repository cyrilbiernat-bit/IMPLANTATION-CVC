using BimMep.Core.Bim;
using Xunit;

namespace BimMep.Tests;

/// <summary>Element de test minimal qui journalise l'ordre de ses appels a Recompute().</summary>
internal sealed class RecordingElement : BimElement
{
    private readonly int _id;
    public List<int> CallOrder { get; }

    public RecordingElement(int id, List<int> callOrder) : base($"Element{id}")
    {
        _id = id;
        CallOrder = callOrder;
    }

    public override string GetIfcType() => "IfcTestElement";

    public override RecomputeOutcome Recompute()
    {
        var outcome = base.Recompute();
        if (outcome.Changed) CallOrder.Add(_id);
        return outcome;
    }
}

public class RecomputeSchedulerTests
{
    [Fact]
    public void RunFrom_PropagatesInTopologicalOrder_AndRecomputesEachElementOnce()
    {
        var callOrder = new List<int>();
        var a = new RecordingElement(1, callOrder);
        var b = new RecordingElement(2, callOrder);
        var c = new RecordingElement(3, callOrder);

        // b depend de a, c depend de b (chaine lineaire A -> B -> C, docs §5.4)
        b.AddDependency(a);
        a.RegisterDependent(b);
        c.AddDependency(b);
        b.RegisterDependent(c);

        var all = new Dictionary<Guid, IRecomputable> { [a.Id] = a, [b.Id] = b, [c.Id] = c };
        var scheduler = new RecomputeScheduler();

        // Etat initial : purge les flags "dirty" poses par les constructeurs.
        scheduler.RunFrom(new IRecomputable[] { a }, all);
        callOrder.Clear();

        a.MarkDirty();
        var report = scheduler.RunFrom(new IRecomputable[] { a }, all);

        Assert.Equal(new[] { 1, 2, 3 }, callOrder);
        Assert.Equal(3, report.RecomputedInOrder.Count);
    }

    [Fact]
    public void RunFrom_UnrelatedElement_IsNotRecomputed()
    {
        var callOrder = new List<int>();
        var a = new RecordingElement(1, callOrder);
        var unrelated = new RecordingElement(2, callOrder);
        var all = new Dictionary<Guid, IRecomputable> { [a.Id] = a, [unrelated.Id] = unrelated };
        var scheduler = new RecomputeScheduler();

        scheduler.RunFrom(new IRecomputable[] { a, unrelated }, all);
        callOrder.Clear();

        a.MarkDirty();
        scheduler.RunFrom(new IRecomputable[] { a }, all);

        Assert.Equal(new[] { 1 }, callOrder);
    }

    [Fact]
    public void RunFrom_CircularDependency_ThrowsCircularDependencyException()
    {
        var callOrder = new List<int>();
        var a = new RecordingElement(1, callOrder);
        var b = new RecordingElement(2, callOrder);

        // Cycle volontaire : a depend de b ET b depend de a.
        a.AddDependency(b);
        b.RegisterDependent(a);
        b.AddDependency(a);
        a.RegisterDependent(b);

        var all = new Dictionary<Guid, IRecomputable> { [a.Id] = a, [b.Id] = b };
        var scheduler = new RecomputeScheduler();

        Assert.Throws<CircularDependencyException>(() => scheduler.RunFrom(new IRecomputable[] { a }, all));
    }
}
