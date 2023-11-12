using Rendering;
using Rendering.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class RenderingServiceExtensions
{
    public static void AddRenderingServices(this IServiceCollection services)
    {
        services.AddSingleton<RenderLoop>();
        services.AddSingleton<ComponentRenderPipeline>();
    }

    public static void ExtractConfigurations(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RenderingOptions>(configuration.GetSection(RenderingOptions.ConfigurationSection));
    }

    public static void InitialiseRenderingServices(this IServiceProvider serviceProvider)
    {
        //var pipeline = serviceProvider.GetRequiredService<ComponentRenderPipeline>();
        //var renderLoop = serviceProvider.GetRequiredService<RenderLoop>();

        //renderLoop.SetDrawAction(pipeline.Draw);
    }
}
