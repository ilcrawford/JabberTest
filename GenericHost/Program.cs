using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace GenericHost
{
    class Program
    {
        public static async Task Main(string[] args)
        {
            var hostBuilder = new HostBuilder()
                .ConfigureAppConfiguration((hostContext, config) =>
                {
                    config.SetBasePath(Environment.CurrentDirectory);
                    config.AddEnvironmentVariables();
                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddOptions();
                    services.Configure<AppConfig>(hostContext.Configuration.GetSection("AppConfig"));
                    services.AddSingleton<IHostedService, PrintTextToConsoleSvc>();
                })
                .ConfigureLogging((hostingContext, logging) =>
                {
                    logging.AddConfiguration(hostingContext.Configuration.GetSection("Logging"));
                    logging.AddConsole();
                });
            
            await hostBuilder.RunConsoleAsync();
        }

        class PrintTextToConsoleSvc : IHostedService, IDisposable
        {
            private readonly ILogger _logger;
            private readonly IOptions<AppConfig> _appConfig;
            private Timer _timer;
            public PrintTextToConsoleSvc(ILogger<PrintTextToConsoleSvc> logger, IOptions<AppConfig> appConfig)
            {
                _logger = logger;
                _appConfig = appConfig;
            }

            public void Dispose()
            {
                _timer?.Dispose();
            }

            public Task StartAsync(CancellationToken cancellationToken)
            {
                _logger.LogInformation("Starting");

                _timer = new Timer(DoWork, null, TimeSpan.Zero,
                    TimeSpan.FromSeconds(5));

                return Task.CompletedTask;
            }

            public Task StopAsync(CancellationToken cancellationToken)
            {
                _logger.LogInformation("Stopping.");

                _timer?.Change(Timeout.Infinite, 0);

                return Task.CompletedTask;
            }

            private void DoWork(object state)
            {
                _logger.LogInformation($"Background work with text: {_appConfig.Value.TextToPrint}");
            }

        }

        class AppConfig
        {
            public string TextToPrint { get; set; }
        }
    }
}
