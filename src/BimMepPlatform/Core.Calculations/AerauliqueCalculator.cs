namespace BimMep.Core.Calculations;

/// <summary>
/// Module de calcul pur (aucune dependance a Core.Bim/Core.Mep) : opere sur des grandeurs physiques
/// brutes pour rester reutilisable aussi bien par le moteur BIM (Core.Mep, docs §3.3 — le sens de la
/// fleche de dependance est Mep -> Calculations, jamais l'inverse) que par des outils externes
/// (export de notes de calcul, scripts de verification independants).
/// </summary>
public static class AirProperties
{
    /// <summary>Masse volumique de l'air a 20°C / pression atmospherique standard.</summary>
    public const double DensityKgM3 = 1.2;
}

public sealed record DuctSegmentInput(
    double LengthM,
    double CrossSectionAreaM2,
    double HydraulicDiameterM,
    double FlowRateM3H,
    double FrictionFactor = 0.02,
    IReadOnlyList<double>? SingularLossCoefficients = null);

public sealed record AerauliqueResult(
    double VelocityMs,
    double DynamicPressurePa,
    double LinearLossPa,
    double SingularLossPa,
    double TotalLossPa)
{
    public bool ExceedsRecommendedVelocity => VelocityMs > AerauliqueCalculator.MaxRecommendedVelocityMs;
}

/// <summary>
/// Calcul aeraulique de base (docs F-CALC-01) : vitesse, pression dynamique, pertes de charge
/// lineaires et singulieres d'un troncon de gaine. Le coefficient de frottement par defaut (0.02)
/// et les coefficients de pertes singulieres ci-dessous sont des valeurs indicatives usuelles pour
/// une gaine galvanisee ; une implementation de production les affine via les abaques normalises
/// EN 12237 / ASHRAE en fonction de la rugosite reelle et du nombre de Reynolds (cf. approche
/// analogue a <see cref="HydrauliqueCalculator"/> pour l'eau).
/// </summary>
public static class AerauliqueCalculator
{
    /// <summary>Seuil usuel de vitesse en gaine principale avant risque de bruit/perte de charge excessive (docs EN 16798).</summary>
    public const double MaxRecommendedVelocityMs = 6.0;

    public static AerauliqueResult Compute(DuctSegmentInput input)
    {
        if (input.CrossSectionAreaM2 <= 0)
            throw new ArgumentException("La section du troncon doit etre strictement positive.", nameof(input));

        double flowM3S = input.FlowRateM3H / 3600.0;
        double velocity = flowM3S / input.CrossSectionAreaM2;
        double dynamicPressure = 0.5 * AirProperties.DensityKgM3 * velocity * velocity;

        double linearLoss = input.FrictionFactor
                             * (input.LengthM / Math.Max(input.HydraulicDiameterM, 1e-3))
                             * dynamicPressure;

        double singularLoss = 0.0;
        if (input.SingularLossCoefficients is not null)
        {
            foreach (var zeta in input.SingularLossCoefficients)
                singularLoss += zeta * dynamicPressure;
        }

        return new AerauliqueResult(velocity, dynamicPressure, linearLoss, singularLoss, linearLoss + singularLoss);
    }

    /// <summary>Coefficients de perte de charge singuliere indicatifs pour raccords aerauliques usuels.</summary>
    public static class SingularCoefficients
    {
        public const double ElbowRound90 = 0.25;
        public const double ElbowRectangular90 = 0.35;
        public const double TeeBranch = 1.0;
        public const double Reduction = 0.15;
        public const double Diffuser = 0.5;
    }
}
