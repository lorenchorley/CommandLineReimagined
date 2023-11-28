using EntityComponentSystem;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

public static class ECSServiceExtensions
{
    public static void AddCoreECSServices(this IServiceCollection services)
    {
        services.AddSingleton<ECS>();
    }

    public static void AddECSSingleton<TService>(this IServiceCollection services) where TService : class
    {
        services.AddSingleton<TService>();

        if (typeof(IECSSystem).IsAssignableFrom(typeof(TService)))
        {
            services.AddSingleton<IECSSystem>(x => (IECSSystem)x.GetRequiredService<TService>());
        }
    }
    
    public static void AddECSSingleton<TInterface, TService>(this IServiceCollection services)
        where TInterface : class
        where TService : class, TInterface
    {
        services.TryAddSingleton<TService>();
        services.AddSingleton<TInterface>(x => x.GetRequiredService<TService>());

        if (typeof(IECSSystem).IsAssignableFrom(typeof(TInterface)))
        {
            services.TryAddSingleton<IECSSystem>(x => (IECSSystem)x.GetRequiredService<TService>());
        }
    }
    
    public static void InitialiseECSSystems(this IServiceProvider serviceProvider)
    {
        foreach (var system in serviceProvider.GetServices<IECSSystem>())
        {
            system.OnInit();
        }
    }
    
    public static void StartECSSystems(this IServiceProvider serviceProvider)
    {
        foreach (var system in serviceProvider.GetServices<IECSSystem>())
        {
            system.OnStart();
        }
    }

}
