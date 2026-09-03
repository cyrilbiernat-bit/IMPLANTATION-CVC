using BimMep.Core.Bim;
using BimMep.Core.Mep;
using BimMep.Core.Takeoff;
using Xunit;

namespace BimMep.Tests;

public class TakeoffServiceTests
{
    private static Family CreateDuctFamily() => new() { Name = "Gaine rectangulaire", Category = "duct" };

    [Fact]
    public void GenerateNomenclature_SingleDuct_ComputesWeightAndLength()
    {
        var family = CreateDuctFamily();
        var type = family.AddType("Generique");
        var duct = new MepDuct("D1", type, DuctShape.Rectangular, lengthM: 10.0);
        duct.ResizeRectangular(0.8, 0.4);

        var project = new Project { Name = "P" };
        project.Elements.Add(duct);

        var report = TakeoffService.GenerateNomenclature(project);
        var row = Assert.Single(report.Rows);

        Assert.Equal("Gaine", row.Category);
        Assert.Equal("800x400 mm", row.Label);
        Assert.Equal(1, row.Count);
        Assert.Equal(10.0, row.TotalLengthM, precision: 6);
        // Perimetre = 2*(0.8+0.4) = 2.4 m ; poids = 2.4 * 10 * 6.0 kg/m2 = 144 kg
        Assert.Equal(144.0, row.TotalWeightKg, precision: 6);
        Assert.Equal(0.0, row.TotalInsulationAreaM2, precision: 6);
    }

    [Fact]
    public void GenerateNomenclature_InsulatedDuct_ComputesInsulationSurface()
    {
        var family = CreateDuctFamily();
        var type = family.AddType("Generique");
        var duct = new MepDuct("D1", type, DuctShape.Rectangular, lengthM: 5.0) { InsulationThicknessM = 0.03 };
        duct.ResizeRectangular(0.8, 0.4);

        var project = new Project { Name = "P" };
        project.Elements.Add(duct);

        var report = TakeoffService.GenerateNomenclature(project);
        var row = Assert.Single(report.Rows);

        // Perimetre 2.4 m * longueur 5 m = 12 m2 de calorifuge
        Assert.Equal(12.0, row.TotalInsulationAreaM2, precision: 6);
    }

    [Fact]
    public void GenerateNomenclature_CircularDuct_UsesCircumferenceForWeight()
    {
        var family = CreateDuctFamily();
        var type = family.AddType("Generique");
        var duct = new MepDuct("D1", type, DuctShape.Circular, lengthM: 4.0);
        duct.ResizeCircular(0.5);

        var project = new Project { Name = "P" };
        project.Elements.Add(duct);

        var report = TakeoffService.GenerateNomenclature(project);
        var row = Assert.Single(report.Rows);

        Assert.Equal("Ø500 mm", row.Label);
        double expectedWeight = Math.PI * 0.5 * 4.0 * 6.0;
        Assert.Equal(expectedWeight, row.TotalWeightKg, precision: 6);
    }

    [Fact]
    public void GenerateNomenclature_TwoDuctsSameDimensions_AreGroupedIntoOneRow()
    {
        var family = CreateDuctFamily();
        var type = family.AddType("Generique");
        var duct1 = new MepDuct("D1", type, DuctShape.Rectangular, lengthM: 3.0);
        duct1.ResizeRectangular(0.6, 0.3);
        var duct2 = new MepDuct("D2", type, DuctShape.Rectangular, lengthM: 7.0);
        duct2.ResizeRectangular(0.6, 0.3);

        var project = new Project { Name = "P" };
        project.Elements.Add(duct1);
        project.Elements.Add(duct2);

        var report = TakeoffService.GenerateNomenclature(project);
        var row = Assert.Single(report.Rows);

        Assert.Equal(2, row.Count);
        Assert.Equal(10.0, row.TotalLengthM, precision: 6);
    }

