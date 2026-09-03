using System.Globalization;
using System.Text;
using BimMep.Core.Bim;
using BimMep.Core.Calculations;
using BimMep.Core.Mep;

namespace BimMep.Core.Takeoff;

/// <summary>
/// Calcule les metres automatiques du projet (docs F-TAKEOFF-01/02) : poids de gaine, surface de
/// calorifuge, longueurs de reseaux par type/dimension/systeme, avec export CSV. Ne depend que du
/// modele en memoire (Core.Bim/Core.Mep) — aucune ecriture disque, aucun format de fichier impose
/// (l'appelant decide ou ecrire le CSV retourne par <see cref="ExportCsv"/>).
///
/// Simplification assumee : seules les gaines aerauliques (tole) recoivent un poids calcule (docs §9
/// "poids de gaine") ; tuyauteries et chemins de cables n'ont pas de formule de poids lineique fiable
/// sans catalogue fabricant (materiau/DN variables), leur ligne de nomenclature ne chiffre donc que la
/// longueur et le nombre.
/// </summary>
public static class TakeoffService
{
    private sealed class Accumulator
    {
        public int Count;
        public double TotalLengthM;
        public double TotalWeightKg;
        public double TotalInsulationAreaM2;
    }

    public static TakeoffReport GenerateNomenclature(Project project)
    {
        var groups = new Dictionary<(string Category, string Label, string? System), Accumulator>();

        void Add(string category, string label, string? system, double lengthM, double weightKg, double insulationAreaM2)
        {
            var key = (category, label, system);
            if (!groups.TryGetValue(key, out var acc))
                groups[key] = acc = new Accumulator();

            acc.Count++;
            acc.TotalLengthM += lengthM;
            acc.TotalWeightKg += weightKg;
            acc.TotalInsulationAreaM2 += insulationAreaM2;
        }

        foreach (var element in project.Elements)
        {
            switch (element)
            {
                case MepDuct duct:
                {
                    double perimeterM = duct.Shape == DuctShape.Rectangular
                        ? 2.0 * (duct.WidthM + duct.HeightM)
                        : Math.PI * duct.DiameterM;
                    double weightKg = perimeterM * duct.LengthM * MaterialConstants.GalvanizedSteelSheetKgPerM2;
                    double insulationAreaM2 = duct.InsulationThicknessM > 0 ? perimeterM * duct.LengthM : 0.0;
                    string label = duct.Shape == DuctShape.Rectangular
                        ? $"{duct.WidthM * 1000:F0}x{duct.HeightM * 1000:F0} mm"
                        : $"Ø{duct.DiameterM * 1000:F0} mm";

                    Add("Gaine", label, PrimarySystem(duct.Connectors.Select(c => c.System)),
                        duct.LengthM, weightKg, insulationAreaM2);
                    break;
                }
                case MepPipe pipe:
                {
                    string label = $"Ø{pipe.DiameterNominalM * 1000:F0} mm";
                    Add("Tuyauterie", label, pipe.SystemType.ToString(), pipe.LengthM, weightKg: 0, insulationAreaM2: 0);
                    break;
                }
                case CableTray tray:
                {
                    string label = $"{tray.WidthM * 1000:F0}x{tray.HeightM * 1000:F0} mm";
                    Add("Chemin de cables", label, PrimarySystem(tray.Cables.Select(c => c.System)),
                        tray.LengthM, weightKg: 0, insulationAreaM2: 0);
                    break;
                }
                case MepEquipment equipment:
                {
                    Add("Equipement", equipment.ManufacturerReference ?? equipment.Name, system: null,
                        lengthM: 0, weightKg: 0, insulationAreaM2: 0);
                    break;
                }
                // Categories hors perimetre de ce dossier d'exemples (murs, portes, ...) : ignorees
                // silencieusement, comme dans IfcProjectExporter.ExportElement (docs §13).
            }
        }

        var rows = groups
            .OrderBy(kv => kv.Key.Category, StringComparer.Ordinal)
            .ThenBy(kv => kv.Key.Label, StringComparer.Ordinal)
            .Select(kv => new TakeoffRow(
                kv.Key.Category, kv.Key.Label, kv.Key.System,
                kv.Value.Count, kv.Value.TotalLengthM, kv.Value.TotalWeightKg, kv.Value.TotalInsulationAreaM2))
            .ToList();

        return new TakeoffReport(rows);
    }

    private static string? PrimarySystem(IEnumerable<SystemClassification> systems) =>
        systems.Select(s => s.ToString()).FirstOrDefault();

    /// <summary>
    /// Export CSV (docs F-TAKEOFF-02). Delimiteur virgule, nombres en culture invariante (point
    /// decimal) — un export vers Excel localise en francais peut necessiter un delimiteur ';' selon
    /// les parametres regionaux du poste ; a adapter au moment de l'integration (hors perimetre ici).
    /// </summary>
    public static string ExportCsv(TakeoffReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Categorie,Label,Systeme,Nombre,LongueurTotaleM,PoidsTotalKg,SurfaceCalorifugeM2");

        foreach (var row in report.Rows)
        {
            sb.Append(CsvField(row.Category)).Append(',')
              .Append(CsvField(row.Label)).Append(',')
              .Append(CsvField(row.System ?? string.Empty)).Append(',')
              .Append(row.Count.ToString(CultureInfo.InvariantCulture)).Append(',')
              .Append(row.TotalLengthM.ToString("F2", CultureInfo.InvariantCulture)).Append(',')
              .Append(row.TotalWeightKg.ToString("F2", CultureInfo.InvariantCulture)).Append(',')
              .Append(row.TotalInsulationAreaM2.ToString("F2", CultureInfo.InvariantCulture))
              .AppendLine();
        }

        return sb.ToString();
    }

    private static string CsvField(string value) =>
        value.Contains(',') || value.Contains('"')
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
}
