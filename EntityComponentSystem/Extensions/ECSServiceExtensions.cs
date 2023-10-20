using EntityComponentSystem;
using Microsoft.Extensions.DependencyInjection;

public static class ECSServiceExtensions
{
    public static void AddECSServices(this IServiceCollection services)
    {
        services.AddSingleton<ECS>();
    }
}