    [Fact]
    public void GenerateNomenclature_DuctsDifferentDimensions_ProduceSeparateRows()
    {
        var family = CreateDuctFamily();
        var type = family.AddType("Generique");
        var duct1 = new MepDuct("D1", type, DuctShape.Rectangular, lengthM: 3.0);
        duct1.ResizeRectangular(0.6, 0.3);
        var duct2 = new MepDuct("D2", type, DuctShape.Rectangular, lengthM: 7.0);
        duct2.ResizeRectangular(0.8, 0.4);

        var project = new Project { Name = "P" };
        project.Elements.Add(duct1);
        project.Elements.Add(duct2);

        var report = TakeoffService.GenerateNomenclature(project);

        Assert.Equal(2, report.Rows.Count);
    }

    [Fact]
    public void GenerateNomenclature_Pipe_HasNoWeightButHasLength()
    {
        var pipe = new MepPipe("P1", null, SystemClassification.WasteEv, lengthM: 8.0) { DiameterNominalM = 0.1 };

        var project = new Project { Name = "P" };
        project.Elements.Add(pipe);

        var report = TakeoffService.GenerateNomenclature(project);
        var row = Assert.Single(report.Rows);

        Assert.Equal("Tuyauterie", row.Category);
        Assert.Equal("Ø100 mm", row.Label);
        Assert.Equal("WasteEv", row.System);
        Assert.Equal(8.0, row.TotalLengthM, precision: 6);
        Assert.Equal(0.0, row.TotalWeightKg, precision: 6);
    }

    [Fact]
    public void GenerateNomenclature_CableTray_UsesLengthM()
    {
        var family = new Family { Name = "Chemin de cables", Category = "cable_tray" };
        var tray = new CableTray("T1", family.AddType("Generique")) { WidthM = 0.3, HeightM = 0.1, LengthM = 20.0 };

        var project = new Project { Name = "P" };
        project.Elements.Add(tray);

        var report = TakeoffService.GenerateNomenclature(project);
        var row = Assert.Single(report.Rows);

        Assert.Equal("Chemin de cables", row.Category);
        Assert.Equal(20.0, row.TotalLengthM, precision: 6);
    }

    [Fact]
    public void GenerateNomenclature_Equipment_CountsUnitsWithoutLengthOrWeight()
    {
        var equipment = new MepEquipment("CTA-1", null) { ManufacturerReference = "TROX-XYZ" };

        var project = new Project { Name = "P" };
        project.Elements.Add(equipment);

        var report = TakeoffService.GenerateNomenclature(project);
        var row = Assert.Single(report.Rows);

        Assert.Equal("Equipement", row.Category);
        Assert.Equal("TROX-XYZ", row.Label);
        Assert.Equal(1, row.Count);
        Assert.Equal(0.0, row.TotalLengthM);
        Assert.Equal(0.0, row.TotalWeightKg);
    }

    [Fact]
    public void GenerateNomenclature_UnsupportedCategory_IsIgnored()
    {
        var project = new Project { Name = "P" };
        project.Elements.Add(new UnsupportedElement("Mur"));

        var report = TakeoffService.GenerateNomenclature(project);

        Assert.Empty(report.Rows);
    }

    [Fact]
    public void ExportCsv_ProducesHeaderAndOneLinePerRow()
    {
        var family = CreateDuctFamily();
        var type = family.AddType("Generique");
        var duct = new MepDuct("D1", type, DuctShape.Rectangular, lengthM: 10.0);
        duct.ResizeRectangular(0.8, 0.4);

        var project = new Project { Name = "P" };
        project.Elements.Add(duct);

        var report = TakeoffService.GenerateNomenclature(project);
        string csv = TakeoffService.ExportCsv(report);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        Assert.Equal(2, lines.Length); // en-tete + 1 ligne
        Assert.StartsWith("Categorie,Label,Systeme,Nombre,LongueurTotaleM,PoidsTotalKg,SurfaceCalorifugeM2", lines[0]);
        Assert.Contains("Gaine,800x400 mm", lines[1]);
        Assert.Contains("144.00", lines[1]);
    }

    private sealed class UnsupportedElement : BimElement
    {
        public UnsupportedElement(string name) : base(name) { }
        public override string GetIfcType() => "IfcWall";
    }
}
