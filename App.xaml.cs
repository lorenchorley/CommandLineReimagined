using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Configuration;
using System.Windows;
using CommandLineReimagine.Commands;
using CommandLineReimagine.Commands.Modules;
using CommandLineReimagine.Configuration;
using CommandLineReimagine.Console;
using CommandLineReimagine.Rendering;

namespace CommandLineReimagine
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
        }

        private void OnStartup(object sender, StartupEventArgs e)
        {
            Host.CreateDefaultBuilder(e.Args)
                .ConfigureAppConfiguration(InitialiseConfiguration)
                .ConfigureServices(ConfigureServices)
                .Build()
                .Services
                .GetRequiredService<MainWindow>()
                .Show();
        }

        private void ConfigureServices(HostBuilderContext hostContext, IServiceCollection services)
        {
            // La fenêtre principale
            services.AddSingleton<MainWindow>();

            // Extraire les sections de configuration qui nous intéressent
            services.ExtractConfigurations(hostContext.Configuration);

            // Les services coeurs
            services.AddCoreConsoleFunctions();

            // Les modules utilisables par les commandes
            services.AddConsoleModules();

            // Les commandes (nouvelle instance à chaque demande)
            services.ConfigureConsoleCommands();
        }

        private static void InitialiseConfiguration(HostBuilderContext hostContext, IConfigurationBuilder configuration)
        {
            configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
        }

    }
}
