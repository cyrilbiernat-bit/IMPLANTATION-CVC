namespace BimMep.Core.Calculations;

/// <summary>
/// Constantes matiere partagees entre modules (docs §9 métrés, §7.3.1 optimisation de poids) — evite
/// de dupliquer des valeurs "magiques" (ex. masse surfacique de tole) dans Core.Routing et Core.Takeoff.
/// Valeurs indicatives (cf. docs §13-modules-critiques.md) : a confronter aux catalogues fabricants
/// reels (epaisseur de tole selon norme EN 1507/1506, classe d'etancheite) en production.
/// </summary>
public static class MaterialConstants
{
    /// <summary>Masse surfacique usuelle d'une tole galvanisee de gaine aeraulique (kg/m2).</summary>
    public const double GalvanizedSteelSheetKgPerM2 = 6.0;
}
