using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using CommandLineReimagine.Commands;
using CommandLineReimagine.Commands.Implementations;
using CommandLineReimagine.Commands.Modules;
using CommandLineReimagine.Configuration;
using CommandLineReimagine.Console;
using CommandLineReimagine.Rendering;

public static class ServiceExtensions
{
    public static void AddCoreConsoleFunctions(this IServiceCollection services)
    {
        services.AddSingleton<ConsoleLayout>();
        services.AddSingleton<RenderLoop>();
        services.AddSingleton<CommandLine>();
        services.AddSingleton<EntityComponentSystem>();
    }

    public static void ExtractConfigurations(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RenderingOptions>(configuration.GetSection(RenderingOptions.ConfigurationSection));
    }

    public static void AddConsoleModules(this IServiceCollection services)
    {
        services.AddSingleton<PathModule>();
        services.AddSingleton<ConsoleOutModule>();
        services.AddSingleton<CommandHistoryModule>();

        // Il faut une nouvelle instance à chaque fois pour qu'un scope soit propre à une exécution d'une commande
        services.AddTransient<ConsoleOutScope>();
    }

    public static void ConfigureConsoleCommands(this IServiceCollection services)
    {
        services.ConfigureCommand<ListDirectoryContents>();
        services.ConfigureCommand<ChangeDirectory>();
        services.ConfigureCommand<MakeDirectory>();
        services.ConfigureCommand<CopyFile>();
        services.ConfigureCommand<UpOneDirectory>();
        services.ConfigureCommand<Echo>();

        // Commandes "systèmes"
        services.ConfigureCommand<DebugOut>();
        services.ConfigureCommand<Exit>();
        services.ConfigureCommand<UnknownCommand>();
    }

    private static void ConfigureCommand<TCommand>(this IServiceCollection services) where TCommand : CommandAction
    {
        // La commande est enregistrée avec son propre type pour qu'on puisse l'instancier
        services.AddTransient<TCommand>();

        // La commande est enregistrée avec son type de base pour qu'on puisse retrouver toutes les commandes en liste
        services.AddTransient<CommandAction, TCommand>();
    }

}
