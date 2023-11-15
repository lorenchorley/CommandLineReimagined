using CommandLineReimagined;
using Controller;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Net.Http;
using Terminal;

public static class ServiceExtensions
{
    public static void ConfigureServices(HostBuilderContext hostContext, IServiceCollection services)
    {
        // La fenêtre principale
        services.AddSingleton<MainWindow>();

        services.AddHttpClient();

        services.AddECSServices();
        services.AddCommands();
        services.AddModules();
        services.AddRenderingServices();
        services.ExtractConfigurations(hostContext.Configuration);
        services.AddVisualInterfaceServices();
        services.AddInteractionLogicServices();
        services.AddRayCastingServices();
        services.AddControllerServices();
    }

    public static void AddConfiguration(HostBuilderContext hostContext, IConfigurationBuilder configuration)
    {
        configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
    }

    public static IServiceProvider InitialiseServices(this IServiceProvider serviceProvider)
    {
        // Open the main window and set up links required for interaction 
        serviceProvider.GetRequiredService<MainWindow>();

        serviceProvider.InitialiseVisualInterfaceServices();
        serviceProvider.InitialiseInteractionLogicServices();
        serviceProvider.InitialiseRenderingServices();
        serviceProvider.InitialiseControllerServices();

        return serviceProvider;
    }

    public static IServiceProvider Initialise(this IServiceProvider serviceProvider)
    {
        
        // Request and initialise the shell
        // Aspect : scene setup and initialisation
        serviceProvider
            .GetRequiredService<Shell>()
            .Init();
        // Request and open the main window
        // Aspect : window and rendering
        serviceProvider
            .GetRequiredService<MainWindow>()
            .Show();

        return serviceProvider;
    }

    public static IServiceProvider Start(this IServiceProvider serviceProvider)
    {
        serviceProvider
            .GetRequiredService<LoopController>()
            .Start();

        return serviceProvider;
    }

}
