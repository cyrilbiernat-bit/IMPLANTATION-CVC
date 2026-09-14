using Bim.Cvc.Application;
using Bim.Cvc.Infrastructure;
using Microsoft.Extensions.Options;
using Xunit;

namespace Bim.Cvc.Infrastructure.Tests;

/// <summary>
/// Vérifie l'extraction IFC contre un vrai fichier .ifc (généré une fois
/// via IfcOpenShell lui-même, voir Fixtures/sample-building.ifc) et un
/// vrai sous-processus Python — pas de mock : ce test échoue si Python ou
/// le paquet "ifcopenshell" ne sont pas installés sur la machine qui
/// exécute les tests, ce qui est le comportement voulu (c'est exactement
/// la dépendance nécessaire en production).
/// </summary>
public sealed class IfcOpenShellGeometryExtractorTests
{
    private static IfcOpenShellGeometryExtractor CreateSut() =>
        new(Options.Create(new IfcExtractionOptions()));

    private static string FixturePath => Path.Combine(AppContext.BaseDirectory, "Fixtures", "sample-building.ifc");

    [Fact]
    public async Task ExtractAsync_returns_triangulated_geometry_for_a_real_ifc_file()
    {
        var sut = CreateSut();

        var elements = await sut.ExtractAsync(FixturePath);

        Assert.NotEmpty(elements);
        Assert.All(elements, e => Assert.Equal("IfcWall", e.IfcType));
        Assert.All(elements, e => Assert.NotEmpty(e.Positions));
        Assert.All(elements, e => Assert.NotEmpty(e.Indices));

        var wall = elements[0];
        // Un maillage triangulé valide : un multiple de 3 sommets (x,y,z) et un multiple de 3 indices (triangles).
        Assert.Equal(0, wall.Positions.Count % 3);
        Assert.Equal(0, wall.Indices.Count % 3);
        Assert.All(wall.Indices, i => Assert.InRange(i, 0, wall.Positions.Count / 3 - 1));
    }

    [Fact]
    public async Task ExtractAsync_throws_a_domain_exception_for_an_invalid_file()
    {
        var sut = CreateSut();
        var badPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.ifc");
        await File.WriteAllTextAsync(badPath, "ceci n'est pas un fichier IFC");

        try
        {
            await Assert.ThrowsAsync<InvalidBuildingModelException>(() => sut.ExtractAsync(badPath));
        }
        finally
        {
            File.Delete(badPath);
        }
    }
}
