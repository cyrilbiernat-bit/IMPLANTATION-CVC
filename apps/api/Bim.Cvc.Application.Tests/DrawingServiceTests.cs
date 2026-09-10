using System.Text;
using Bim.Cvc.Domain;
using Xunit;

namespace Bim.Cvc.Application.Tests;

public sealed class DrawingServiceTests
{
    private static DrawingService CreateSut(int pageCount = 1, IDrawingRepository? repository = null)
    {
        return new DrawingService(new FakeFileStore(), repository ?? new FakeRepository(), new FakePageCounter(pageCount));
    }

    private static MemoryStream SomeBytes(int length = 16) => new(new byte[length]);

    [Fact]
    public async Task ImportAsync_stores_drawing_and_returns_page_count()
    {
        var sut = CreateSut(pageCount: 7);

        var drawing = await sut.ImportAsync("reseau-cvc.pdf", SomeBytes());

        Assert.Equal("reseau-cvc.pdf", drawing.FileName);
        Assert.Equal(7, drawing.NbPages);
        Assert.NotEqual(Guid.Empty, drawing.Id);
    }

    [Fact]
    public async Task ImportAsync_persists_the_drawing_in_the_repository()
    {
        var repository = new FakeRepository();
        var sut = CreateSut(repository: repository);

        var drawing = await sut.ImportAsync("plan.pdf", SomeBytes());

        Assert.Same(drawing, repository.Get(drawing.Id));
    }

    [Theory]
    [InlineData("plan.png")]
    [InlineData("plan")]
    [InlineData("plan.PDF.exe")]
    public async Task ImportAsync_rejects_non_pdf_file_names(string fileName)
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<InvalidDrawingException>(() => sut.ImportAsync(fileName, SomeBytes()));
    }

    [Fact]
    public async Task ImportAsync_rejects_an_empty_file()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<InvalidDrawingException>(() => sut.ImportAsync("plan.pdf", new MemoryStream()));
    }

    [Fact]
    public async Task ImportAsync_rejects_a_pdf_with_no_pages()
    {
        var sut = CreateSut(pageCount: 0);

        await Assert.ThrowsAsync<InvalidDrawingException>(() => sut.ImportAsync("plan.pdf", SomeBytes()));
    }

    private sealed class FakeFileStore : IDrawingFileStore
    {
        public Task<string> SaveAsync(Guid drawingId, string fileName, Stream content, CancellationToken ct = default)
            => Task.FromResult($"memory://{drawingId}");

        public Task<Stream> OpenReadAsync(string storagePath, CancellationToken ct = default)
            => Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes("fake-pdf")));
    }

    private sealed class FakeRepository : IDrawingRepository
    {
        private readonly Dictionary<Guid, Drawing> _drawings = [];

        public void Add(Drawing drawing) => _drawings[drawing.Id] = drawing;

        public Drawing? Get(Guid id) => _drawings.GetValueOrDefault(id);
    }

    private sealed class FakePageCounter(int pageCount) : IPdfPageCounter
    {
        public int CountPages(Stream pdfContent) => pageCount;
    }
}
