namespace BimMep.Core.Takeoff;

/// <summary>
/// Ligne de nomenclature agregee (docs F-TAKEOFF-01/02) : regroupe tous les elements partageant la
/// meme categorie/dimension/systeme en une seule ligne chiffree, plutot qu'une ligne par occurrence.
/// </summary>
public sealed record TakeoffRow(
    string Category,
    string Label,
    string? System,
    int Count,
    double TotalLengthM,
    double TotalWeightKg,
    double TotalInsulationAreaM2);

public sealed record TakeoffReport(IReadOnlyList<TakeoffRow> Rows)
{
    public double GrandTotalLengthM => Rows.Sum(r => r.TotalLengthM);
    public double GrandTotalWeightKg => Rows.Sum(r => r.TotalWeightKg);
    public double GrandTotalInsulationAreaM2 => Rows.Sum(r => r.TotalInsulationAreaM2);
}
