using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Windows;

namespace CommandLineReimagined;

public partial class App : Application
{
    public App()
    {
    }

    private void OnStartup(object sender, StartupEventArgs e)
    {
        // Create the host builder
        Host.CreateDefaultBuilder(e.Args)

            // Configure the host
            .ConfigureAppConfiguration(InitialiseConfiguration)
            .ConfigureServices(ConfigureServices)

            // Create the host
            .Build()

            // Request and open the main window
            .Services
            .GetRequiredService<MainWindow>()
            .Show();
    }

    private void ConfigureServices(HostBuilderContext hostContext, IServiceCollection services)
    {
        // La fenêtre principale
        services.AddSingleton<MainWindow>();

        services.AddECSServices();
        services.AddCommands();
        services.AddModules();
        services.AddRenderingServices();
        services.ExtractConfigurations(hostContext.Configuration);
        services.AddVisualInterfaceServices();

    }

    private static void InitialiseConfiguration(HostBuilderContext hostContext, IConfigurationBuilder configuration)
    {
        configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
    }

}
