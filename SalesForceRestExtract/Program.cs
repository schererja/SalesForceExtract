using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Extensions.Logging;
using SalesForceRestExtract.Controller;
using SalesForceRestExtract.Models;

namespace SalesForceRestExtract
{
    internal class Program
    {
        /// <summary>
        ///     Used as the log config file name
        /// </summary>
        private const string LoggerConfigFile = "nlog.config";

        /// <summary>
        ///     Used as the application config file name
        /// </summary>
        private const string AppConfigFile = "app-settings.json";

        /// <summary>
        ///     Main entry point for project
        /// </summary>
        /// <param name="args">
        ///     Optional args, not currently used
        /// </param>
        private static void Main(string[] args)
        {
            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);

            // Creates the Service Provider
            var serviceProvider = serviceCollection.BuildServiceProvider();
            serviceProvider.GetService<ILoggerFactory>()
                .AddNLog(new NLogProviderOptions
                {
                    CaptureMessageProperties = true,
                    CaptureMessageTemplates = true
                });
            // Loads the internal configuration file
            LogManager.LoadConfiguration(LoggerConfigFile);
            serviceProvider.GetService<SalesForceController>().Start();
        }

        /// <summary>
        ///     Used for configuring the services that are being used by
        ///     dependency Injection
        /// </summary>
        /// <param name="serviceCollection">
        ///     Requires a <see cref="IServiceCollection" />.
        /// </param>
        public static void ConfigureServices(IServiceCollection serviceCollection)
        {
            // Sets up configuration settings
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile(AppConfigFile, false)
                .Build();

            serviceCollection.AddOptions();
            serviceCollection.Configure<AppSettings>(configuration
                .GetSection("Configuration"));
            // Sets up logging
            serviceCollection.AddSingleton(new LoggerFactory()
                .AddConsole(configuration.GetSection("Logging"))
                .AddDebug());
            serviceCollection.AddTransient<SalesForceController>();
            serviceCollection.AddLogging();
        }
    }
}