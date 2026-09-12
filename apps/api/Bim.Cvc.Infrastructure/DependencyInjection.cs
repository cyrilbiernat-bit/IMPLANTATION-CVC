using Bim.Cvc.Application;
using Bim.Cvc.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bim.Cvc.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<LocalStorageOptions>(configuration.GetSection("LocalStorage"));

        var connectionString = configuration.GetConnectionString("BimCvc")
            ?? throw new InvalidOperationException(
                "Chaîne de connexion 'BimCvc' manquante (ConnectionStrings:BimCvc dans la configuration).");
        services.AddDbContext<BimCvcDbContext>(options => options.UseNpgsql(connectionString));

        services.AddScoped<IProjectRepository, EfProjectRepository>();
        services.AddScoped<ProjectService>();

        services.AddScoped<IDrawingRepository, EfDrawingRepository>();
        services.AddSingleton<IDrawingFileStore, LocalDiskDrawingFileStore>();
        services.AddSingleton<IPlanFileParser, PdfPlanParser>();
        services.AddSingleton<IPlanFileParser, CadPlanParser>();
        services.AddScoped<DrawingService>();

        services.AddScoped<ICvcObjectRepository, EfCvcObjectRepository>();
        services.AddScoped<ILayerRepository, EfLayerRepository>();
        services.AddScoped<LayerService>();
        services.AddScoped<CvcObjectService>();
        services.AddScoped<ProjectMetresService>();
        services.AddScoped<ProjectNomenclatureService>();

        return services;
    }
}
