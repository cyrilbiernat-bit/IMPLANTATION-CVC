using System.Text.RegularExpressions;
using BimMep.Core.Bim;
using BimMep.Core.Geometry;
using BimMep.Core.Ifc;
using BimMep.Core.Mep;
using Xunit;

namespace BimMep.Tests;

public class IfcStepWriterTests
{
    [Fact]
    public void FormatValue_Real_AlwaysIncludesDecimalPoint()
    {
        Assert.Equal("10.5", IfcStepWriter.FormatValue(10.5));
        Assert.Equal("10.", IfcStepWriter.FormatValue(10.0));
        Assert.Equal("0.", IfcStepWriter.FormatValue(0.0));
    }

    [Fact]
    public void FormatValue_ScientificNotation_MantissaKeepsDecimalPoint()
    {
        // Regression : "1E-05" (sans point dans la mantisse) est invalide en grammaire EXPRESS et a
        // fait echouer le moteur geometrique OCCT/IfcOpenShell lors de la validation (docs §13).
        string formatted = IfcStepWriter.FormatValue(1e-5);
        Assert.Matches(@"^\d+\.\d*e-05$|^\d+\.e-05$", formatted.ToLowerInvariant());
        Assert.Contains('.', formatted[..formatted.IndexOfAny(new[] { 'e', 'E' })]);
    }

    [Fact]
    public void FormatValue_Null_IsDollarSign() => Assert.Equal("$", IfcStepWriter.FormatValue(null));

    [Fact]
    public void FormatValue_Derived_IsAsterisk() => Assert.Equal("*", IfcStepWriter.FormatValue(IfcDerived.Instance));

    [Fact]
    public void FormatValue_Ref_IsHashPrefixed() => Assert.Equal("#42", IfcStepWriter.FormatValue(new IfcRef(42)));

    [Fact]
    public void FormatValue_Enum_IsDotWrapped() => Assert.Equal(".METRE.", IfcStepWriter.FormatValue(new IfcEnum("METRE")));

    [Fact]
    public void FormatValue_Bool_IsTOrF()
    {
        Assert.Equal(".T.", IfcStepWriter.FormatValue(true));
        Assert.Equal(".F.", IfcStepWriter.FormatValue(false));
    }

    [Fact]
    public void FormatValue_String_EscapesQuotes() => Assert.Equal("'it''s'", IfcStepWriter.FormatValue("it's"));

    [Fact]
    public void FormatValue_TypedLiteral_WrapsInTypeName() =>
        Assert.Equal("IFCREAL(1.5)", IfcStepWriter.FormatValue(new IfcTypedLiteral("IFCREAL", 1.5)));

    [Fact]
    public void Write_SingleListAttributeEntity_ProducesNestedParentheses()
    {
        // Regression directe du bug trouve par validation IfcOpenShell : IfcCartesianPoint n'a qu'un
        // seul attribut explicite (une liste) et doit donc se serialiser avec des parentheses imbriquees.
        var writer = new IfcStepWriter();
        writer.Write("IFCCARTESIANPOINT", new List<object?> { 1.0, 2.0, 3.0 });
        string doc = writer.BuildDocument("d", "f.ifc", "IFC4");

        Assert.Contains("=IFCCARTESIANPOINT((1.,2.,3.));", doc);
    }

    [Fact]
    public void Write_MultipleFlatAttributes_ProducesSingleLevelParentheses()
    {
        var writer = new IfcStepWriter();
        writer.Write("IFCTESTENTITY", 1.0, 2.0, 3.0);
        string doc = writer.BuildDocument("d", "f.ifc", "IFC4");

        Assert.Contains("=IFCTESTENTITY(1.,2.,3.);", doc);
    }

    [Fact]
    public void BuildDocument_HasValidStepEnvelope()
    {
        var writer = new IfcStepWriter();
        writer.Write("IFCTESTENTITY", 1);
        string doc = writer.BuildDocument("desc", "f.ifc", "IFC4");

        Assert.StartsWith("ISO-10303-21;", doc);
        Assert.EndsWith("END-ISO-10303-21;", doc);
        Assert.Contains("FILE_SCHEMA(('IFC4'));", doc);
    }
}

