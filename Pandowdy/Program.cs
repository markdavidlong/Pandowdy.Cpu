// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Avalonia;
using ReactiveUI.Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pandowdy.Cpu;
using Pandowdy.UI;
using Pandowdy.UI.Interfaces;
using Pandowdy.UI.Services;
using Pandowdy.EmuCore;
using Pandowdy.EmuCore.Input;
using Pandowdy.EmuCore.IO;
using Pandowdy.EmuCore.Machine;
using Pandowdy.EmuCore.Memory;
using Pandowdy.EmuCore.Slots;
using Pandowdy.UI.ViewModels;
using Pandowdy.EmuCore.DataTypes;
using Pandowdy.EmuCore.Video;
using Pandowdy.EmuCore.DiskII;
using Pandowdy.EmuCore.DiskII.Providers;
using Pandowdy.Project.Services;
using Pandowdy.Project.Interfaces;

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
                    var wrapped = new CapabilityAwareServiceCollection(services);
                    RegisterServices(wrapped);
                });
        }

        private static void RegisterServices(IServiceCollection services)
        {
            // Pandowdy.Cpu - CPU state (injected into CPU)
            services.AddSingleton<CpuState>();

            // Pandowdy.Cpu - CPU emulation
            services.AddSingleton<IPandowdyCpu>(sp =>
            {
                var state = sp.GetRequiredService<CpuState>();
                return CpuFactory.Create(CpuVariant.Wdc65C02, state);
            });

            // EmuCore - Input Subsystems
            //services.AddSingleton<SingularKeyHandler>();  // Keyboard handler (both IKeyboardReader and IKeyboardSetter)
            services.AddSingleton<QueuedKeyHandler>();  // Keyboard handler (both IKeyboardReader and IKeyboardSetter)
            services.AddSingleton<IKeyboardReader>(sp => sp.GetRequiredService<QueuedKeyHandler>());
            services.AddSingleton<IKeyboardSetter>(sp => sp.GetRequiredService<QueuedKeyHandler>());

            services.AddSingleton<IGameControllerStatus, SimpleGameController>();

            // Disk Status Provider - provides observable disk status for UI
            // Single instance shared between EmuCore (mutator) and UI (provider)
            services.AddSingleton<DiskStatusProvider>();
            services.AddSingleton<IDiskStatusProvider>(sp => sp.GetRequiredService<DiskStatusProvider>());
            services.AddSingleton<IDiskStatusMutator>(sp => sp.GetRequiredService<DiskStatusProvider>());

            // Card Response Channel - provides response stream and emitter for card messaging
            // Single instance shared between cards (emitter) and UI (provider)
            services.AddSingleton<CardResponseChannel>();
            services.AddSingleton<ICardResponseProvider>(sp => sp.GetRequiredService<CardResponseChannel>());
            services.AddSingleton<ICardResponseEmitter>(sp => sp.GetRequiredService<CardResponseChannel>());

            // Project management - ad hoc project always exists (Phase 2a)
            services.AddSingleton<ISkilletProjectManager, SkilletProjectManager>();

            // IDiskImageStore - resolves from current project
            // This allows cards to receive IDiskImageStore via constructor injection
            services.AddSingleton<IDiskImageStore>(sp =>
            {
                var projectManager = sp.GetRequiredService<ISkilletProjectManager>();
                return projectManager.CurrentProject ?? throw new InvalidOperationException("No project loaded (ad hoc project initialization failed)");
            });

            // Card factory - receives IDiskImageStore from current project
            services.AddSingleton<ICardFactory>(sp =>
            {
                var cards = sp.GetServices<ICard>();
                var diskImageStore = sp.GetRequiredService<IDiskImageStore>();
                return new CardFactory(cards, diskImageStore);
            });
            services.AddSingleton<ISlots, Slots>();

            services.AddSingleton<CpuClockingCounters>();

            // EmuCore - Memory & Bus
            services.AddSingleton<AddressSpaceController>();

            services.AddSingleton<IDirectMemoryPoolReader>(sp => sp.GetRequiredService<AddressSpaceController>());

            services.AddSingleton<IFrameProvider, FrameProvider>();
            services.AddSingleton<IEmulatorState, EmulatorStateProvider>();
            services.AddSingleton<ICharacterRomProvider, CharacterRomProvider>();
            services.AddSingleton<IDisplayBitmapRenderer, LegacyBitmapRenderer>();

            services.AddSingleton<IFrameGenerator, FrameGenerator>();

            services.AddSingleton<SystemIoHandler>();
            services.AddSingleton<NoSlotClockIoHandler>();
            services.AddSingleton<ISystemIoHandler>(sp =>
                sp.GetRequiredService<NoSlotClockIoHandler>());

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
            services.AddSingleton<ILanguageCard, LanguageCard>(sp =>
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

            services.AddSingleton<IAppleIIBus, VA2MBus>();

            // Memory Inspector - provides read-only access to all memory regions for debugging
            services.AddSingleton<IMemoryInspector, MemoryInspector>();

            // UI services
            services.AddSingleton<IRefreshTicker, AvaloniaRefreshTicker>();
            services.AddSingleton<IMessageBoxService, MessageBoxService>();
            services.AddSingleton<IDiskFileDialogService, DiskFileDialogService>();
            services.AddSingleton<IProjectFileDialogService, ProjectFileDialogService>();
            services.AddSingleton<GuiSettingsService>(); // Master GUI settings service
            services.AddSingleton<IDriveStateService, DriveStateService>();

            // ViewModels
            services.AddTransient<EmulatorStateViewModel>();
            services.AddTransient<SystemStatusViewModel>();
            services.AddTransient<DiskStatusPanelViewModel>();
            services.AddTransient<CpuStatusPanelViewModel>();
            services.AddTransient<StatusBarViewModel>();
            services.AddTransient<PeripheralsMenuViewModel>();
            services.AddTransient<MainWindowViewModel>();

            services.AddSingleton<RestartCollection>();

            services.AddSingleton<VA2M>();


            // Register IEmulatorCoreInterface alias for VA2M
            // This allows the UI to depend on the core interface abstraction instead of concrete VA2M type,
            // providing explicit thread-safe contract and preventing accidental cross-thread calls
            services.AddSingleton<IEmulatorCoreInterface>(sp => sp.GetRequiredService<VA2M>());

            // MainWindow factory - encapsulates creation and initialization
            services.AddSingleton<IMainWindowFactory, MainWindowFactory>();
        }

        private static async Task InitializeCoreAsync(IServiceProvider services)
        {
            System.Diagnostics.Debug.WriteLine("[Program] === Initializing Emulator Core ===");

            // Create ad hoc project (Phase 2a - non-nullable IDiskImageStore requirement)
            // Ad hoc project is an in-memory .skillet used when no user project is open
            var projectManager = services.GetRequiredService<ISkilletProjectManager>();
            await projectManager.CreateAdHocAsync();
            System.Diagnostics.Debug.WriteLine("[Program] Created ad hoc in-memory project");

            // Wire RestartCollection into Slots for automatic card registration.
            // Done here (not in DI) to avoid circular dependency:
            // RestartCollection → IRestartable singletons → VA2MBus → AddressSpaceController → ISlots → RestartCollection
            var slots = (Slots)services.GetRequiredService<ISlots>();
            var restartCollection = services.GetRequiredService<RestartCollection>();
            slots.SetRestartCollection(restartCollection);

            // Install Disk II controller in slot 6 (standard Apple II configuration)
            // Cards implementing IRestartable are automatically registered in RestartCollection
            slots.InstallCard(10, SlotNumber.Slot6); // 10 = DiskIIControllerCard16Sector
            slots.InstallCard(10, SlotNumber.Slot5); // 10 = DiskIIControllerCard16Sector

            System.Diagnostics.Debug.WriteLine("[Program] Disk II controllers installed in slots 5 and 6");
            System.Diagnostics.Debug.WriteLine("[Program] === Core Initialization Complete ===");
            System.Diagnostics.Debug.WriteLine("[Program] Note: Disk image restoration deferred to GUI (MainWindow.InitialStartup)");

            // Disk image restoration has been moved to MainWindow.InitialStartup()
            // This ensures the GUI is fully initialized before restoring user state
        }
    }
}
