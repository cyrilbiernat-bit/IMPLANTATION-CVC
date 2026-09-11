using Bim.Cvc.Domain;

namespace Bim.Cvc.Api.Drawings;

public sealed record LayerDto(Guid Id, Guid DrawingId, string Name, string Color, bool Visible, bool Locked, int Order)
{
    public static LayerDto From(Layer layer) =>
        new(layer.Id, layer.DrawingId, layer.Name, layer.Color, layer.Visible, layer.Locked, layer.Order);
}

public sealed record CreateLayerRequest(string? Name);

public sealed record UpdateLayerRequest(string? Name, string? Color, bool? Visible, bool? Locked);
