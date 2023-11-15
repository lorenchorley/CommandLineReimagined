using Controller;
using Microsoft.Extensions.DependencyInjection;

public static class ControllerServiceExtensions
{
    public static void AddControllerServices(this IServiceCollection services)
    {
        services.AddSingleton<LoopController>();
    }

    public static void InitialiseControllerServices(this IServiceProvider serviceProvider)
    {
    }
}
