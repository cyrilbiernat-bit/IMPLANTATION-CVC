using BimMep.Core.Bim;
using BimMep.Core.ClashDetection;
using BimMep.Core.Geometry;
using BimMep.Core.Ifc;
using BimMep.Core.Mep;
using BimMep.Core.Routing;
using BimMep.Core.Takeoff;
using BimMep.Samples;

Console.WriteLine("=== BimMep Platform — demonstration des modules critiques ===\n");

// ---------------------------------------------------------------------------
// 1) Moteur BIM : redimensionnement parametrique 800x400 -> 1000x500 (docs §5.4)
// ---------------------------------------------------------------------------
Console.WriteLine("[1] Moteur BIM — recalcul parametrique en cascade");

var ductFamily = new Family { Name = "Gaine rectangulaire", Category = "duct" };
var ductType = ductFamily.AddType("Gaine rect. galva - generique");

var duct1 = new MepDuct("Troncon CTA-01", ductType, DuctShape.Rectangular, lengthM: 6.0);
duct1.ResizeRectangular(0.8, 0.4);
var duct1Outlet = duct1.AddConnector(new Point3D(6, 0, 3), new Vector3D(1, 0, 0), SystemClassification.SupplyAir);

var duct2 = new MepDuct("Troncon distribution-02", ductType, DuctShape.Rectangular, lengthM: 4.0);
duct2.ResizeRectangular(0.8, 0.4);
var duct2Inlet = duct2.AddConnector(new Point3D(0, 0, 3), new Vector3D(-1, 0, 0), SystemClassification.SupplyAir);
duct1Outlet.ConnectTo(duct2Inlet);

// Recalcul initial (etat de reference, aucun avertissement attendu : les deux troncons ont la meme section)
var scheduler = new RecomputeScheduler();
var elements = new Dictionary<Guid, IRecomputable> { [duct1.Id] = duct1, [duct2.Id] = duct2 };
duct2.AddDependency(duct1); // duct2 depend de duct1 (connecte en aval) -> recalcule apres lui
duct1.RegisterDependent(duct2); // exemple simplifie ; le reseau complet (docs §5.4) enregistre ces liens a la connexion

var initialReport = scheduler.RunFrom(new IRecomputable[] { duct1 }, elements);
Console.WriteLine($"  Recalcul initial : {initialReport.RecomputedInOrder.Count} element(s), " +
                   $"{initialReport.Warnings.Count} avertissement(s).");

// Modification : 800x400 -> 1000x500 sur le premier troncon seulement -> discontinuite avec duct2
Console.WriteLine("  Redimensionnement de 'Troncon CTA-01' : 800x400 mm -> 1000x500 mm");
duct1.ResizeRectangular(1.0, 0.5);
var resizeReport = scheduler.RunFrom(new IRecomputable[] { duct1 }, elements);
foreach (var warning in resizeReport.Warnings)
    Console.WriteLine($"  ⚠ {warning}");

Console.WriteLine();

// ---------------------------------------------------------------------------
// 2) Moteur de routage : A* + comparaison de variantes (docs §7.3.1, §16 P1)
// ---------------------------------------------------------------------------
Console.WriteLine("[2] Moteur de routage — A* + optimisation de section");

var routingGraph = new RoutingGraph3D(cellSizeM: 0.5);
routingGraph.BuildGrid(new AxisAlignedBox(new Point3D(0, 0, 0), new Point3D(10, 6, 4)));
routingGraph.AddObstacle(new AxisAlignedBox(new Point3D(4, 0, 0), new Point3D(5, 6, 3))); // poteau traversant

var routingService = new RoutingService(new AStarPathFinder());
var constraints = new RoutingConstraints { MaxPressureLossPa = 50.0 };

var path = routingService.RouteSegment(routingGraph, new Point3D(0.5, 3, 3), new Point3D(9.5, 3, 3), constraints);
Console.WriteLine($"  Trace trouve : {path.Waypoints.Count} points, longueur {path.TotalLength:F2} m (evite l'obstacle).");

var variants = routingService.OptimizeForWeight(
    routingGraph, new Point3D(0.5, 3, 3), new Point3D(9.5, 3, 3), constraints,
    designFlowM3H: 20_000,
    candidateSections: new (double, double)[] { (0.6, 0.3), (0.8, 0.4), (1.0, 0.5) });

foreach (var variant in variants)
{
    Console.WriteLine($"  Variante {variant.Label} : poids ~{variant.EstimatedWeightKg:F0} kg, " +
                       $"perte de charge ~{variant.EstimatedPressureLossPa:F1} Pa");
}

Console.WriteLine();

// ---------------------------------------------------------------------------
// 3) Clash detection : "Gaine VS Poutre -> decalage automatique" (docs §2.2)
// ---------------------------------------------------------------------------
Console.WriteLine("[3] Clash Detection — decalage automatique gaine/poutre");

