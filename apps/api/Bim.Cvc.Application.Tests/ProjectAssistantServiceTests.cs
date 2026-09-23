using Bim.Cvc.Domain;
using Xunit;

namespace Bim.Cvc.Application.Tests;

public sealed class ProjectAssistantServiceTests
{
    private sealed record Sut(
        ProjectAssistantService Assistant,
        ProjectService Projects,
        FakeDrawingRepository Drawings,
        CvcObjectService CvcObjects,
        LayerService Layers,
        FakeChatClient ChatClient);

    private static Sut CreateSut()
    {
        var projects = new ProjectService(new FakeProjectRepository());
        var drawings = new FakeDrawingRepository();
        var layerRepository = new FakeLayerRepository();
        var objectRepository = new FakeCvcObjectRepository();
        var layers = new LayerService(drawings, layerRepository, objectRepository);
        var cvcObjects = new CvcObjectService(drawings, objectRepository, layers);
        var metres = new ProjectMetresService(projects, drawings, objectRepository, cvcObjects);
        var nomenclature = new ProjectNomenclatureService(projects, drawings, objectRepository, cvcObjects, layers);
        var chatClient = new FakeChatClient();
        var assistant = new ProjectAssistantService(projects, metres, nomenclature, chatClient);
        return new Sut(assistant, projects, drawings, cvcObjects, layers, chatClient);
    }

    private static Drawing AddCalibratedDrawing(Sut sut, Guid projectId, string fileName = "plan.pdf")
    {
        var drawing = new Drawing { ProjectId = projectId, FileName = fileName, StoragePath = "memory://x", NbPages = 1 };
        drawing.Calibration = Calibration.Create(1, new CalibrationPoint(0, 0), new CalibrationPoint(100, 0), 10);
        sut.Drawings.Add(drawing);
        return drawing;
    }

