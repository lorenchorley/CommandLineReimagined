using CommandLine.Modules;
using CommandLineReimagined.Commands;
using EntityComponentSystem;
using Microsoft.Extensions.DependencyInjection;

public static class CommandLineServiceExtensions
{
    public static void AddModules(this IServiceCollection services)
    {
        services.AddSingleton<Shell>();

        services.AddSingleton<PathModule>();
        services.AddSingleton<ConsoleOutModule>();
        services.AddSingleton<CommandHistoryModule>();

        // Il faut une nouvelle instance à chaque fois pour qu'un scope soit propre à une exécution d'une commande
        services.AddTransient<ConsoleOutScope>();
    }

}
