using BimMep.Core.Geometry;

namespace BimMep.Core.Bim;

/// <summary>
/// Famille : categorie fonctionnelle + jeu de parametres declares (docs §5.3).
/// Ex. "Gaine rectangulaire", potentiellement liee a un fabricant.
/// </summary>
public sealed class Family
{
    public Guid Id { get; } = Guid.NewGuid();
    public required string Name { get; init; }
    public required string Category { get; init; }
    public Guid? ManufacturerId { get; init; }
    public List<ParameterDefinition> ParameterDefinitions { get; } = new();
    public List<FamilyType> Types { get; } = new();

    public FamilyType AddType(string name, IDictionary<string, object>? typeParameters = null)
    {
        var type = new FamilyType(this, name, typeParameters);
        Types.Add(type);
        return type;
    }
}

/// <summary>
/// Type : valeurs par defaut pour un sous-ensemble des parametres de la famille (docs §5.3).
/// Fabrique des occurrences via <see cref="FamilyType.CreateOccurrence"/> — le point d'entree
/// utilise par le copilote IA (docs §7.3, F-AI-01) pour instancier un equipement.
/// </summary>
public sealed class FamilyType
{
    private readonly Dictionary<string, ParameterValue> _typeParameters = new();

    internal FamilyType(Family family, string name, IDictionary<string, object>? typeParameters)
    {
        Id = Guid.NewGuid();
        ParentFamily = family;
        Name = name;
        if (typeParameters is not null)
        {
            foreach (var (key, value) in typeParameters)
                _typeParameters[key] = new ParameterValue { Key = key, Value = value };
        }
    }

    public Guid Id { get; }
    public Family ParentFamily { get; }
    public string Name { get; }

    public ParameterValue? GetTypeParameter(string key) =>
        _typeParameters.TryGetValue(key, out var v) ? v : null;

    public void SetTypeParameter(string key, object value) =>
        _typeParameters[key] = new ParameterValue { Key = key, Value = value };

    /// <summary>
    /// Cree une occurrence a partir d'une fabrique fournie par l'appelant (chaque categorie MEP
    /// connait sa propre construction — cf. Core.Mep). Le Type ne cree pas directement l'element
    /// concret pour eviter une dependance circulaire Core.Bim -> Core.Mep.
    /// </summary>
    public T CreateOccurrence<T>(Func<FamilyType, Transform3D, T> factory, Transform3D placement) where T : BimElement =>
        factory(this, placement);
}
