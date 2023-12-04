using Rendering;
using Rendering.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Rendering.Spaces;

public static class RenderingServiceExtensions
{
    public static void AddRenderingServices(this IServiceCollection services)
    {
        services.AddECSSingleton<RenderLoop>();
        services.AddECSSingleton<ComponentRenderPipeline>();
        services.AddECSSingleton<ConceptualUISpace>();
        services.AddECSSingleton<PhysicalScreenSpace>();
    }

    public static void ExtractConfigurations(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RenderingOptions>(configuration.GetSection(RenderingOptions.ConfigurationSection));
    }

    public static void InitialiseRenderingServices(this IServiceProvider serviceProvider)
    {
        var canvasUpdateHandler = serviceProvider.GetRequiredService<ICanvasUpdateSystem>();
        var renderLoop = serviceProvider.GetRequiredService<RenderLoop>();
        renderLoop.SetRenderToScreenAction(canvasUpdateHandler.UpdateVisual);
    }
}