var beam = new StructuralBeamStub("Poutre B-12") { WidthM = 0.3, HeightM = 0.5, LengthM = 8.0 };
beam.Placement = new Transform3D(new Point3D(2, 2.9, 2.8), 0); // traverse le passage de la gaine en Y/Z

var duct3 = new MepDuct("Troncon en conflit", ductType, DuctShape.Rectangular, lengthM: 8.0);
duct3.ResizeRectangular(0.8, 0.4);
duct3.Placement = new Transform3D(new Point3D(0, 3, 3.0), 0); // traverse la poutre en X

var boundsProvider = new SampleBoundsProvider();
var clashDetector = new ClashDetector(boundsProvider);
var allElementsForClash = new List<BimElement> { beam, duct3 };
var clashes = clashDetector.DetectClashes(allElementsForClash);

Console.WriteLine($"  {clashes.Count} conflit(s) detecte(s).");

var clashResolver = new ClashResolver(scheduler);
var recomputableElements = new Dictionary<Guid, IRecomputable> { [duct3.Id] = duct3, [beam.Id] = beam };

foreach (var clash in clashes)
{
    var resolution = clashResolver.ProposeResolution(clash);
    Console.WriteLine($"  Conflit {clash.Severity} : {resolution.Rationale}");

    if (resolution.Strategy != ClashResolutionStrategy.ManualReview)
    {
        var report = clashResolver.ApplyResolution(resolution, recomputableElements);
        Console.WriteLine($"  -> Resolution appliquee, {report.RecomputedInOrder.Count} element(s) recalcule(s).");
    }
}

Console.WriteLine();

// ---------------------------------------------------------------------------
// 4) Export IFC4 : hierarchie spatiale + elements MEP (docs §6, §16 P0)
// ---------------------------------------------------------------------------
Console.WriteLine("[4] Export IFC4 — modele complet vers fichier .ifc");

var level0 = new Level { Name = "RDC", ElevationMeters = 0.0, HeightMeters = 3.0 };
duct1.Level = level0;
duct2.Level = level0;
duct3.Level = level0;
duct1.InsulationThicknessM = 0.03; // calorifuge 30 mm sur le troncon CTA-01, pour la demo metres (§5)

var pipe1 = new MepPipe("Colonne EU-1", null, SystemClassification.WasteEu, lengthM: 12.0) { DiameterNominalM = 0.1 };
pipe1.Level = level0;

var cableTrayFamily = new Family { Name = "Chemin de cables", Category = "cable_tray" };
var tray1 = new CableTray("CdC-CFO-1", cableTrayFamily.AddType("Generique")) { WidthM = 0.3, HeightM = 0.1, LengthM = 15.0 };
tray1.Level = level0;

var project = new Project { Name = "Demonstration BimMepPlatform" };
project.Levels.Add(level0);
project.Elements.Add(duct1);
project.Elements.Add(duct2);
project.Elements.Add(duct3);
project.Elements.Add(pipe1);
project.Elements.Add(tray1);

string ifcText = IfcProjectExporter.Export(project);
string outputPath = Path.Combine(AppContext.BaseDirectory, "bimmep-demo.ifc");
File.WriteAllText(outputPath, ifcText);

Console.WriteLine($"  Fichier ecrit : {outputPath}");
Console.WriteLine($"  {ifcText.Split('\n').Length} lignes STEP, " +
                   $"{ifcText.Split("IFCDUCTSEGMENT").Length - 1} IfcDuctSegment exporte(s).");

Console.WriteLine();

// ---------------------------------------------------------------------------
// 5) Metres automatiques : nomenclature + export CSV (docs F-TAKEOFF-01/02, §16 P3)
// ---------------------------------------------------------------------------
Console.WriteLine("[5] Metres automatiques — nomenclature du projet");

var takeoff = TakeoffService.GenerateNomenclature(project);
foreach (var row in takeoff.Rows)
{
    Console.WriteLine($"  {row.Category,-16} {row.Label,-14} {row.System ?? "-",-14} " +
                       $"x{row.Count} L={row.TotalLengthM:F1}m P={row.TotalWeightKg:F1}kg " +
                       $"Calorifuge={row.TotalInsulationAreaM2:F1}m2");
}
Console.WriteLine($"  Total : {takeoff.GrandTotalLengthM:F1} m, {takeoff.GrandTotalWeightKg:F1} kg, " +
                   $"{takeoff.GrandTotalInsulationAreaM2:F1} m2 de calorifuge.");

string csvPath = Path.Combine(AppContext.BaseDirectory, "bimmep-nomenclature.csv");
File.WriteAllText(csvPath, TakeoffService.ExportCsv(takeoff));
Console.WriteLine($"  Nomenclature CSV ecrite : {csvPath}");

Console.WriteLine("\n=== Fin de la demonstration ===");
