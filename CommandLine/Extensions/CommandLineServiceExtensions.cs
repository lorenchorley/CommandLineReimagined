using CommandLine.Modules;
using Microsoft.Extensions.DependencyInjection;
using Terminal;
using Terminal.Commands;
using Terminal.Naming;
using Terminal.Types;
using Terminal.Variables;

public static class CommandLineServiceExtensions
{
    public static void AddModules(this IServiceCollection services)
    {
        services.AddSingleton<Shell>();

        services.AddSingleton<PathModule>();
        services.AddSingleton<ConsoleOutModule>();
        services.AddSingleton<CommandHistoryModule>();
        services.AddSingleton<NameResolver>();
        services.AddSingleton<CommandRegistry>();

        // Il faut une nouvelle instance à chaque fois pour qu'un bloc de texte soit propre à une exécution d'une commande
        services.AddTransient<ConsoleOutBlock>();
    }

}
