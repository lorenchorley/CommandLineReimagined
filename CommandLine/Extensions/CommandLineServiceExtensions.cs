using CommandLine.Modules;
using UIComponents;
using Microsoft.Extensions.DependencyInjection;
using Terminal;
using Terminal.Commands;
using Terminal.Naming;
using Terminal.Scoping;
using Terminal.Search;

public static class CommandLineServiceExtensions
{
    public static void AddModules(this IServiceCollection services)
    {
        services.AddSingleton<Shell>();
        services.AddSingleton<Prompt>();
        services.AddSingleton<Scene>();

        services.AddSingleton<PathModule>();
        services.AddSingleton<ConsoleOutModule>();
        services.AddSingleton<CommandHistoryModule>();
        services.AddSingleton<NameResolver>();
        services.AddSingleton<CommandRegistry>();
        services.AddSingleton<ScopeRegistry>();
        services.AddSingleton<CommandSearch>();

        // Il faut une nouvelle instance à chaque fois pour qu'un bloc de texte soit propre à une exécution d'une commande
        services.AddTransient<CliBlock>();
    }

}
