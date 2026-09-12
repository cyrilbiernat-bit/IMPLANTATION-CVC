using Bim.Cvc.Domain;
using Xunit;

namespace Bim.Cvc.Application.Tests;

public sealed class DrawingServiceCalibrationTests
{
    private static (DrawingService sut, Drawing drawing) CreateCalibratedContext(int nbPages = 1)
    {
        var repository = new FakeRepository();
        var projects = new ProjectService(new FakeProjectRepository());
        var sut = new DrawingService(new FakeFileStore(), repository, [], projects);
        var drawing = new Drawing { ProjectId = Guid.NewGuid(), FileName = "plan.pdf", StoragePath = "memory://x", NbPages = nbPages };
        repository.Add(drawing);
        return (sut, drawing);
    }

    [Fact]
    public void Calibrate_computes_meters_per_pixel_from_a_known_distance()
    {
        var (sut, drawing) = CreateCalibratedContext();

        // 200 px de large pour un mur de 10 m -> 0,05 m/px.
        var result = sut.Calibrate(drawing.Id, pageNumber: 1, new CalibrationPoint(0, 0), new CalibrationPoint(200, 0), realDistanceMeters: 10);

        Assert.NotNull(result.Calibration);
        Assert.Equal(0.05, result.Calibration!.MetersPerPixel, precision: 6);
    }

    [Fact]
    public void Calibrate_throws_when_the_drawing_does_not_exist()
    {
        var (sut, _) = CreateCalibratedContext();

        Assert.Throws<DrawingNotFoundException>(() =>
            sut.Calibrate(Guid.NewGuid(), 1, new CalibrationPoint(0, 0), new CalibrationPoint(10, 0), 5));
    }

    [Fact]
    public void Calibrate_rejects_a_page_number_outside_the_document()
    {
        var (sut, drawing) = CreateCalibratedContext(nbPages: 2);

        Assert.Throws<InvalidDrawingException>(() =>
            sut.Calibrate(drawing.Id, pageNumber: 3, new CalibrationPoint(0, 0), new CalibrationPoint(10, 0), 5));
    }

    [Fact]
    public void Calibrate_rejects_two_identical_points()
    {
        var (sut, drawing) = CreateCalibratedContext();

        Assert.Throws<InvalidDrawingException>(() =>
            sut.Calibrate(drawing.Id, 1, new CalibrationPoint(5, 5), new CalibrationPoint(5, 5), 10));
    }

    [Fact]
    public void Calibrate_rejects_a_non_positive_distance()
    {
        var (sut, drawing) = CreateCalibratedContext();

        Assert.Throws<InvalidDrawingException>(() =>
            sut.Calibrate(drawing.Id, 1, new CalibrationPoint(0, 0), new CalibrationPoint(10, 0), 0));
    }

    [Fact]
    public void Recalibrating_replaces_the_previous_calibration()
    {
        var (sut, drawing) = CreateCalibratedContext();
        sut.Calibrate(drawing.Id, 1, new CalibrationPoint(0, 0), new CalibrationPoint(200, 0), 10);

        var result = sut.Calibrate(drawing.Id, 1, new CalibrationPoint(0, 0), new CalibrationPoint(100, 0), 10);

        Assert.Equal(0.1, result.Calibration!.MetersPerPixel, precision: 6);
    }

    private sealed class FakeFileStore : IDrawingFileStore
    {
        public Task<string> SaveAsync(Guid drawingId, string fileName, Stream content, CancellationToken ct = default)
            => Task.FromResult($"memory://{drawingId}");

        public Task<Stream> OpenReadAsync(string storagePath, CancellationToken ct = default)
            => Task.FromResult<Stream>(new MemoryStream());
    }

    private sealed class FakeRepository : IDrawingRepository
    {
        private readonly Dictionary<Guid, Drawing> _drawings = [];

        public void Add(Drawing drawing) => _drawings[drawing.Id] = drawing;

        public Drawing? Get(Guid id) => _drawings.GetValueOrDefault(id);

        public IReadOnlyList<Drawing> GetByProject(Guid projectId) =>
            _drawings.Values.Where(d => d.ProjectId == projectId).ToList();
    }

    private sealed class FakeProjectRepository : IProjectRepository
    {
        private readonly Dictionary<Guid, Project> _projects = [];

        public void Add(Project project) => _projects[project.Id] = project;

        public Project? Get(Guid id) => _projects.GetValueOrDefault(id);

        public IReadOnlyList<Project> GetAll() => _projects.Values.ToList();
    }
}
