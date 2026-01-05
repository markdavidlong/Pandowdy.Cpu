using Avalonia;
using ReactiveUI.Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pandowdy.UI;
using Pandowdy.UI.Interfaces;
using Pandowdy.EmuCore;
using Pandowdy.EmuCore.Interfaces;
using Pandowdy.UI.ViewModels;
using Pandowdy.EmuCore.Services;
using Pandowdy.EmuCore.DataTypes;

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

                    services.AddSingleton<IDirectMemoryPoolReader>(sp => sp.GetRequiredService<MemoryPool>());

                    services.AddSingleton<ICpu, CPUAdapter>();
                    services.AddSingleton<IFrameProvider, FrameProvider>();
                    services.AddSingleton<IEmulatorState, EmulatorStateProvider>();
                    services.AddSingleton<ICharacterRomProvider, CharacterRomProvider>();
                    services.AddSingleton<IDisplayBitmapRenderer, LegacyBitmapRenderer>();

                    services.AddSingleton<IFrameGenerator, FrameGenerator>();

                    // SystemStatusProvider implements both ISystemStatusProvider and ISoftSwitchResponder
                    // Register the concrete type first
                    services.AddSingleton<SystemStatusProvider>();
                    // Then register both interfaces to point to the same instance
                    services.AddSingleton<ISystemStatusProvider>(sp => sp.GetRequiredService<SystemStatusProvider>());
                    services.AddSingleton<ISoftSwitchResponder>(sp => sp.GetRequiredService<SystemStatusProvider>());
                    
                    // Floating Bus Provider
                    services.AddSingleton<IFloatingBusProvider, NullFloatingBusProvider>();
                    
                    // System ROM Provider - loads embedded ROM
                    services.AddSingleton<ISystemRomProvider>(sp => 
                        new SystemRomProvider("res:Pandowdy.EmuCore.Resources.a2e_enh_c-f.rom"));
                    
                    // System RAM - Keyed by descriptive size names
                    // Transient lifetime ensures each request gets a new instance
                    services.AddKeyedTransient<ISystemRam>("16K", (sp, key) => new MemoryBlock(0x4000));
                    services.AddKeyedTransient<ISystemRam>("48K", (sp, key) => new MemoryBlock(0xC000));
                    
                    // Language Card - uses two 16KB RAM blocks
                    services.AddSingleton<ILanguageCard,LanguageCard>(sp =>
                    {
                        var mainRam = sp.GetRequiredKeyedService<ISystemRam>("16K");    // 16KB main RAM
                        var auxRam = sp.GetRequiredKeyedService<ISystemRam>("16K");     // 16KB aux RAM
                        var systemRom = sp.GetRequiredService<ISystemRomProvider>();
                        var floatingBus = sp.GetRequiredService<IFloatingBusProvider>();
                        var status = sp.GetRequiredService<ISystemStatusProvider>();
                        
                        return new LanguageCard(mainRam, auxRam, systemRom, floatingBus, status);
                    });

                    // SystemRamSelector - uses two 48KB RAM blocks
                    services.AddSingleton<ISystemRamSelector, SystemRamSelector>(sp =>
                    {
                        var mainRam = sp.GetRequiredKeyedService<ISystemRam>("48K");    // 48KB main RAM
                        var auxRam = sp.GetRequiredKeyedService<ISystemRam>("48K");     // 48KB aux RAM
                        var floatingBus = sp.GetRequiredService<IFloatingBusProvider>();
                        var status = sp.GetRequiredService<ISystemStatusProvider>();

                        return new SystemRamSelector(mainRam, auxRam, floatingBus, status);
                    });

                    services.AddSingleton<IAppleIIBus,VA2MBus>();

                    // UI services
                    services.AddSingleton<IRefreshTicker, AvaloniaRefreshTicker>();

                    // ViewModels
                    services.AddTransient<EmulatorStateViewModel>();
                    services.AddTransient<MainWindowViewModel>();
                    services.AddTransient<SystemStatusViewModel>();

                    services.AddSingleton<VA2M>();
                    
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
