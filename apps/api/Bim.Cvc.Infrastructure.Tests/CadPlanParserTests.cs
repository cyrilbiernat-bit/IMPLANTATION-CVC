using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Tables;
using Bim.Cvc.Domain;
using CSMath;
using Xunit;

namespace Bim.Cvc.Infrastructure.Tests;

public sealed class CadPlanParserTests
{
    private static CadDocument BuildSampleDocument()
    {
        var doc = new CadDocument();

        doc.ModelSpace.Entities.Add(new Line { StartPoint = new XYZ(0, 0, 0), EndPoint = new XYZ(10, 0, 0) });
        doc.ModelSpace.Entities.Add(new Circle { Center = new XYZ(5, 5, 0), Radius = 2 });
        doc.ModelSpace.Entities.Add(new Arc { Center = new XYZ(0, 0, 0), Radius = 3, StartAngle = 0, EndAngle = Math.PI / 2 });
        doc.ModelSpace.Entities.Add(new LwPolyline
        {
            IsClosed = true,
            Vertices =
            {
                new LwPolyline.Vertex(new XY(0, 0)),
                new LwPolyline.Vertex(new XY(4, 0)),
                new LwPolyline.Vertex(new XY(4, 4)),
            },
        });
        doc.ModelSpace.Entities.Add(new MText { InsertPoint = new XYZ(1, 1, 0), Value = "CTA-01", Height = 2.5 });

        var symbol = new BlockRecord("DIFFUSEUR");
        symbol.Entities.Add(new Line { StartPoint = new XYZ(0, 0, 0), EndPoint = new XYZ(1, 0, 0) });
        doc.BlockRecords.Add(symbol);
        doc.ModelSpace.Entities.Add(new Insert(symbol)
        {
            InsertPoint = new XYZ(100, 50, 0),
            Rotation = Math.PI / 2,
            XScale = 2,
        });

        return doc;
    }

    private static IReadOnlyList<PlanEntity> ParseAsDxf(CadDocument doc)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.dxf");
        try
        {
            DxfWriter.Write(path, doc, false, null);
            using var stream = File.OpenRead(path);
            var result = new CadPlanParser().Parse(PlanFormat.Dxf, stream);
            return result.Entities!;
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static IReadOnlyList<PlanEntity> ParseAsDwg(CadDocument doc)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.dwg");
        try
        {
            DwgWriter.Write(path, doc, null);
            using var stream = File.OpenRead(path);
            var result = new CadPlanParser().Parse(PlanFormat.Dwg, stream);
            return result.Entities!;
        }
        finally
        {
            File.Delete(path);
        }
    }

    public static IEnumerable<object[]> BothFormats()
    {
        yield return [(Func<CadDocument, IReadOnlyList<PlanEntity>>)ParseAsDxf];
        yield return [(Func<CadDocument, IReadOnlyList<PlanEntity>>)ParseAsDwg];
    }

    [Theory]
    [MemberData(nameof(BothFormats))]
    public void Parse_extracts_every_supported_entity_type(Func<CadDocument, IReadOnlyList<PlanEntity>> parse)
    {
        var entities = parse(BuildSampleDocument());

        Assert.Contains(entities, e => e is PlanLine);
        Assert.Contains(entities, e => e is PlanCircle);
        Assert.Contains(entities, e => e is PlanArc);
        Assert.Contains(entities, e => e is PlanPolyline);
        Assert.Contains(entities, e => e is PlanText);
    }

    [Theory]
    [MemberData(nameof(BothFormats))]
    public void Parse_reads_circle_center_and_radius(Func<CadDocument, IReadOnlyList<PlanEntity>> parse)
    {
        var circle = Assert.IsType<PlanCircle>(parse(BuildSampleDocument()).Single(e => e is PlanCircle));

        Assert.Equal(5, circle.Center.X, precision: 3);
        Assert.Equal(5, circle.Center.Y, precision: 3);
        Assert.Equal(2, circle.Radius, precision: 3);
    }

    [Theory]
    [MemberData(nameof(BothFormats))]
    public void Parse_reads_arc_angles_in_radians(Func<CadDocument, IReadOnlyList<PlanEntity>> parse)
    {
        var arc = Assert.IsType<PlanArc>(parse(BuildSampleDocument()).Single(e => e is PlanArc));

        Assert.Equal(0, arc.StartAngleRad, precision: 3);
        Assert.Equal(Math.PI / 2, arc.EndAngleRad, precision: 3);
    }

    [Theory]
    [MemberData(nameof(BothFormats))]
    public void Parse_reads_closed_polyline_vertices(Func<CadDocument, IReadOnlyList<PlanEntity>> parse)
    {
        var polyline = Assert.IsType<PlanPolyline>(parse(BuildSampleDocument()).Single(e => e is PlanPolyline));

        Assert.True(polyline.Closed);
        Assert.Equal(3, polyline.Points.Count);
        Assert.Equal(4, polyline.Points[2].X, precision: 3);
        Assert.Equal(4, polyline.Points[2].Y, precision: 3);
    }

    [Theory]
    [MemberData(nameof(BothFormats))]
    public void Parse_reads_mtext_plain_text(Func<CadDocument, IReadOnlyList<PlanEntity>> parse)
    {
        var text = Assert.IsType<PlanText>(parse(BuildSampleDocument()).Single(e => e is PlanText));

        Assert.Equal("CTA-01", text.Value);
    }

    [Theory]
    [MemberData(nameof(BothFormats))]
    public void Parse_flattens_block_inserts_applying_translation_rotation_and_scale(
        Func<CadDocument, IReadOnlyList<PlanEntity>> parse)
    {
        // Ligne de bloc (0,0)->(1,0), insérée en (100,50), tournée de 90°, à l'échelle 2
        // -> attendu (100,50)->(100,52).
        var entities = parse(BuildSampleDocument());
        var line = entities.OfType<PlanLine>().Single(l => l.Start.X > 50);

        Assert.Equal(100, line.Start.X, precision: 2);
        Assert.Equal(50, line.Start.Y, precision: 2);
        Assert.Equal(100, line.End.X, precision: 2);
        Assert.Equal(52, line.End.Y, precision: 2);
    }
}
