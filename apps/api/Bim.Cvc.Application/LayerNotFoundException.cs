namespace Bim.Cvc.Application;

public sealed class LayerNotFoundException(Guid layerId) : Exception($"Calque {layerId} introuvable.");
