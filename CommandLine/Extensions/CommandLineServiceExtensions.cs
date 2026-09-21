using CommandLine.Modules;
using CommandLineReimagined.Core;
using UIComponents;
using Microsoft.Extensions.DependencyInjection;
using Terminal;
using Terminal.Commands;
using Terminal.Search;
using Terminal.Execution;
using Microsoft.Extensions.DependencyInjection.Extensions;

public static class CommandLineServiceExtensions
{
    public static void AddModules(this IServiceCollection services)
    {
        services.AddECSSingleton<Shell>();
        services.AddECSSingleton<Prompt>();
        services.AddECSSingleton<Scene>();
        services.AddECSSingleton<MouseInputHandler>(); // Component, needs accessor via ecs instance if injected into IoC
        services.AddECSSingleton<KeyInputHandler>();

        services.AddECSSingleton<ConsoleOutModule>();

        // One session per shell, over an in-memory log seeded the way the browser is.
        // Decision 0012 leaves a projection onto the real disk out of scope, so the
        // desktop filesystem lives as long as the window does, exactly as the tab's
        // does before Phase 2 persists it.
        services.AddSingleton(sp => DesktopSession.Create(sp.GetRequiredService<IApplicationLifetime>()));

        // Search and the registry want names and descriptions, not runnable commands.
        // Neither is an ECS subsystem; they were registered as one only because every
        // other service here was.
        services.AddSingleton(sp => new CommandRegistry(sp.GetRequiredService<Session>().Commands.ToList()));
        services.AddSingleton(sp => new CommandSearch(sp.GetRequiredService<Session>().Commands.ToList()));

        services.AddSingleton<ResultRenderer>();

        // A host that can actually close replaces this; tests and the web host do not.
        services.TryAddSingleton<IApplicationLifetime, NoOpApplicationLifetime>();

        // Il faut une nouvelle instance à chaque fois pour qu'un bloc de texte soit propre à une exécution d'une commande
        services.AddTransient<CliBlock>();
    }
}
