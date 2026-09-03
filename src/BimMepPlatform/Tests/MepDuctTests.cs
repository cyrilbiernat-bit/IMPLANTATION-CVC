using BimMep.Core.Bim;
using BimMep.Core.Geometry;
using BimMep.Core.Mep;
using Xunit;

namespace BimMep.Tests;

public class MepDuctTests
{
    private static Family CreateDuctFamily() => new() { Name = "Gaine rectangulaire", Category = "duct" };

    [Fact]
    public void ResizeRectangular_UpdatesDimensionsAndCrossSection()
    {
        var family = CreateDuctFamily();
        var type = family.AddType("Generique");
        var duct = new MepDuct("D1", type, DuctShape.Rectangular, lengthM: 5.0);

        duct.ResizeRectangular(0.8, 0.4);

        Assert.Equal(0.8, duct.WidthM);
        Assert.Equal(0.4, duct.HeightM);
        Assert.Equal(0.32, duct.CrossSectionAreaM2, precision: 10);
        Assert.True(duct.IsDirty);
    }

    [Fact]
    public void ResizeCircular_OnRectangularDuct_Throws()
    {
        var family = CreateDuctFamily();
        var type = family.AddType("Generique");
        var duct = new MepDuct("D1", type, DuctShape.Rectangular, lengthM: 5.0);

        Assert.Throws<InvalidOperationException>(() => duct.ResizeCircular(0.5));
    }

    [Fact]
    public void Recompute_MismatchedConnectedDuctSizes_ProducesDiscontinuityWarning()
    {
        var family = CreateDuctFamily();
        var type = family.AddType("Generique");

        var duct1 = new MepDuct("Troncon-1", type, DuctShape.Rectangular, lengthM: 6.0);
        duct1.ResizeRectangular(0.8, 0.4);
        var outlet = duct1.AddConnector(new Point3D(6, 0, 3), new Vector3D(1, 0, 0), SystemClassification.SupplyAir);

        var duct2 = new MepDuct("Troncon-2", type, DuctShape.Rectangular, lengthM: 4.0);
        duct2.ResizeRectangular(0.8, 0.4);
        var inlet = duct2.AddConnector(new Point3D(0, 0, 3), new Vector3D(-1, 0, 0), SystemClassification.SupplyAir);
        outlet.ConnectTo(inlet);

        duct2.AddDependency(duct1);
        duct1.RegisterDependent(duct2);

        var all = new Dictionary<Guid, IRecomputable> { [duct1.Id] = duct1, [duct2.Id] = duct2 };
        var scheduler = new RecomputeScheduler();

        // Baseline : sections identiques, aucun avertissement attendu.
        var baseline = scheduler.RunFrom(new IRecomputable[] { duct1 }, all);
        Assert.Empty(baseline.Warnings);

        // Redimensionnement d'un seul des deux troncons -> discontinuite.
        duct1.ResizeRectangular(1.0, 0.5);
        var report = scheduler.RunFrom(new IRecomputable[] { duct1 }, all);

        Assert.Single(report.Warnings);
        Assert.Contains("Discontinuite de section", report.Warnings[0]);
    }

    [Fact]
    public void AddConnector_SizesMatchCurrentDuctDimensions()
    {
        var family = CreateDuctFamily();
        var type = family.AddType("Generique");
        var duct = new MepDuct("D1", type, DuctShape.Circular, lengthM: 3.0);
        duct.ResizeCircular(0.315);

        var connector = duct.AddConnector(new Point3D(0, 0, 0), new Vector3D(1, 0, 0), SystemClassification.ExtractAir);

        Assert.Equal(0.315, connector.SizePrimary);
        Assert.Equal(0.0, connector.SizeSecondary);
    }

    [Fact]
    public void MepPipe_GravityNetwork_WarnsWhenSlopeBelowMinimum()
    {
        var pipe = new MepPipe("EV-1", null, SystemClassification.WasteEv, lengthM: 8.0)
        {
            SlopePercent = 0.4
        };

        var result = pipe.Validate();

        Assert.True(result.IsValid); // un avertissement seul ne rend pas le resultat invalide
        Assert.Contains(result.Issues, i => i.Code == "PENTE_INSUFFISANTE" && i.Severity == ValidationSeverity.Warning);
    }

    [Fact]
    public void MepPipe_GravityNetwork_DefaultSlopeMeetsMinimum()
    {
        var pipe = new MepPipe("EU-1", null, SystemClassification.WasteEu, lengthM: 8.0);

        var result = pipe.Validate();

        Assert.DoesNotContain(result.Issues, i => i.Code == "PENTE_INSUFFISANTE");
    }
}
