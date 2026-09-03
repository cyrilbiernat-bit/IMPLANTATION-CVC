namespace BimMep.Core.Bim;

/// <summary>Type physique d'un parametre BIM. Cf. docs/bim-mep-platform/05-schema-bim.md §5.5.</summary>
public enum ParameterType
{
    Length,
    Area,
    Volume,
    FlowRate,
    Pressure,
    Velocity,
    Text,
    Number,
    Boolean,
    Enum,
    Reference
}

/// <summary>
/// Declaration d'un parametre au niveau d'une Famille : porte l'unite, indique s'il est saisi
/// au niveau Type ou Occurrence, et s'il est calcule (donc jamais editable manuellement).
/// </summary>
public sealed record ParameterDefinition(
    string Key,
    ParameterType Type,
    string Unit,
    bool IsTypeParameter,
    bool IsReadOnly,
    object? DefaultValue = null);

/// <summary>Valeur effective d'un parametre porte par un Type ou une Occurrence.</summary>
public sealed class ParameterValue
{
    public required string Key { get; init; }
    public required object Value { get; set; }
    public bool IsOverride { get; set; }

    public double AsDouble() => Value switch
    {
        double d => d,
        int i => i,
        _ => throw new InvalidOperationException($"Le parametre '{Key}' n'est pas numerique.")
    };
}
