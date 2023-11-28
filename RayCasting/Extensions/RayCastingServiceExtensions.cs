using Rendering;
using Rendering.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using EntityComponentSystem.RayCasting;

public static class RayCastingServiceExtensions
{
    public static void AddRayCastingServices(this IServiceCollection services)
    {
        services.AddECSSingleton<RayCaster>();
    }

}
