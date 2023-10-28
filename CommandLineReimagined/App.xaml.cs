using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Windows;
using static ServiceExtensions;

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
            .ConfigureAppConfiguration(AddConfiguration)
            .ConfigureServices(ConfigureServices)

            // Create the host
            .Build()

            // Initialise the services
            .Services
            .InitialiseServices()

            // Request and open the main window
            .GetRequiredService<MainWindow>()
            .Show();
    }

}
