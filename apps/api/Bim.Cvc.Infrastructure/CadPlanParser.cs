using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using Bim.Cvc.Application;
using Bim.Cvc.Domain;

namespace Bim.Cvc.Infrastructure;

/// <summary>
/// Lit un plan DXF ou DWG (AutoCAD R14 à 2018+) via ACadSharp — une
/// bibliothèque MIT indépendante d'Autodesk/ODA, sans conversion externe.
/// Seules les entités utiles à un plan CVC en vue de dessus sont extraites
/// (lignes, polylignes, cercles, arcs, textes), en aplatissant les
/// références de bloc (INSERT) ; les arcs de polyligne (bulge) sont
/// approximés par des segments droits. Le modèle 3D / les solides ne sont
/// pas exploités — ce plan reste un fond de référence, comme le PDF.
/// </summary>
public sealed class CadPlanParser : IPlanFileParser
{
    public bool CanParse(PlanFormat format) => format is PlanFormat.Dxf or PlanFormat.Dwg;

    public ParsedPlan Parse(PlanFormat format, Stream content)
    {
        content.Position = 0;
        CadDocument document = format switch
        {
            PlanFormat.Dxf => DxfReader.Read(content, null),
            PlanFormat.Dwg => DwgReader.Read(content, null),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Format non pris en charge par CadPlanParser."),
        };

        var entities = new List<PlanEntity>();
        CollectEntities(document.ModelSpace.Entities, entities, Transform2D.Identity, depth: 0);
        return new ParsedPlan(PageCount: 1, entities);
    }

    private const int MaxBlockNestingDepth = 8;

    private static void CollectEntities(IEnumerable<Entity> source, List<PlanEntity> sink, Transform2D transform, int depth)
    {
        if (depth > MaxBlockNestingDepth)
        {
            return; // bloc imbriqué de façon anormale/cyclique — on arrête plutôt que de boucler.
        }

        foreach (var entity in source)
        {
            switch (entity)
            {
                case Arc arc: // Arc hérite de Circle : à tester avant le cas Circle.
                    sink.Add(new PlanArc(
                        transform.Apply(arc.Center.X, arc.Center.Y),
                        arc.Radius * transform.Scale,
                        arc.StartAngle + transform.RotationRad,
                        arc.EndAngle + transform.RotationRad));
                    break;

                case Circle circle:
                    sink.Add(new PlanCircle(transform.Apply(circle.Center.X, circle.Center.Y), circle.Radius * transform.Scale));
                    break;

                case Line line:
                    sink.Add(new PlanLine(
                        transform.Apply(line.StartPoint.X, line.StartPoint.Y),
                        transform.Apply(line.EndPoint.X, line.EndPoint.Y)));
                    break;

                case LwPolyline polyline:
                    sink.Add(new PlanPolyline(
                        polyline.Vertices.Select(v => transform.Apply(v.Location.X, v.Location.Y)).ToList(),
                        polyline.IsClosed));
                    break;

                case TextEntity text:
                    sink.Add(new PlanText(transform.Apply(text.InsertPoint.X, text.InsertPoint.Y), text.Value, text.Height * transform.Scale));
                    break;

                case MText mtext:
                    sink.Add(new PlanText(transform.Apply(mtext.InsertPoint.X, mtext.InsertPoint.Y), mtext.PlainText, mtext.Height * transform.Scale));
                    break;

                case Insert insert when insert.Block is not null:
                    var blockTransform = transform.Combine(insert.InsertPoint.X, insert.InsertPoint.Y, insert.Rotation, insert.XScale);
                    CollectEntities(insert.Block.Entities, sink, blockTransform, depth + 1);
                    break;
            }
        }
    }
}
