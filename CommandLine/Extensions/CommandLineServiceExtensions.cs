using CommandLine.Modules;
using UIComponents;
using Microsoft.Extensions.DependencyInjection;
using Terminal;
using Terminal.Commands;
using Terminal.Naming;
using Terminal.Scoping;
using Terminal.Search;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Terminal.Execution;
using Commands;

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
        services.AddSingleton<CommandHistory>();
        services.AddECSSingleton<NameResolver>();
        services.AddECSSingleton<CommandRegistry>();
        services.AddECSSingleton<ScopeRegistry>();
        services.AddECSSingleton<CommandSearch>();

        // Execution layer. Plain singletons: none of these need the ECS lifecycle.
        services.AddSingleton<ArgumentBinder>();
        services.AddSingleton<ResultRenderer>();
        services.AddSingleton<CommandEvaluator>(sp => new CommandEvaluator(
            sp,
            sp.GetServices<ICommandAction>().Select(action => action.Profile),
            sp.GetRequiredService<CommandHistory>(),
            sp.GetRequiredService<ArgumentBinder>()));

        // A host that can actually close replaces this; tests and the web host do not.
        services.TryAddSingleton<IApplicationLifetime, NoOpApplicationLifetime>();

        // Il faut une nouvelle instance à chaque fois pour qu'un bloc de texte soit propre à une exécution d'une commande
        services.AddTransient<CliBlock>();
    }
}
