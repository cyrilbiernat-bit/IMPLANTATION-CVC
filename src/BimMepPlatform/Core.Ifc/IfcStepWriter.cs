using System.Collections;
using System.Globalization;
using System.Text;

namespace BimMep.Core.Ifc;

/// <summary>Reference vers une instance STEP deja ecrite (serialisee "#123").</summary>
public readonly record struct IfcRef(int Id);

/// <summary>Valeur d'enumeration EXPRESS (serialisee ".VALEUR.").</summary>
public readonly record struct IfcEnum(string Value);

/// <summary>Attribut derive, non applicable ici (serialise "*") — rarement necessaire pour un export.</summary>
public sealed class IfcDerived
{
    public static readonly IfcDerived Instance = new();
    private IfcDerived() { }
}

/// <summary>
/// Type EXPRESS "defini" (defined type) tel que IFCREAL/IFCLABEL/IFCTEXT/IFCBOOLEAN, utilise pour
/// typer explicitement une valeur (ex. IfcPropertySingleValue.NominalValue : IfcValue). Se serialise
/// en ligne, jamais comme instance numerotee separee (contrairement a IfcRef).
/// </summary>
public readonly record struct IfcTypedLiteral(string TypeName, object Value);

/// <summary>
/// Ecrivain STEP (ISO-10303-21) bas niveau : gere la numerotation des instances et le formatage des
/// valeurs selon la grammaire EXPRESS (docs §15.7 — l'export "rapide" natif C#, en complement du
/// pipeline IfcOpenShell/Python pour l'import robuste de fichiers tiers, docs §6.4).
///
/// Ne connait rien du schema IFC4 lui-meme (aucune entite codee en dur ici) : c'est
/// <see cref="IfcProjectExporter"/> qui sait quels attributs porte IfcProject, IfcDuctSegment, etc.
/// Cette separation permet de reutiliser l'ecrivain pour n'importe quelle version de schema EXPRESS.
/// </summary>
public sealed class IfcStepWriter
{
    private readonly List<string> _dataLines = new();
    private int _nextId = 1;

    /// <summary>Ecrit une nouvelle instance STEP (ex. IFCDUCTSEGMENT(...)) et retourne sa reference.</summary>
    public IfcRef Write(string ifcType, params object?[] attributes)
    {
        int id = _nextId++;
        _dataLines.Add($"#{id}={ifcType.ToUpperInvariant()}({string.Join(",", attributes.Select(FormatValue))});");
        return new IfcRef(id);
    }

    public string BuildDocument(string fileDescription, string fileName, string schemaName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("ISO-10303-21;");
        sb.AppendLine("HEADER;");
        sb.AppendLine($"FILE_DESCRIPTION({FormatValue(new object?[] { fileDescription })},'2;1');");
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
        sb.AppendLine($"FILE_NAME({FormatValue(fileName)},{FormatValue(timestamp)}," +
                       $"{FormatValue(new object?[] { "BimMepPlatform" })},{FormatValue(new object?[] { "BimMepPlatform" })}," +
                       $"{FormatValue("BimMepPlatform Core.Ifc exporter")},{FormatValue("BimMepPlatform")},{FormatValue("")});");
        sb.AppendLine($"FILE_SCHEMA({FormatValue(new object?[] { schemaName })});");
        sb.AppendLine("ENDSEC;");
        sb.AppendLine("DATA;");
        foreach (var line in _dataLines)
            sb.AppendLine(line);
        sb.AppendLine("ENDSEC;");
        sb.Append("END-ISO-10303-21;");
        return sb.ToString();
    }

    public static string FormatValue(object? value) => value switch
    {
        null => "$",
        IfcDerived => "*",
        IfcRef r => $"#{r.Id}",
        IfcEnum e => $".{e.Value}.",
        IfcTypedLiteral t => $"{t.TypeName.ToUpperInvariant()}({FormatValue(t.Value)})",
        bool b => b ? ".T." : ".F.",
        string s => $"'{EscapeString(s)}'",
        double d => FormatReal(d),
        float f => FormatReal(f),
        int i => i.ToString(CultureInfo.InvariantCulture),
        // Une liste EXPRESS (ex. les points d'une polyligne, les Psets d'un IfcRelDefinesByProperties) :
        // exclut `string`, qui implemente aussi IEnumerable mais doit rester une chaine STEP.
        IEnumerable list => $"({string.Join(",", list.Cast<object?>().Select(FormatValue))})",
        _ => throw new NotSupportedException($"Type non pris en charge pour la serialisation STEP : {value.GetType()}")
    };

    private static string EscapeString(string s) => s.Replace("'", "''");

    /// <summary>
    /// La grammaire EXPRESS exige un point decimal explicite dans la mantisse d'un REAL, meme en
    /// notation scientifique (ex. "1.E-05" et non "1E-05" — ce dernier a fait echouer le moteur
    /// geometrique d'IfcOpenShell/OCCT lors de la validation, cf. docs §13-modules-critiques.md).
    /// </summary>
    private static string FormatReal(double d)
    {
        string s = d.ToString("R", CultureInfo.InvariantCulture);
        int expIndex = s.IndexOfAny(new[] { 'E', 'e' });
        string mantissa = expIndex >= 0 ? s[..expIndex] : s;
        string exponent = expIndex >= 0 ? s[expIndex..].Replace("E", "e") : "";
        if (!mantissa.Contains('.'))
            mantissa += ".";
        return mantissa + exponent;
    }
}
