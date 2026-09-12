using Bim.Cvc.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bim.Cvc.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<LocalStorageOptions>(configuration.GetSection("LocalStorage"));

        services.AddSingleton<IProjectRepository, InMemoryProjectRepository>();
        services.AddScoped<ProjectService>();

        services.AddSingleton<IDrawingRepository, InMemoryDrawingRepository>();
        services.AddSingleton<IDrawingFileStore, LocalDiskDrawingFileStore>();
        services.AddSingleton<IPlanFileParser, PdfPlanParser>();
        services.AddSingleton<IPlanFileParser, CadPlanParser>();
        services.AddScoped<DrawingService>();

        services.AddSingleton<ICvcObjectRepository, InMemoryCvcObjectRepository>();
        services.AddSingleton<ILayerRepository, InMemoryLayerRepository>();
        services.AddScoped<LayerService>();
        services.AddScoped<CvcObjectService>();
        services.AddScoped<ProjectMetresService>();
        services.AddScoped<ProjectNomenclatureService>();

        return services;
    }
}
