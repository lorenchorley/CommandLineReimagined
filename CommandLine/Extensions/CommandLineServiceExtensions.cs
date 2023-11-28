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
        services.AddECSSingleton<Shell>();
        services.AddECSSingleton<Prompt>();
        services.AddECSSingleton<Scene>();
        services.AddECSSingleton<MouseInputHandler>(); // Component, needs accessor via ecs instance if injected into IoC
        services.AddECSSingleton<KeyInputHandler>();

        services.AddECSSingleton<PathModule>();
        services.AddECSSingleton<ConsoleOutModule>();
        services.AddECSSingleton<CommandHistoryModule>();
        services.AddECSSingleton<NameResolver>();
        services.AddECSSingleton<CommandRegistry>();
        services.AddECSSingleton<ScopeRegistry>();
        services.AddECSSingleton<CommandSearch>();

        // Il faut une nouvelle instance à chaque fois pour qu'un bloc de texte soit propre à une exécution d'une commande
        services.AddTransient<CliBlock>();
    }
}
