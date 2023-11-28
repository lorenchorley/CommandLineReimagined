using Microsoft.Extensions.Hosting;
using System.Windows;
using static ServiceExtensions;

namespace Application;

public partial class App : System.Windows.Application
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
            .Initialise()
            .Start();

    }

}
