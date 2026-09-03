namespace BimMep.Core.Calculations;

public sealed record PipeSegmentInput(
    double LengthM,
    double InternalDiameterM,
    double FlowRateM3H,
    double RoughnessM = 0.00015,           // acier ~0.15 mm ; a ajuster selon le materiau (PVC ~0.0015 mm)
    double KinematicViscosityM2S = 1.0e-6, // eau a ~20°C
    IReadOnlyList<double>? SingularLossCoefficients = null);

public sealed record HydrauliqueResult(
    double VelocityMs,
    double ReynoldsNumber,
    bool IsTurbulent,
    double FrictionFactor,
    double LinearLossPa,
    double SingularLossPa,
    double TotalLossPa)
{
    public bool ExceedsRecommendedVelocity => VelocityMs > HydrauliqueCalculator.MaxRecommendedVelocityMs;
}

/// <summary>
/// Calcul hydraulique de base (docs F-CALC-02) : vitesse, regime d'ecoulement (Reynolds), pertes de
/// charge lineaires (Darcy-Weisbach) et singulieres d'un troncon de tuyauterie. Le facteur de
/// frottement en regime turbulent est obtenu par la formule explicite de Swamee-Jain, qui approxime
/// Colebrook-White sans iteration — suffisant pour un pre-dimensionnement (docs §5.7, LOD 200/300) ;
/// un calcul EXE affine avec les courbes constructeur reelles.
/// </summary>
public static class HydrauliqueCalculator
{
    public const double WaterDensityKgM3 = 1000.0;

    /// <summary>Limite usuelle de vitesse en reseau de chauffage/sanitaire avant risque de bruit/erosion.</summary>
    public const double MaxRecommendedVelocityMs = 2.0;

    private const double LaminarTurbulentThreshold = 4000.0;

    public static HydrauliqueResult Compute(PipeSegmentInput input)
    {
        if (input.InternalDiameterM <= 0)
            throw new ArgumentException("Le diametre interieur doit etre strictement positif.", nameof(input));

        double areaM2 = Math.PI * input.InternalDiameterM * input.InternalDiameterM / 4.0;
        double flowM3S = input.FlowRateM3H / 3600.0;
        double velocity = flowM3S / areaM2;

        double reynolds = velocity * input.InternalDiameterM / input.KinematicViscosityM2S;
        bool turbulent = reynolds > LaminarTurbulentThreshold;

        double frictionFactor = turbulent
            ? SwameeJainFrictionFactor(reynolds, input.RoughnessM, input.InternalDiameterM)
            : 64.0 / Math.Max(reynolds, 1.0); // regime laminaire (loi de Hagen-Poiseuille)

        double linearLoss = frictionFactor
                             * (input.LengthM / input.InternalDiameterM)
                             * (WaterDensityKgM3 * velocity * velocity / 2.0);

        double dynamicPressure = 0.5 * WaterDensityKgM3 * velocity * velocity;
        double singularLoss = 0.0;
        if (input.SingularLossCoefficients is not null)
        {
            foreach (var k in input.SingularLossCoefficients)
                singularLoss += k * dynamicPressure;
        }

        return new HydrauliqueResult(velocity, reynolds, turbulent, frictionFactor, linearLoss, singularLoss, linearLoss + singularLoss);
    }

    /// <summary>Formule explicite de Swamee-Jain (valide pour 5000 &lt; Re &lt; 10^8, rugosite relative &lt; 0.05).</summary>
    private static double SwameeJainFrictionFactor(double reynolds, double roughnessM, double diameterM)
    {
        double relativeRoughness = roughnessM / diameterM;
        double denominator = Math.Log10(relativeRoughness / 3.7 + 5.74 / Math.Pow(reynolds, 0.9));
        return 0.25 / (denominator * denominator);
    }

    public static class SingularCoefficients
    {
        public const double Elbow90 = 0.9;
        public const double Elbow45 = 0.4;
        public const double TeeBranch = 1.8;
        public const double GateValveOpen = 0.2;
        public const double CheckValve = 2.0;
    }
}
