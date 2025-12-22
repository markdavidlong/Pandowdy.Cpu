using System;
using System.Threading.Tasks;
using Avalonia;
using ReactiveUI.Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pandowdy.UI;
using Pandowdy.EmuCore;
using Pandowdy.UI.ViewModels;

namespace Pandowdy
{
    public static class Program
    {
        private static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .UseReactiveUI()
                .LogToTrace();
        }

        public static async Task Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();

            var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
            logger.LogInformation("Starting Pandowdy host.");

            UiBootstrap.IntegrateUiServiceProvider(host);

            await InitializeCoreAsync(host.Services);

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

            logger.LogInformation("Shutting down Pandowdy host.");
            await host.StopAsync();
        }

        private static IHostBuilder CreateHostBuilder(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                    logging.AddDebug();
                })
                .ConfigureServices((context, services) =>
                {
                    // EmuCore
                    services.AddSingleton<IFrameProvider, FrameProvider>();
                    services.AddSingleton<IEmulatorState, EmulatorStateProvider>();
                    services.AddSingleton<ISystemStatusProvider, SystemStatusProvider>();

                    // UI services
                    services.AddSingleton<IRefreshTicker, AvaloniaRefreshTicker>();

                    // ViewModels
                    services.AddTransient<EmulatorStateViewModel>();
                    services.AddTransient<MainWindowViewModel>();
                    services.AddTransient<SystemStatusViewModel>();

                    // Machine factory (singleton instance for now)
                    services.AddSingleton(provider =>
                    {
                        var state = provider.GetRequiredService<IEmulatorState>();
                        var frame = provider.GetRequiredService<IFrameProvider>();
                        var sysStatus = provider.GetRequiredService<ISystemStatusProvider>();
                        return new VA2M(state, frame, sysStatus);
                    });
                });
        }

        private static Task InitializeCoreAsync(IServiceProvider services)
        {
            return Task.CompletedTask;
        }
    }
}
