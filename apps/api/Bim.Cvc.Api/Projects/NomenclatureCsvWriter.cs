using System.Globalization;
using System.Text;

namespace Bim.Cvc.Api.Projects;

/// <summary>
/// Module 6 — export de la nomenclature au format CSV. Séparateur
/// point-virgule : c'est ce qu'Excel en locale française attend pour
/// scinder les colonnes automatiquement à l'ouverture (la virgule y est
/// le séparateur décimal).
/// </summary>
public static class NomenclatureCsvWriter
{
    private static readonly string[] Headers =
    [
        "Identifiant", "Plan", "Calque", "Type", "Dimensions", "Longueur (m)",
        "Débit (m³/h)", "Vitesse (m/s)", "Pression (Pa)", "Poids (kg)", "Surface calorifuge (m²)",
    ];

    public static string Write(IEnumerable<NomenclatureRowDto> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(';', Headers));

        foreach (var row in rows)
        {
            var dimensions = row.DiameterMm is { } diameter
                ? $"⌀{FormatNumber(diameter)} mm"
                : row.WidthMm is { } width && row.HeightMm is { } height
                    ? $"{FormatNumber(width)}×{FormatNumber(height)} mm"
                    : "";

            var fields = new[]
            {
                row.ObjectId.ToString()[..8],
                row.DrawingFileName,
                row.LayerName,
                row.Type,
                dimensions,
                FormatNumber(row.LengthMeters),
                FormatNumber(row.DebitM3h),
                FormatNumber(row.VitesseMs),
                FormatNumber(row.PressionPa),
                FormatNumber(row.WeightKg),
                FormatNumber(row.InsulationAreaM2),
            };

            sb.AppendLine(string.Join(';', fields.Select(Escape)));
        }

        return sb.ToString();
    }

    private static string FormatNumber(double? value) =>
        value is null ? "" : value.Value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string Escape(string value) =>
        value.IndexOfAny([';', '"', '\n']) >= 0
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
}
