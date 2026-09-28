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

    /// <summary>
    /// Décalage (mètres, repère du plan 2D) à appliquer aux coordonnées IFC
    /// du modèle pour le recaler sur le réseau CVC — le repère du fichier
    /// IFC ne coïncide pas forcément avec l'origine du plan calibré. Fixé
    /// par recalage interactif dans la vue 3D (<see cref="BuildingModelService.SetAlignment"/>) ;
    /// remis à zéro à chaque nouvel import.
    /// </summary>
    public double OffsetXMeters { get; set; }
    public double OffsetZMeters { get; set; }
}
