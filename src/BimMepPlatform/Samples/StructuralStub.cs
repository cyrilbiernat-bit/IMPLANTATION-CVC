using BimMep.Core.Bim;

namespace BimMep.Samples;

/// <summary>
/// Les entites architecturales/structure (IfcBeam, IfcWall, ...) ne sont pas implementees dans ce
/// dossier d'exemples (perimetre MEP, cf. docs §17). Ce stub minimal sert uniquement a demontrer le
/// scenario "Gaine VS Poutre" du clash detection (docs §2.2 / cahier des charges module 5).
/// </summary>
public sealed class StructuralBeamStub : BimElement
{
    public double WidthM { get; init; }
    public double HeightM { get; init; }
    public double LengthM { get; init; }

    public StructuralBeamStub(string name) : base(name) { }

    public override string GetIfcType() => "IfcBeam";
}
