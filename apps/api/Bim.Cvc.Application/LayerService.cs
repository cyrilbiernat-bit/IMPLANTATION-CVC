using Bim.Cvc.Domain;

namespace Bim.Cvc.Application;

/// <summary>
/// Module 3 — calques d'un plan : affichage et verrouillage indépendants,
/// comme dans un logiciel CAO. Un plan a toujours au moins un calque.
/// </summary>
public sealed class LayerService(IDrawingRepository drawings, ILayerRepository layers, ICvcObjectRepository objects)
{
    /// <summary>Calque créé automatiquement à l'import d'un plan (module 1).</summary>
    public Layer CreateDefault(Guid drawingId) => CreateInternal(drawingId, "Calque 1");

    public Layer Create(Guid drawingId, string? name)
    {
        _ = drawings.Get(drawingId) ?? throw new DrawingNotFoundException(drawingId);
        return CreateInternal(drawingId, name);
    }

    private Layer CreateInternal(Guid drawingId, string? name)
    {
        var order = layers.GetByDrawing(drawingId).Count;
        var layer = new Layer
        {
            DrawingId = drawingId,
            Name = string.IsNullOrWhiteSpace(name) ? $"Calque {order + 1}" : name.Trim(),
            Order = order,
        };
        layers.Add(layer);
        return layer;
    }

    public IReadOnlyList<Layer> GetByDrawing(Guid drawingId)
    {
        _ = drawings.Get(drawingId) ?? throw new DrawingNotFoundException(drawingId);
        return layers.GetByDrawing(drawingId);
    }

    public bool IsLocked(Guid layerId) => layers.Get(layerId)?.Locked ?? false;

    /// <summary>Vérifie que le calque existe, appartient au plan et n'est pas verrouillé — avant d'y poser un objet (module 3).</summary>
    public Layer RequireUnlockedLayer(Guid drawingId, Guid layerId)
    {
        var layer = layers.Get(layerId) ?? throw new InvalidCvcObjectException("Calque introuvable.");
        if (layer.DrawingId != drawingId)
        {
            throw new InvalidCvcObjectException("Ce calque n'appartient pas à ce plan.");
        }
        if (layer.Locked)
        {
            throw new InvalidCvcObjectException($"Le calque « {layer.Name} » est verrouillé.");
        }
        return layer;
    }

    public Layer Update(Guid drawingId, Guid layerId, string? name, string? color, bool? visible, bool? locked)
    {
        var layer = layers.Get(layerId) ?? throw new LayerNotFoundException(layerId);
        if (layer.DrawingId != drawingId)
        {
            throw new LayerNotFoundException(layerId);
        }

        if (!string.IsNullOrWhiteSpace(name)) layer.Name = name.Trim();
        if (!string.IsNullOrWhiteSpace(color)) layer.Color = color;
        if (visible.HasValue) layer.Visible = visible.Value;
        if (locked.HasValue) layer.Locked = locked.Value;
        return layer;
    }

    /// <summary>Supprime un calque. Ses objets sont réaffectés au premier calque restant — jamais le dernier calque d'un plan.</summary>
    public void Delete(Guid drawingId, Guid layerId)
    {
        _ = drawings.Get(drawingId) ?? throw new DrawingNotFoundException(drawingId);
        var layer = layers.Get(layerId);
        if (layer is null || layer.DrawingId != drawingId)
        {
            throw new LayerNotFoundException(layerId);
        }

        var remaining = layers.GetByDrawing(drawingId).Where(l => l.Id != layerId).ToList();
        if (remaining.Count == 0)
        {
            throw new InvalidLayerException("Impossible de supprimer le dernier calque du plan.");
        }

        var fallback = remaining[0];
        foreach (var obj in objects.GetByDrawing(drawingId).Where(o => o.LayerId == layerId))
        {
            obj.LayerId = fallback.Id;
        }

        layers.Remove(layerId);
    }
}
