using BimMep.Core.Geometry;

namespace BimMep.Core.Bim;

public sealed record ValidationIssue(string Code, string Message, ValidationSeverity Severity);

public enum ValidationSeverity { Info, Warning, Error }

public sealed class ValidationResult
{
    public List<ValidationIssue> Issues { get; } = new();
    public bool IsValid => !Issues.Any(i => i.Severity == ValidationSeverity.Error);

    public static ValidationResult Ok() => new();
}

/// <summary>
/// Cible du moteur de recalcul (cf. RecomputeScheduler) : tout objet dont la modification doit
/// se propager a des objets dependants (connecteurs, raccords, reseau) implemente cette interface.
/// Correspond au diagramme de sequence "propagation parametrique" (docs §5.4).
/// </summary>
public interface IRecomputable
{
    Guid Id { get; }
    bool IsDirty { get; }
    void MarkDirty();

    /// <summary>Identifiants des objets dont ce noeud depend geometriquement (doivent etre recalcules avant lui).</summary>
    IReadOnlyCollection<Guid> UpstreamDependencies { get; }

    /// <summary>Regenere la geometrie/etat derive de cet objet a partir de ses parametres courants.</summary>
    RecomputeOutcome Recompute();
}

public sealed record RecomputeOutcome(bool Changed, IReadOnlyCollection<Guid> DownstreamToNotify, string? Warning = null)
{
    public static RecomputeOutcome Unchanged() => new(false, Array.Empty<Guid>());
}

/// <summary>
/// Base commune de toutes les entites du modele BIM (architecturales et MEP). Porte le GUID IFC
/// stable (genere une seule fois, jamais regenere - cf. docs §5.1 et §6.4), les parametres
/// personnalisables, et la mecanique de recalcul en cascade.
/// </summary>
public abstract class BimElement : IRecomputable
{
    private readonly Dictionary<string, ParameterValue> _parameters = new();
    private readonly HashSet<Guid> _upstreamDependencies = new();
    private readonly HashSet<Guid> _downstreamDependents = new();

    protected BimElement(string name, FamilyType? familyType = null)
    {
        Id = Guid.NewGuid();
        IfcGuid = IfcGuidGenerator.NewGuid();
        Name = name;
        FamilyType = familyType;
        CreatedAt = DateTimeOffset.UtcNow;
        Lod = 100;
        RevisionNumber = 1;
        IsDirty = true;
    }

    public Guid Id { get; }
    public string IfcGuid { get; }
    public string Name { get; set; }
    public FamilyType? FamilyType { get; }
    public Transform3D Placement { get; set; } = Transform3D.Identity;
    public int Lod { get; set; }
    public int RevisionNumber { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public string? CreatedBy { get; set; }
    public bool IsDirty { get; private set; }

    public IReadOnlyCollection<Guid> UpstreamDependencies => _upstreamDependencies;
    public IReadOnlyCollection<Guid> DownstreamDependents => _downstreamDependents;

    /// <summary>Categorie IFC cible (§6.2) — chaque sous-classe MEP la precise (ex. "IfcDuctSegment").</summary>
    public abstract string GetIfcType();

    public void AddDependency(BimElement upstream) => _upstreamDependencies.Add(upstream.Id);
    public void RegisterDependent(BimElement downstream) => _downstreamDependents.Add(downstream.Id);

    public void MarkDirty() => IsDirty = true;

    /// <summary>
    /// Applique un nouveau parametre d'occurrence et marque l'element (et donc son reseau) a recalculer.
    /// Ne recalcule pas immediatement : c'est le role du RecomputeScheduler d'orchestrer l'ordre global
    /// (cf. docs §5.4 — evite les recalculs redondants sur un graphe de dependances partage).
    /// </summary>
    public void SetParameter(string key, object value)
    {
        if (_parameters.TryGetValue(key, out var existing))
        {
            existing.Value = value;
            existing.IsOverride = true;
        }
        else
        {
            _parameters[key] = new ParameterValue { Key = key, Value = value, IsOverride = true };
        }
        MarkDirty();
    }

    public ParameterValue? GetParameter(string key)
    {
        if (_parameters.TryGetValue(key, out var value)) return value;
        return FamilyType?.GetTypeParameter(key);
    }

    public double GetNumericParameter(string key, double fallback = 0.0) =>
        GetParameter(key)?.AsDouble() ?? fallback;

    /// <summary>
    /// Regenere la geometrie/etat derive. Les sous-classes MEP (MepDuct, MepPipe, ...) implementent
    /// la logique geometrique reelle ; cette methode de base incremente la revision et nettoie le flag dirty.
    /// </summary>
    public virtual RecomputeOutcome Recompute()
    {
        if (!IsDirty) return RecomputeOutcome.Unchanged();
        RevisionNumber++;
        IsDirty = false;
        return new RecomputeOutcome(Changed: true, DownstreamToNotify: DownstreamDependents);
    }

    public virtual ValidationResult Validate() => ValidationResult.Ok();
}

/// <summary>
/// Generateur de GlobalId IFC (base64 22 caracteres sur l'alphabet restreint IFC, cf. buildingSmart).
/// Genere une fois a la creation de l'element (docs §5.1) ; jamais recalculer.
/// </summary>
public static class IfcGuidGenerator
{
    private const string Alphabet =
        "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz_$";

    public static string NewGuid()
    {
        var bytes = Guid.NewGuid().ToByteArray(); // 16 octets = 128 bits
        var chars = new char[22];
        // Encodage compact façon IFC : decoupage en groupes de bits sur l'alphabet de 64 symboles.
        ulong hi = BitConverter.ToUInt64(bytes, 0);
        ulong lo = BitConverter.ToUInt64(bytes, 8);
        for (int i = 0; i < 11; i++)
        {
            chars[i] = Alphabet[(int)(hi & 0x3F)];
            hi >>= 6;
        }
        for (int i = 11; i < 22; i++)
        {
            chars[i] = Alphabet[(int)(lo & 0x3F)];
            lo >>= 6;
        }
        return new string(chars);
    }
}
