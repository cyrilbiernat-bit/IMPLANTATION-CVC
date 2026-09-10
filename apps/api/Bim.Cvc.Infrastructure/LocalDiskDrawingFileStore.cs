using Bim.Cvc.Application;
using Microsoft.Extensions.Options;

namespace Bim.Cvc.Infrastructure;

public sealed class LocalStorageOptions
{
    /// <summary>Dossier racine, hors public web, où sont écrits les plans importés.</summary>
    public string RootPath { get; set; } = Path.Combine(AppContext.BaseDirectory, "storage", "drawings");
}

/// <summary>
/// Stand-in de développement pour Azure Blob Storage : écrit les plans sur
/// le disque local du serveur. Implémente <see cref="IDrawingFileStore"/>
/// pour rester interchangeable sans toucher au reste de l'application.
/// </summary>
public sealed class LocalDiskDrawingFileStore(IOptions<LocalStorageOptions> options) : IDrawingFileStore
{
    private readonly string _rootPath = options.Value.RootPath;

    public async Task<string> SaveAsync(Guid drawingId, string fileName, Stream content, CancellationToken ct = default)
    {
        Directory.CreateDirectory(_rootPath);
        var storagePath = Path.Combine(_rootPath, $"{drawingId}.pdf");

        content.Position = 0;
        await using var fileStream = File.Create(storagePath);
        await content.CopyToAsync(fileStream, ct);

        return storagePath;
    }

    public Task<Stream> OpenReadAsync(string storagePath, CancellationToken ct = default)
    {
        Stream stream = File.OpenRead(storagePath);
        return Task.FromResult(stream);
    }
}
