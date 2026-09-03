namespace BimMep.Core.Calculations;

/// <summary>Paroi d'enveloppe contribuant aux deperditions par transmission (mur, vitrage, toiture, plancher bas).</summary>
public sealed record EnvelopeElement(string Name, double AreaM2, double UValueWM2K);

public sealed record ThermalLossInput(
    IReadOnlyList<EnvelopeElement> EnvelopeElements,
    double VolumeM3,
    double AirChangesPerHour,
    double IndoorTemperatureC,
    double OutdoorTemperatureC);

public sealed record ThermalLossResult(double TransmissionLossW, double VentilationLossW, double TotalLossW);

/// <summary>
/// Calcul simplifie des deperditions thermiques d'un local (docs F-CALC-03, reference NF EN 12831).
/// Couvre les deux termes principaux de la norme (transmission a travers l'enveloppe + renouvellement
/// d'air) mais pas les coefficients complementaires (ponts thermiques lineiques, majoration
/// d'orientation/d'intermittence) : suffisant pour un pre-dimensionnement APS/APD (docs §5.7) ; un
/// dimensionnement EXE s'appuie sur un outil de calcul reglementaire complet et dedie (docs F-CALC-04,
/// le present module n'a pas vocation a se substituer a un moteur RE2020 certifie).
/// </summary>
public static class ThermalCalculator
{
    /// <summary>Capacite thermique volumique de l'air (Wh/(m3.K)), utilisee pour le terme de renouvellement d'air.</summary>
    private const double AirVolumetricHeatCapacityWhM3K = 0.34;

    public static ThermalLossResult ComputeRoomLoss(ThermalLossInput input)
    {
        double deltaT = input.IndoorTemperatureC - input.OutdoorTemperatureC;
        if (deltaT <= 0)
            return new ThermalLossResult(0, 0, 0); // pas de deperdition si l'exterieur n'est pas plus froid

        double transmission = input.EnvelopeElements.Sum(e => e.UValueWM2K * e.AreaM2) * deltaT;
        double ventilation = AirVolumetricHeatCapacityWhM3K * input.AirChangesPerHour * input.VolumeM3 * deltaT;

        return new ThermalLossResult(transmission, ventilation, transmission + ventilation);
    }
}
