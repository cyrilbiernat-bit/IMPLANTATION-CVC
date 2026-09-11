namespace Bim.Cvc.Domain;

/// <summary>
/// Calque d'un plan (module 3) — regroupe des objets CVC, avec affichage
/// et verrouillage indépendants, comme dans AutoCAD/Revit. Un plan a
/// toujours au moins un calque (créé automatiquement à l'import).
/// </summary>
public sealed class Layer
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid DrawingId { get; init; }
    public required string Name { get; set; }
    public string Color { get; set; } = "#0d9488";
    public bool Visible { get; set; } = true;
    public bool Locked { get; set; }
    public required int Order { get; init; }
}
