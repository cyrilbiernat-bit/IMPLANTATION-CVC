namespace Bim.Cvc.Application;

public sealed class DrawingNotFoundException(Guid drawingId) : Exception($"Plan {drawingId} introuvable.");
