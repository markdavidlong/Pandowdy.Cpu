using Avalonia;
using ReactiveUI.Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pandowdy.UI;
using Pandowdy.UI.Interfaces;
using Pandowdy.EmuCore;
using Pandowdy.EmuCore.Interfaces;
using Pandowdy.UI.ViewModels;
using Pandowdy.EmuCore.Services;
using Pandowdy.EmuCore.DataTypes;
using Pandowdy.EmuCore.Cards;
using Pandowdy.EmuCore.DiskII;
using Pandowdy.EmuCore.DiskII.Providers;

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

            UiBootstrap.IntegrateUiServiceProvider(host);

            await InitializeCoreAsync(host.Services);

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

            await host.StopAsync();
        }

        private static IHostBuilder CreateHostBuilder(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
                .ConfigureServices((context, services) =>
                {
                    services.AddSingleton<Emulator.CPU>();

                    // EmuCore - Input Subsystems
                    //services.AddSingleton<SingularKeyHandler>();  // Keyboard handler (both IKeyboardReader and IKeyboardSetter)
                    services.AddSingleton<QueuedKeyHandler>();  // Keyboard handler (both IKeyboardReader and IKeyboardSetter)
                    services.AddSingleton<IKeyboardReader>(sp => sp.GetRequiredService<QueuedKeyHandler>());
                    services.AddSingleton<IKeyboardSetter>(sp => sp.GetRequiredService<QueuedKeyHandler>());

                    services.AddSingleton<IGameControllerStatus, SimpleGameController>();

                    // EmuCore - Telemetry
                    services.AddSingleton<ITelemetryAggregator, TelemetryAggregator>();

                    services.AddSingleton<ICardFactory, CardFactory>();
                    services.AddSingleton<ISlots, Slots>();

                    services.AddSingleton<CpuClockingCounters>();

                    // EmuCore - Memory & Bus
                    services.AddSingleton<AddressSpaceController>();

                    services.AddSingleton<IDirectMemoryPoolReader>(sp => sp.GetRequiredService<AddressSpaceController>());

                    services.AddSingleton<ICpu, CPUAdapter>();
                    services.AddSingleton<IFrameProvider, FrameProvider>();
                    services.AddSingleton<IEmulatorState, EmulatorStateProvider>();
                    services.AddSingleton<ICharacterRomProvider, CharacterRomProvider>();
                    services.AddSingleton<IDisplayBitmapRenderer, LegacyBitmapRenderer>();

                    services.AddSingleton<IFrameGenerator, FrameGenerator>();

                    services.AddSingleton<ISystemIoHandler, SystemIoHandler>();

                    // Disk II subsystem
                    services.AddSingleton<IDiskImageFactory, DiskImageFactory>();
                    services.AddSingleton<IDiskIIFactory, DiskIIFactory>();

                    // Cards
                    services.AddTransient<ICard, NullCard>();
                    services.AddTransient<ICard, DiskIIControllerCard16Sector>();
                    services.AddTransient<ICard, DiskIIControllerCard13Sector>();

                    // Threaded rendering services
                    services.AddSingleton<VideoMemorySnapshotPool>(sp => new VideoMemorySnapshotPool(maxPoolSize: 4));
                    services.AddSingleton<RenderingService>();

                    services.AddSingleton<SoftSwitches>();

                    // SystemStatusProvider implements both ISystemStatusProvider (read-only) and ISystemStatusMutator (read-write)
                    // Register the concrete type first - now with game controller integration
                    services.AddSingleton<SystemStatusProvider>(sp =>
                    {
                        var gameController = sp.GetRequiredService<IGameControllerStatus>();
                        return new SystemStatusProvider(gameController);
                    });
                    // Register read-only interface
                    services.AddSingleton<ISystemStatusProvider>(sp => sp.GetRequiredService<SystemStatusProvider>());
                    // Register read-write interface (inherits from ISystemStatusProvider)
                    services.AddSingleton<ISystemStatusMutator>(sp => sp.GetRequiredService<SystemStatusProvider>());


                    // Floating Bus Provider
                    //services.AddSingleton<IFloatingBusProvider, NullFloatingBusProvider>();
                    services.AddSingleton<IFloatingBusProvider, RandomFloatingBusProvider>();

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
                    
                    
                    // Register IEmulatorCoreInterface alias for VA2M
                    // This allows the UI to depend on the core interface abstraction instead of concrete VA2M type,
                    // providing explicit thread-safe contract and preventing accidental cross-thread calls
                    services.AddSingleton<IEmulatorCoreInterface>(sp => sp.GetRequiredService<VA2M>());
                    
                    // MainWindow factory - encapsulates creation and initialization
                    services.AddSingleton<IMainWindowFactory, MainWindowFactory>();
                });
        }

        private static Task InitializeCoreAsync(IServiceProvider services)
        {
            // Install Disk II controller in slot 6 (standard Apple II configuration)
            var slots = services.GetRequiredService<ISlots>();
            slots.InstallCard(10, SlotNumber.Slot6); // 10 = DiskIIControllerCard16Sector

            // Insert a disk into Drive 1 (optional - for testing)
            if (slots.GetCardIn(SlotNumber.Slot6) is DiskIIControllerCard diskController)
            {
                // Example: Insert a disk image into Drive 1
                // diskController.Drives[0].InsertDisk(@"E:\XPS Diagnostic IIe 1.0.5.nib");
                diskController.Drives[0].InsertDisk(@"E:\test.nib");
                // diskController.Drives[0].InsertDisk(@"E:\A2eDiagnostics_v2.1.nib");
                // diskController.Drives[0].InsertDisk(@"E:\missing_ring_good.nib");

                // Example: Insert a disk image into Drive 2
                // diskController.Drives[1].InsertDisk(@"C:\path\to\data.dsk");
                diskController.Drives[1].InsertDisk(@"E:\blank.nib");
            }

            return Task.CompletedTask;
        }
    }
}
