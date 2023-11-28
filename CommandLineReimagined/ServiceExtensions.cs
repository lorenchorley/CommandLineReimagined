using Application;
using Application.EventHandlers;
using Application.FrameworkAccessors;
using Application.UpdateHandlers;
using Controller;
using InteractionLogic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Rendering;
using System;

public static class ServiceExtensions
{
    public static void ConfigureServices(HostBuilderContext hostContext, IServiceCollection services)
    {
        services.AddHttpClient();

        services.AddCoreECSServices();
        services.AddCommands();
        services.AddModules();
        services.AddRenderingServices();
        services.ExtractConfigurations(hostContext.Configuration);
        services.AddVisualInterfaceServices();
        services.AddInteractionLogicServices();
        services.AddRayCastingServices();
        services.AddControllerServices();

        services.AddSingleton<CanvasAccessor>();
        services.AddSingleton<InputAccessor>();
        services.AddSingleton<ContextMenuAccessor>();

        services.AddECSSingleton<MainWindow>();
        services.AddECSSingleton<ICanvasUpdateSystem, CanvasUpdateHandler>();
        services.AddECSSingleton<CanvasInteractionEventHandler>();
        services.AddECSSingleton<CanvasRenderingEventHandler>();
        services.AddECSSingleton<TextInputHandler>();
        services.AddECSSingleton<ITextUpdateSystem, TextInputUpdateHandler>();
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
        serviceProvider.InitialiseECSSystems();

        //// Request and initialise the shell
        //// Aspect : scene setup and initialisation
        //serviceProvider
        //    .InitialiseTerminal();

        //// Request and open the main window
        //// Aspect : window and rendering
        //serviceProvider
        //    .GetRequiredService<MainWindow>()
        //    .Show();

        return serviceProvider;
    }

    public static IServiceProvider Start(this IServiceProvider serviceProvider)
    {
        serviceProvider.StartECSSystems();

        // The LoopController is a special object in the system that must be managed that so that it starts only once all other objects are fully initialised
        serviceProvider
            .GetRequiredService<LoopController>()
            .Start();

        return serviceProvider;
    }

}
