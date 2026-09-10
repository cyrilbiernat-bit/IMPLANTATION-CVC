namespace Bim.Cvc.Domain;

/// <summary>
/// Un plan PDF importé par un utilisateur (module 1). La calibration
/// (module 2) complètera cet objet avec l'échelle et les points de référence.
/// </summary>
public sealed class Drawing
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string FileName { get; init; }
    public required string StoragePath { get; init; }
    public required int NbPages { get; init; }
    public DateTimeOffset UploadedAt { get; init; } = DateTimeOffset.UtcNow;
}