    [Fact]
    public async Task AskAsync_throws_for_an_unknown_project()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<ProjectNotFoundException>(() => sut.Assistant.AskAsync(Guid.NewGuid(), [], "Bonjour ?"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AskAsync_rejects_an_empty_question(string question)
    {
        var sut = CreateSut();
        var project = sut.Projects.Create("Tour");

        await Assert.ThrowsAsync<ArgumentException>(() => sut.Assistant.AskAsync(project.Id, [], question));
    }

    [Fact]
    public async Task AskAsync_returns_the_chat_client_answer()
    {
        var sut = CreateSut();
        var project = sut.Projects.Create("Tour");
        sut.ChatClient.NextAnswer = "La longueur totale est de 5 mètres.";

        var answer = await sut.Assistant.AskAsync(project.Id, [], "Quelle est la longueur totale ?");

        Assert.Equal("La longueur totale est de 5 mètres.", answer);
    }

    [Fact]
    public async Task AskAsync_includes_project_name_and_metres_in_the_system_prompt()
    {
        var sut = CreateSut();
        var project = sut.Projects.Create("Réhabilitation Tour A");
        var drawing = AddCalibratedDrawing(sut, project.Id, "reseau.pdf");
        var layer = sut.Layers.CreateDefault(drawing.Id);
        sut.CvcObjects.AddDuct(
            drawing.Id, layer.Id, CvcObjectType.GaineCirculaire,
            new Point2D(0, 0), new Point2D(50, 0), null, null, 315,
            debitM3h: 500, vitesseMs: 4.5, pressionPa: 120);

        await sut.Assistant.AskAsync(project.Id, [], "Quel est le débit ?");

        Assert.Contains("Réhabilitation Tour A", sut.ChatClient.LastSystemPrompt);
        Assert.Contains("5", sut.ChatClient.LastSystemPrompt); // longueur totale (m)
        Assert.Contains("reseau.pdf", sut.ChatClient.LastSystemPrompt);
        Assert.Contains("débit 500", sut.ChatClient.LastSystemPrompt);
    }

    [Fact]
    public async Task AskAsync_appends_the_question_after_the_supplied_history()
    {
        var sut = CreateSut();
        var project = sut.Projects.Create("Tour");
        var history = new List<AssistantMessage>
        {
            new("user", "Combien de plans ?"),
            new("assistant", "Aucun plan pour l'instant."),
        };

        await sut.Assistant.AskAsync(project.Id, history, "Et maintenant ?");

        Assert.Equal(3, sut.ChatClient.LastMessages!.Count);
        Assert.Equal("Combien de plans ?", sut.ChatClient.LastMessages[0].Content);
        Assert.Equal("Aucun plan pour l'instant.", sut.ChatClient.LastMessages[1].Content);
        Assert.Equal("user", sut.ChatClient.LastMessages[2].Role);
        Assert.Equal("Et maintenant ?", sut.ChatClient.LastMessages[2].Content);
    }

    [Fact]
    public async Task AskAsync_propagates_chat_client_unavailability()
    {
        var sut = CreateSut();
        var project = sut.Projects.Create("Tour");
        sut.ChatClient.Failure = new AssistantUnavailableException("clé API manquante");

        var ex = await Assert.ThrowsAsync<AssistantUnavailableException>(
            () => sut.Assistant.AskAsync(project.Id, [], "Bonjour ?"));
        Assert.Equal("clé API manquante", ex.Message);
    }

    private sealed class FakeChatClient : IAssistantChatClient
    {
        public string NextAnswer { get; set; } = "";
        public Exception? Failure { get; set; }
        public string? LastSystemPrompt { get; private set; }
        public IReadOnlyList<AssistantMessage>? LastMessages { get; private set; }

        public Task<string> AskAsync(string systemPrompt, IReadOnlyList<AssistantMessage> messages, CancellationToken ct = default)
        {
            LastSystemPrompt = systemPrompt;
            LastMessages = messages;
            return Failure is not null ? Task.FromException<string>(Failure) : Task.FromResult(NextAnswer);
        }
    }

    private sealed class FakeProjectRepository : IProjectRepository
    {
        private readonly Dictionary<Guid, Project> _projects = [];

        public void Add(Project project) => _projects[project.Id] = project;

        public Project? Get(Guid id) => _projects.GetValueOrDefault(id);

        public IReadOnlyList<Project> GetAll() => _projects.Values.ToList();
    }

    private sealed class FakeDrawingRepository : IDrawingRepository
    {
        private readonly Dictionary<Guid, Drawing> _drawings = [];

        public void Add(Drawing drawing) => _drawings[drawing.Id] = drawing;

        public Drawing? Get(Guid id) => _drawings.GetValueOrDefault(id);

        public IReadOnlyList<Drawing> GetByProject(Guid projectId) =>
            _drawings.Values.Where(d => d.ProjectId == projectId).ToList();
    }

    private sealed class FakeCvcObjectRepository : ICvcObjectRepository
    {
        private readonly Dictionary<Guid, CvcObject> _objects = [];

        public void Add(CvcObject cvcObject) => _objects[cvcObject.Id] = cvcObject;

        public CvcObject? Get(Guid id) => _objects.GetValueOrDefault(id);

        public IReadOnlyList<CvcObject> GetByDrawing(Guid drawingId) =>
            _objects.Values.Where(o => o.DrawingId == drawingId).ToList();

        public bool Remove(Guid id) => _objects.Remove(id);
    }

    private sealed class FakeLayerRepository : ILayerRepository
    {
        private readonly Dictionary<Guid, Layer> _layers = [];

        public void Add(Layer layer) => _layers[layer.Id] = layer;

        public Layer? Get(Guid id) => _layers.GetValueOrDefault(id);

        public IReadOnlyList<Layer> GetByDrawing(Guid drawingId) =>
            _layers.Values.Where(l => l.DrawingId == drawingId).OrderBy(l => l.Order).ToList();

        public bool Remove(Guid id) => _layers.Remove(id);
    }
}
