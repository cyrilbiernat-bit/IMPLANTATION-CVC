using System.Diagnostics;
using System.Text.Json;
using Bim.Cvc.Application;
using Bim.Cvc.Domain;
using Microsoft.Extensions.Options;

namespace Bim.Cvc.Infrastructure;

public sealed class IfcExtractionOptions
{
    /// <summary>Interpréteur Python à invoquer (doit avoir "ifcopenshell" installé — pip install ifcopenshell).</summary>
    public string PythonExecutable { get; set; } = "python3";

    /// <summary>Chemin du script d'extraction, copié à côté de l'assembly à la compilation (voir le .csproj).</summary>
    public string ScriptPath { get; set; } = Path.Combine(AppContext.BaseDirectory, "Ifc", "ifc_extract.py");

    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);
}

/// <summary>
/// Extrait la géométrie IFC en sous-processus Python via IfcOpenShell (le
/// moteur open source qu'utilise notamment l'atelier BIM de FreeCAD) — il
/// n'existe pas de bibliothèque .NET équivalente pour la géométrie IFC.
/// </summary>
public sealed class IfcOpenShellGeometryExtractor(IOptions<IfcExtractionOptions> options) : IIfcGeometryExtractor
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private sealed record ExtractedElementJson(string IfcType, string Name, List<float> Positions, List<int> Indices);

    public async Task<IReadOnlyList<BuildingElement>> ExtractAsync(string ifcFilePath, CancellationToken ct = default)
    {
        var opts = options.Value;
        var outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = opts.PythonExecutable,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            startInfo.ArgumentList.Add(opts.ScriptPath);
            startInfo.ArgumentList.Add(ifcFilePath);
            startInfo.ArgumentList.Add(outputPath);

            using var process = Process.Start(startInfo)
                ?? throw new InvalidBuildingModelException("Impossible de démarrer l'extraction IFC (Python introuvable).");

            var stderrTask = process.StandardError.ReadToEndAsync(ct);
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(opts.Timeout);

            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                process.Kill(entireProcessTree: true);
                throw new InvalidBuildingModelException("L'extraction du modèle IFC a dépassé le délai maximal.");
            }

            if (process.ExitCode != 0)
            {
                var stderr = await stderrTask;
                throw new InvalidBuildingModelException(
                    $"Échec de l'extraction du modèle IFC : {(string.IsNullOrWhiteSpace(stderr) ? $"code {process.ExitCode}" : stderr.Trim())}");
            }

            await using var stream = File.OpenRead(outputPath);
            var rows = await JsonSerializer.DeserializeAsync<List<ExtractedElementJson>>(stream, JsonOptions, ct) ?? [];
            return rows.Select(r => new BuildingElement(r.IfcType, r.Name, r.Positions, r.Indices)).ToList();
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }
}