public class IfcProjectExporterTests
{
    private static Project BuildSampleProject()
    {
        var family = new Family { Name = "Gaine rectangulaire", Category = "duct" };
        var type = family.AddType("Generique");
        var duct = new MepDuct("Troncon test", type, DuctShape.Rectangular, lengthM: 5.0);
        duct.ResizeRectangular(0.8, 0.4);

        var level = new Level { Name = "RDC", ElevationMeters = 0.0, HeightMeters = 3.0 };
        duct.Level = level;

        var project = new Project { Name = "Projet Test" };
        project.Levels.Add(level);
        project.Elements.Add(duct);
        return project;
    }

    [Fact]
    public void Export_ProducesValidStepEnvelope()
    {
        string ifc = IfcProjectExporter.Export(BuildSampleProject());

        Assert.StartsWith("ISO-10303-21;", ifc);
        Assert.EndsWith("END-ISO-10303-21;", ifc);
        Assert.Contains("FILE_SCHEMA(('IFC4'));", ifc);
        Assert.Contains("IFCPROJECT(", ifc);
        Assert.Contains("IFCDUCTSEGMENT(", ifc);
        Assert.Contains("IFCBUILDINGSTOREY(", ifc);
    }

    [Fact]
    public void Export_AllInstanceIdsAreUniqueAndSequential()
    {
        string ifc = IfcProjectExporter.Export(BuildSampleProject());
        var ids = Regex.Matches(ifc, @"^#(\d+)=", RegexOptions.Multiline)
            .Select(m => int.Parse(m.Groups[1].Value))
            .ToList();

        Assert.NotEmpty(ids);
        Assert.Equal(ids.Distinct().Count(), ids.Count);
        Assert.Equal(Enumerable.Range(1, ids.Count), ids);
    }

    [Fact]
    public void Export_NoDanglingReferences()
    {
        string ifc = IfcProjectExporter.Export(BuildSampleProject());
        var definedIds = Regex.Matches(ifc, @"^#(\d+)=", RegexOptions.Multiline)
            .Select(m => int.Parse(m.Groups[1].Value)).ToHashSet();

        // Toute reference "#N" apparaissant dans une ligne de donnees doit correspondre a un id defini
        // quelque part dans le fichier (aucune reference orpheline) — verification structurelle
        // independante d'IfcOpenShell (docs §13, la validation semantique complete reste externe).
        var dataSection = ifc[(ifc.IndexOf("DATA;", StringComparison.Ordinal) + 5)..];
        var referencedIds = Regex.Matches(dataSection, @"#(\d+)")
            .Select(m => int.Parse(m.Groups[1].Value)).ToHashSet();

        Assert.All(referencedIds, id => Assert.Contains(id, definedIds));
    }

    [Fact]
    public void Export_DuctGlobalId_Is22CharacterIfcGuid()
    {
        var project = BuildSampleProject();
        var duct = (MepDuct)project.Elements[0];

        string ifc = IfcProjectExporter.Export(project);

        Assert.Contains(duct.IfcGuid, ifc);
        Assert.Equal(22, duct.IfcGuid.Length);
    }

    [Fact]
    public void Export_ElementWithoutLevel_FallsBackToDefaultStorey()
    {
        var family = new Family { Name = "Gaine rectangulaire", Category = "duct" };
        var type = family.AddType("Generique");
        var duct = new MepDuct("Sans niveau", type, DuctShape.Rectangular, lengthM: 2.0);
        duct.ResizeRectangular(0.5, 0.3);

        var project = new Project { Name = "Projet sans niveaux" };
        project.Elements.Add(duct); // aucun Level assigne, aucun Level ajoute au projet

        string ifc = IfcProjectExporter.Export(project);

        Assert.Contains("IFCBUILDINGSTOREY(", ifc);
        Assert.Contains("IFCRELCONTAINEDINSPATIALSTRUCTURE(", ifc);
    }

    [Fact]
    public void Export_UnsupportedElementCategory_IsSkippedWithoutError()
    {
        var project = BuildSampleProject();
        project.Elements.Add(new UnsupportedElement("Mur non gere"));

        var exception = Record.Exception(() => IfcProjectExporter.Export(project));

        Assert.Null(exception);
    }

    private sealed class UnsupportedElement : BimElement
    {
        public UnsupportedElement(string name) : base(name) { }
        public override string GetIfcType() => "IfcWall";
    }
}
