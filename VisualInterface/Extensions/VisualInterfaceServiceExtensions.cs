using CommandLineReimagined.Console;
using Microsoft.Extensions.DependencyInjection;

public static class VisualInterfaceServiceExtensions
{
    public static void AddVisualInterfaceServices(this IServiceCollection services)
    {
        services.AddSingleton<ConsoleLayout>();
    }
}
