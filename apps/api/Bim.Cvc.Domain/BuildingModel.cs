namespace Bim.Cvc.Domain;

/// <summary>
/// Élément géométrique d'un modèle de bâtiment IFC (mur, dalle, espace…),
/// déjà triangulé pour l'affichage 3D — un maillage classique (sommets +
/// indices de triangles), directement exploitable par un moteur de rendu
/// comme Three.js sans traitement supplémentaire côté client.
/// </summary>
public sealed record BuildingElement(
    string IfcType,
    string Name,
    IReadOnlyList<float> Positions,
    IReadOnlyList<int> Indices);

/// <summary>
/// Modèle de bâtiment importé depuis un fichier IFC (export Revit,
/// ArchiCAD, FreeCAD/BIM…) — contexte architectural affiché aux côtés du
/// réseau CVC dans la vue 3D (Lot 2). Un projet a au plus un modèle de
/// bâtiment ; un nouvel import remplace le précédent.
/// </summary>
public sealed class BuildingModel
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid ProjectId { get; init; }
    public required string FileName { get; init; }
    public DateTimeOffset UploadedAt { get; init; } = DateTimeOffset.UtcNow;
    public required IReadOnlyList<BuildingElement> Elements { get; init; }
}
