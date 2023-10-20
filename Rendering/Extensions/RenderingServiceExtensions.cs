using CommandLineReimagined.Rendering;
using CommandLineReimagined.Rendering.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class RenderingServiceExtensions
{
    public static void AddRenderingServices(this IServiceCollection services)
    {
        services.AddSingleton<RenderLoop>();
    }

    public static void ExtractConfigurations(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RenderingOptions>(configuration.GetSection(RenderingOptions.ConfigurationSection));
    }

}
