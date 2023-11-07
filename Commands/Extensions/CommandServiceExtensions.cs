using Commands;
using Commands.Implementations;
using Microsoft.Extensions.DependencyInjection;

public static class CommandServiceExtensions
{
    public static void AddCommands(this IServiceCollection services)
    {
        services.ConfigureCommand<ListDirectoryContents>();
        services.ConfigureCommand<ChangeDirectory>();
        services.ConfigureCommand<MakeDirectory>();
        services.ConfigureCommand<CopyFile>();
        services.ConfigureCommand<UpOneDirectory>();
        services.ConfigureCommand<Echo>();
        //services.ConfigureCommand<ProgressTest>();
        services.ConfigureCommand<Download>();

        // Commandes "systèmes"
        services.ConfigureCommand<DebugOut>();
        services.ConfigureCommand<Exit>();
        services.ConfigureCommand<UnknownCommand>();
    }

    private static void ConfigureCommand<TCommand>(this IServiceCollection services) where TCommand : class, ICommandAction
    {
        // La commande est enregistrée avec son propre type pour qu'on puisse l'instancier
        services.AddTransient<TCommand>();

        // La commande est enregistrée avec son type de base pour qu'on puisse retrouver toutes les commandes en liste
        services.AddTransient<ICommandAction, TCommand>();
    }

}
