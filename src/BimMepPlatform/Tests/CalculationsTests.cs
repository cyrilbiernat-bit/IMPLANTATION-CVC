using BimMep.Core.Calculations;
using Xunit;

namespace BimMep.Tests;

public class AerauliqueCalculatorTests
{
    [Fact]
    public void Compute_KnownValues_MatchesHandCalculation()
    {
        // Gaine 0.8x0.4 m (Dh = 2*0.8*0.4/1.2 = 8/15 m), longueur 10 m, debit 5760 m3/h
        // choisi pour donner une vitesse ronde de 5.0 m/s (5760/3600 / 0.32 = 5.0).
        var input = new DuctSegmentInput(
            LengthM: 10.0,
            CrossSectionAreaM2: 0.32,
            HydraulicDiameterM: 8.0 / 15.0,
            FlowRateM3H: 5760.0,
            FrictionFactor: 0.02);

        var result = AerauliqueCalculator.Compute(input);

        Assert.Equal(5.0, result.VelocityMs, precision: 6);
        Assert.Equal(15.0, result.DynamicPressurePa, precision: 6);   // 0.5 * 1.2 * 5^2
        Assert.Equal(5.625, result.LinearLossPa, precision: 6);       // 0.02 * 18.75 * 15
        Assert.Equal(0.0, result.SingularLossPa, precision: 6);
        Assert.Equal(5.625, result.TotalLossPa, precision: 6);
        Assert.False(result.ExceedsRecommendedVelocity);
    }

    [Fact]
    public void Compute_WithSingularCoefficients_AddsToTotalLoss()
    {
        var input = new DuctSegmentInput(
            LengthM: 10.0,
            CrossSectionAreaM2: 0.32,
            HydraulicDiameterM: 8.0 / 15.0,
            FlowRateM3H: 5760.0,
            SingularLossCoefficients: new[] { AerauliqueCalculator.SingularCoefficients.ElbowRound90, 1.0 });

        var result = AerauliqueCalculator.Compute(input);

        // (0.25 + 1.0) * 15.0 Pa de pression dynamique
        Assert.Equal(18.75, result.SingularLossPa, precision: 6);
        Assert.Equal(result.LinearLossPa + 18.75, result.TotalLossPa, precision: 6);
    }

    [Fact]
    public void Compute_HighVelocity_FlagsExceedsRecommendedVelocity()
    {
        var input = new DuctSegmentInput(
            LengthM: 5.0,
            CrossSectionAreaM2: 0.32,
            HydraulicDiameterM: 8.0 / 15.0,
            FlowRateM3H: 8000.0); // ~6.94 m/s > 6.0 m/s

        var result = AerauliqueCalculator.Compute(input);

        Assert.True(result.ExceedsRecommendedVelocity);
    }

    [Fact]
    public void Compute_ZeroOrNegativeArea_Throws()
    {
        var input = new DuctSegmentInput(1.0, 0.0, 0.5, 1000.0);
        Assert.Throws<ArgumentException>(() => AerauliqueCalculator.Compute(input));
    }
}

public class HydrauliqueCalculatorTests
{
    [Fact]
    public void Compute_TurbulentFlow_ComputesVelocityAndReynolds()
    {
        // D = 50 mm ; debit calibre pour vitesse = 1.0 m/s : Q = v * A * 3600
        double diameter = 0.05;
        double area = Math.PI * diameter * diameter / 4.0;
        double flowM3H = 1.0 * area * 3600.0;

        var input = new PipeSegmentInput(LengthM: 20.0, InternalDiameterM: diameter, FlowRateM3H: flowM3H);
        var result = HydrauliqueCalculator.Compute(input);

        Assert.Equal(1.0, result.VelocityMs, precision: 6);
        Assert.Equal(50000.0, result.ReynoldsNumber, precision: 1); // v*D/nu = 1.0*0.05/1e-6
        Assert.True(result.IsTurbulent);
        Assert.InRange(result.FrictionFactor, 0.015, 0.05); // plage plausible pour acier, Re=5e4
        Assert.True(result.LinearLossPa > 0);
        Assert.False(result.ExceedsRecommendedVelocity); // 1.0 m/s < 2.0 m/s
    }

    [Fact]
    public void Compute_LowReynolds_UsesLaminarFrictionFactor()
    {
        // v = 0.05 m/s, D = 0.02 m => Re = 0.05*0.02/1e-6 = 1000 < 4000 -> laminaire
        double diameter = 0.02;
        double velocity = 0.05;
        double area = Math.PI * diameter * diameter / 4.0;
        double flowM3H = velocity * area * 3600.0;

        var input = new PipeSegmentInput(LengthM: 5.0, InternalDiameterM: diameter, FlowRateM3H: flowM3H);
        var result = HydrauliqueCalculator.Compute(input);

        Assert.False(result.IsTurbulent);
        Assert.Equal(64.0 / result.ReynoldsNumber, result.FrictionFactor, precision: 6);
    }

    [Fact]
    public void Compute_ZeroOrNegativeDiameter_Throws()
    {
        var input = new PipeSegmentInput(1.0, 0.0, 100.0);
        Assert.Throws<ArgumentException>(() => HydrauliqueCalculator.Compute(input));
    }
}

public class ThermalCalculatorTests
{
    [Fact]
    public void ComputeRoomLoss_KnownValues_MatchesHandCalculation()
    {
        var input = new ThermalLossInput(
            EnvelopeElements: new[]
            {
                new EnvelopeElement("Mur", AreaM2: 20, UValueWM2K: 1.5),
                new EnvelopeElement("Vitrage", AreaM2: 5, UValueWM2K: 2.5)
            },
            VolumeM3: 50,
            AirChangesPerHour: 0.5,
            IndoorTemperatureC: 20,
            OutdoorTemperatureC: 0);

        var result = ThermalCalculator.ComputeRoomLoss(input);

        // Transmission = (20*1.5 + 5*2.5) * 20 = 850 W ; Ventilation = 0.34*0.5*50*20 = 170 W
        Assert.Equal(850.0, result.TransmissionLossW, precision: 6);
        Assert.Equal(170.0, result.VentilationLossW, precision: 6);
        Assert.Equal(1020.0, result.TotalLossW, precision: 6);
    }

    [Fact]
    public void ComputeRoomLoss_OutdoorWarmerThanIndoor_ReturnsZero()
    {
        var input = new ThermalLossInput(
            EnvelopeElements: new[] { new EnvelopeElement("Mur", 20, 1.5) },
            VolumeM3: 50,
            AirChangesPerHour: 0.5,
            IndoorTemperatureC: 15,
            OutdoorTemperatureC: 20);

        var result = ThermalCalculator.ComputeRoomLoss(input);

        Assert.Equal(0.0, result.TotalLossW);
    }
}
