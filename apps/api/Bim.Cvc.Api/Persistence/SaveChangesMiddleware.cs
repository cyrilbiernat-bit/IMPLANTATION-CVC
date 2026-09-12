using Bim.Cvc.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bim.Cvc.Api.Persistence;

/// <summary>
/// Un seul <see cref="BimCvcDbContext.SaveChangesAsync"/> par requête, après
/// exécution du contrôleur : les services applicatifs (module 2-6) mutent
/// des entités déjà chargées (calibration, verrouillage de calque,
/// réaffectation d'objets…) directement en mémoire, sans jamais appeler de
/// méthode "Update" explicite — le suivi de changements d'EF Core s'en
/// charge, à condition qu'il y ait un point d'enregistrement unique.
/// </summary>
public sealed class SaveChangesMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, BimCvcDbContext db)
    {
        await next(context);

        try
        {
            await db.SaveChangesAsync(context.RequestAborted);
        }
        catch (DbUpdateException) when (!context.Response.HasStarted)
        {
            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        }
    }
}
