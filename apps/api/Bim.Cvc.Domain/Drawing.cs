namespace Bim.Cvc.Domain;

/// <summary>
/// Un plan PDF importé par un utilisateur (module 1). La calibration
/// (module 2) complètera cet objet avec l'échelle et les points de référence.
/// </summary>
public sealed class Drawing
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid ProjectId { get; init; }
    public required string FileName { get; init; }
    public required string StoragePath { get; init; }
    public required int NbPages { get; init; }
    public DateTimeOffset UploadedAt { get; init; } = DateTimeOffset.UtcNow;
    public PlanFormat Format { get; init; } = PlanFormat.Pdf;

    /// <summary>Géométrie extraite pour un plan vectoriel (DXF/DWG). Null pour un PDF.</summary>
    public IReadOnlyList<PlanEntity>? VectorEntities { get; init; }

    /// <summary>Échelle du plan (module 2). Null tant qu'il n'a pas été calibré.</summary>
    public Calibration? Calibration { get; set; }
}
