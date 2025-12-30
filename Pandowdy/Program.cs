using System;
using System.Threading.Tasks;
using Avalonia;
using ReactiveUI.Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pandowdy.UI;
using Pandowdy.UI.Interfaces;
using Pandowdy.EmuCore;
using Pandowdy.UI.ViewModels;
using IFrameProvider = Pandowdy.EmuCore.Interfaces.IFrameProvider;
using IEmulatorState = Pandowdy.EmuCore.Interfaces.IEmulatorState;
using ISystemStatusProvider = Pandowdy.EmuCore.Interfaces.ISystemStatusProvider;
using IRefreshTicker = Pandowdy.EmuCore.Interfaces.IRefreshTicker;
using Pandowdy.EmuCore.Interfaces;
using Pandowdy.EmuCore.Services;

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
                    services.AddSingleton<Emulator.CPU>();



                    // EmuCore
                    services.AddSingleton<MemoryPool>();
                    services.AddSingleton<ICpu, CPUAdapter>();
                    services.AddSingleton<IFrameProvider, FrameProvider>();
                    services.AddSingleton<IEmulatorState, EmulatorStateProvider>();
                    
                    // SystemStatusProvider implements both ISystemStatusProvider and ISoftSwitchResponder
                    // Register the concrete type first
                    services.AddSingleton<SystemStatusProvider>();
                    // Then register both interfaces to point to the same instance
                    services.AddSingleton<ISystemStatusProvider>(sp => sp.GetRequiredService<SystemStatusProvider>());
                    services.AddSingleton<ISoftSwitchResponder>(sp => sp.GetRequiredService<SystemStatusProvider>());
                    
                    services.AddSingleton<IAppleIIBus,VA2MBus>();

                    // UI services
                    services.AddSingleton<IRefreshTicker, AvaloniaRefreshTicker>();

                    // ViewModels
                    services.AddTransient<EmulatorStateViewModel>();
                    services.AddTransient<MainWindowViewModel>();
                    services.AddTransient<SystemStatusViewModel>();


                    services.AddSingleton<VA2M>();
                    // Machine factory (singleton instance for now)
                    //services.AddSingleton(provider =>
                    //{
                    //    var state = provider.GetRequiredService<IEmulatorState>();
                    //    var frame = provider.GetRequiredService<IFrameProvider>();
                    //    var sysStatus = provider.GetRequiredService<ISystemStatusProvider>();
                    //    return new VA2M(state, frame, sysStatus);
                    //});
                    
                    // MainWindow factory - encapsulates creation and initialization
                    services.AddSingleton<IMainWindowFactory, MainWindowFactory>();
                });
        }

        private static Task InitializeCoreAsync(IServiceProvider _)
        {
            return Task.CompletedTask;
        }
    }
}
