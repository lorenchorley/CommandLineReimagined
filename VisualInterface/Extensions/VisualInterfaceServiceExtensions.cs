using Console;
using Microsoft.Extensions.DependencyInjection;
using Rendering;

public static class VisualInterfaceServiceExtensions
{
    public static void AddVisualInterfaceServices(this IServiceCollection services)
    {
        services.AddSingleton<ConsoleLayout>();
    }
    
    public static void InitialVisualInterfaceServices(this IServiceProvider serviceProvider)
    {
        var consoleLayout = serviceProvider.GetRequiredService<ConsoleLayout>();
        var renderLoop = serviceProvider.GetRequiredService<RenderLoop>();

        renderLoop.SetDrawAction(consoleLayout.Draw);
    }
}
