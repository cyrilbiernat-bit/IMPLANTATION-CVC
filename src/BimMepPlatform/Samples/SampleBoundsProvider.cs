using BimMep.Core.Bim;
using BimMep.Core.ClashDetection;
using BimMep.Core.Geometry;
using BimMep.Core.Mep;

namespace BimMep.Samples;

/// <summary>
/// Implementation illustrative de IElementBoundsProvider : calcule une boite englobante a partir du
/// placement et des dimensions connues de chaque type d'element. Un moteur de production delegue ce
/// calcul au kernel geometrique (Core.Geometry / libbimgeo, docs §15.1-15.2) a partir du BRep reel.
/// Convention : chaque element est oriente le long de l'axe X sur sa longueur.
/// </summary>
public sealed class SampleBoundsProvider : IElementBoundsProvider
{
    public AxisAlignedBox GetBounds(BimElement element)
    {
        var origin = element.Placement.Origin;

        return element switch
        {
            MepDuct duct => BoxAlongX(origin, duct.LengthM, HalfWidth(duct), HalfHeight(duct)),
            StructuralBeamStub beam => BoxAlongX(origin, beam.LengthM, beam.WidthM / 2, beam.HeightM / 2),
            MepPipe pipe => BoxAlongX(origin, pipe.LengthM, pipe.DiameterNominalM / 2, pipe.DiameterNominalM / 2),
            _ => AxisAlignedBox.FromCenterExtent(origin, 0.1, 0.1, 0.1)
        };
    }

    private static double HalfWidth(MepDuct duct) =>
        duct.Shape == DuctShape.Rectangular ? duct.WidthM / 2 : duct.DiameterM / 2;

    private static double HalfHeight(MepDuct duct) =>
        duct.Shape == DuctShape.Rectangular ? duct.HeightM / 2 : duct.DiameterM / 2;

    private static AxisAlignedBox BoxAlongX(Point3D origin, double lengthM, double halfWidth, double halfHeight)
    {
        var center = new Point3D(origin.X + lengthM / 2, origin.Y, origin.Z);
        return AxisAlignedBox.FromCenterExtent(center, lengthM / 2, halfHeight, halfWidth);
    }
}
