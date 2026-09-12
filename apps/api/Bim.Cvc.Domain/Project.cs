namespace Bim.Cvc.Domain;

/// <summary>
/// Un projet regroupe un ou plusieurs plans (module 1) — c'est l'échelle à
/// laquelle les métrés (module 5) sont agrégés.
/// </summary>
public sealed class Project
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Name { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
