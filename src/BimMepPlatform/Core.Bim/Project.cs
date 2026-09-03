using BimMep.Core.Geometry;

namespace BimMep.Core.Bim;

public enum ProjectPhase { APS, APD, PRO, EXE, DOE }

/// <summary>Niveau (etage) du batiment (docs §4.2 table levels).</summary>
public sealed class Level
{
    public Guid Id { get; } = Guid.NewGuid();
    public required string Name { get; init; }
    public required double ElevationMeters { get; init; }
    public required double HeightMeters { get; init; }
    public int SortOrder { get; init; }
}

/// <summary>Piece/local (docs §4.2 table rooms). Le contour est simplifie a un polygone 2D + hauteur.</summary>
public sealed class Room
{
    public Guid Id { get; } = Guid.NewGuid();
    public required string Name { get; init; }
    public required Level Level { get; init; }
    public required IReadOnlyList<Point3D> Boundary { get; init; }
    public double AreaM2 { get; set; }
    public double VolumeM3 { get; set; }
    public double HeatingLoadW { get; set; }
    public double CoolingLoadW { get; set; }
}

/// <summary>
/// Racine agregat du modele BIM d'un projet. `CurrentLod` pilote les controles de coherence
/// a l'export (docs §5.7) : un export EXE est refuse si des elements MEP structurants restent
/// en LOD inferieur a 300 (cf. <see cref="ValidateLodConsistency"/>).
/// </summary>
public sealed class Project
{
    public Guid Id { get; } = Guid.NewGuid();
    public required string Name { get; init; }
    public ProjectPhase Phase { get; set; } = ProjectPhase.APS;
    public int CurrentLod { get; set; } = 100;

    public List<Level> Levels { get; } = new();
    public List<Room> Rooms { get; } = new();
    public List<BimElement> Elements { get; } = new();

    private const int MinLodForExe = 300;

    public ValidationResult ValidateLodConsistency()
    {
        var result = ValidationResult.Ok();
        if (Phase != ProjectPhase.EXE) return result;

        foreach (var element in Elements.Where(e => e.Lod < MinLodForExe))
        {
            result.Issues.Add(new ValidationIssue(
                Code: "LOD_INSUFFICIENT_FOR_EXE",
                Message: $"L'element '{element.Name}' ({element.GetIfcType()}) est en LOD {element.Lod}, " +
                         $"insuffisant pour une phase EXE (minimum {MinLodForExe}).",
                Severity: ValidationSeverity.Error));
        }
        return result;
    }
}
