using CommandLineReimagined;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;

public static class ServiceExtensions
{
    public static void ConfigureServices(HostBuilderContext hostContext, IServiceCollection services)
    {
        // La fenêtre principale
        services.AddSingleton<MainWindow>();

        services.AddECSServices();
        services.AddCommands();
        services.AddModules();
        services.AddRenderingServices();
        services.ExtractConfigurations(hostContext.Configuration);
        services.AddVisualInterfaceServices();
        services.AddInteractionLogicServices();
        services.AddRayCastingServices();
    }

    public static void AddConfiguration(HostBuilderContext hostContext, IConfigurationBuilder configuration)
    {
        configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
    }

    public static IServiceProvider InitialiseServices(this IServiceProvider serviceProvider)
    {
        serviceProvider.GetRequiredService<MainWindow>();

        serviceProvider.InitialVisualInterfaceServices();
        serviceProvider.InitialiseInteractionLogicServices();

        return serviceProvider;
    }

}
