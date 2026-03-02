// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System;
using System.Threading.Tasks;
using Pandowdy.UI.ViewModels;
using Pandowdy.UI.Interfaces;
using Pandowdy.UI.Helpers;
using Pandowdy.UI.Services;
using Pandowdy.EmuCore.Machine;

namespace Pandowdy.UI;

/// <summary>
/// Factory for creating fully-initialized <see cref="MainWindow"/> instances with proper dependency injection.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Purpose:</strong> Provides a clean abstraction over MainWindow's two-phase initialization
/// pattern, encapsulating the complexity of constructor + Initialize() calls into a single Create() method.
/// </para>
/// <para>
/// <strong>Why This Factory Exists:</strong> Avalonia requires windows to have parameterless constructors
/// for XAML compilation, preventing direct constructor-based dependency injection. This factory bridges
/// that gap by:
/// <list type="number">
/// <item>Accepting all dependencies via its own constructor (enabling DI container registration)</item>
/// <item>Creating MainWindow with parameterless constructor (satisfying Avalonia)</item>
/// <item>Calling Initialize() with all dependencies (completing setup)</item>
/// <item>Returning a fully functional window ready for Show()</item>
/// </list>
/// </para>
/// <para>
/// <strong>Simplified Dependencies:</strong> With the introduction of <see cref="IEmulatorCoreInterface"/>
/// observable accessors, this factory now only needs 3 dependencies instead of 4. The frame provider is
/// accessed through <see cref="IEmulatorCoreInterface.FrameProvider"/>, eliminating redundant parameters.
/// </para>
/// <para>
/// <strong>Dependency Injection Pattern:</strong> This factory is registered as a singleton in the DI
/// container and receives all MainWindow dependencies. When Create() is called, it transfers those
/// dependencies to the new MainWindow instance.
/// </para>
/// <para>
/// <strong>Thread Safety:</strong> This factory is NOT thread-safe for concurrent Create() calls, but
/// this is acceptable because Create() is only called once during application startup from the UI thread.
/// </para>
/// <para>
/// <strong>Usage Example:</strong>
/// <code>
/// // In DI container registration (Program.cs or similar):
/// services.AddSingleton&lt;IMainWindowFactory, MainWindowFactory&gt;();
/// 
/// // In application startup:
/// var factory = serviceProvider.GetRequiredService&lt;IMainWindowFactory&gt;();
/// var mainWindow = factory.Create();
/// mainWindow.Show();
/// </code>
/// </para>
/// </remarks>
/// <param name="viewModel">
/// The main window view model containing UI state and commands. Must not be null.
/// </param>
/// <param name="machine">
/// The emulator core control interface (IEmulatorCoreInterface) providing the complete control surface
/// for the emulator. This single interface provides command queueing (Reset, EnqueueKey, etc.),
/// execution control (RunAsync, Clock, ThrottleEnabled), and observable accessors (EmulatorState,
/// FrameProvider, SystemStatus). Must not be null.
/// </param>
/// <param name="refreshTicker">
/// The 60 Hz refresh ticker that triggers periodic display updates, ensuring smooth
/// animation and responsive UI. Must not be null.
/// </param>
/// <param name="driveStateService">
/// The drive state service for restoring disk images from saved state. Must not be null.
/// </param>
/// <param name="guiSettingsService">
/// The GUI settings service for loading and saving all GUI configuration. Must not be null.
/// </param>
public sealed class MainWindowFactory(
    MainWindowViewModel viewModel,
    IEmulatorCoreInterface machine,
    IRefreshTicker refreshTicker,
    IDriveStateService driveStateService,
    GuiSettingsService guiSettingsService) : IMainWindowFactory
{
    /// <summary>
    /// Main window view model containing UI state, commands, and child view models.
    /// </summary>
    /// <remarks>
    /// Validated as non-null in constructor. Passed to MainWindow.Initialize() to set up
    /// data binding and reactive subscriptions.
    /// </remarks>
    private readonly MainWindowViewModel _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

    /// <summary>
    /// Emulator core control interface providing complete control surface for the emulator.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Validated as non-null in constructor. Passed to MainWindow.Initialize() which attaches
    /// it to the Apple2Display control for keyboard input injection and emulator control commands.
    /// </para>
    /// <para>
    /// <strong>The Firm Seam:</strong> This interface is the single point of contact between the UI
    /// and emulator core. It provides:
    /// <list type="bullet">
    /// <item><strong>Command Queueing:</strong> Reset, UserReset, EnqueueKey, SetPushButton</item>
    /// <item><strong>Execution Control:</strong> RunAsync, Clock, ThrottleEnabled</item>
    /// <item><strong>Observable Accessors:</strong> EmulatorState, FrameProvider, SystemStatus</item>
    /// </list>
    /// </para>
    /// <para>
    /// Uses <see cref="IEmulatorCoreInterface"/> abstraction instead of concrete VA2M type,
    /// decoupling the UI from emulator implementation details and providing an explicit thread-safe
    /// contract that prevents accidental cross-thread calls.
    /// </para>
    /// </remarks>
    private readonly IEmulatorCoreInterface _machine = machine ?? throw new ArgumentNullException(nameof(machine));

    /// <summary>
    /// 60 Hz refresh ticker for driving periodic display updates.
    /// </summary>
    /// <remarks>
    /// Validated as non-null in constructor. Passed to MainWindow.Initialize() which subscribes
    /// to its Stream to trigger RequestRefresh() calls on the display at 60 Hz.
    /// </remarks>
    private readonly IRefreshTicker _refreshTicker = refreshTicker ?? throw new ArgumentNullException(nameof(refreshTicker));

    /// <summary>
    /// Drive state service for restoring disk images from saved state.
    /// </summary>
    /// <remarks>
    /// Validated as non-null in constructor. Passed to MainWindow.Initialize() which uses it
    /// to restore disk images during the initial startup sequence.
    /// </remarks>
    private readonly IDriveStateService _driveStateService = driveStateService ?? throw new ArgumentNullException(nameof(driveStateService));

    /// <summary>
    /// GUI settings service for loading and saving all GUI configuration.
    /// </summary>
    /// <remarks>
    /// Validated as non-null in constructor. Used to load settings before window creation
    /// and apply them to the ViewModel, ensuring correct initial state.
    /// </remarks>
    private readonly GuiSettingsService _guiSettingsService = guiSettingsService ?? throw new ArgumentNullException(nameof(guiSettingsService));

    /// <summary>
    /// Creates a new <see cref="MainWindow"/> instance and initializes it with all dependencies.
    /// </summary>
    /// <returns>
    /// A fully initialized MainWindow ready to be shown. All dependencies are injected,
    /// reactive subscriptions are set up, position/size restored, and settings loaded.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <strong>Atomic Operation:</strong> This method performs both construction and initialization
    /// as a single atomic operation, ensuring the returned window is always in a consistent,
    /// fully-initialized state.
    /// </para>
    /// <para>
    /// <strong>Creation Steps:</strong>
    /// <list type="number">
    /// <item>Call MainWindow parameterless constructor (XAML loading, minimal setup)</item>
    /// <item>Call window.Initialize() with all dependencies (complete setup)</item>
    /// <item>Restore window position/size BEFORE showing (Windows 11 compatibility)</item>
    /// <item>Return a fully-initialized window</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Simplified Dependencies:</strong> Frame provider is now accessed through
    /// <see cref="IEmulatorCoreInterface.FrameProvider"/> instead of being a separate parameter.
    /// This reduces coupling and makes the firm seam between UI and emulator more explicit.
    /// </para>
    /// <para>
    /// <strong>Windows 11 Position Restore:</strong> Window position and size are restored
    /// BEFORE the window is shown. This significantly improves the chance that Windows 11
    /// will respect the saved position instead of applying its own "smart" placement algorithm.
    /// </para>
    /// <para>
    /// <strong>Multi-Monitor Support:</strong> Validates that saved positions are still on-screen.
    /// If the monitor was disconnected or position is invalid, falls back to centered on primary.
    /// </para>
    /// <para>
    /// <strong>What's Initialized:</strong>
    /// <list type="bullet">
    /// <item>ViewModel and DataContext set</item>
    /// <item>Machine attached to Apple2Display (provides FrameProvider through interface)</item>
    /// <item>SoftSwitchStatusPanel initialized with its view model</item>
    /// <item>ReactiveUI subscriptions set up (7 property subscriptions + 4 command bridges)</item>
    /// <item>Window position/size restored from saved settings</item>
    /// <item>Display settings restored from configuration file</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Exception Safety:</strong> If Initialize() throws an exception (e.g., ScreenDisplay
    /// control not found in XAML), the exception propagates to the caller. The partially-constructed
    /// window should be discarded.
    /// </para>
    /// <para>
    /// <strong>Usage:</strong>
    /// <code>
    /// var factory = serviceProvider.GetRequiredService&lt;IMainWindowFactory&gt;();
    /// var window = factory.Create();
    /// window.Show(); // Window is ready to show at saved position
    /// </code>
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown by MainWindow.Initialize() if called more than once (shouldn't happen in normal usage)
    /// or if required controls (e.g., ScreenDisplay) are not found in XAML.
    /// </exception>
    public MainWindow Create()
    {
        // Load ALL settings BEFORE creating window (using async-over-sync pattern)
        // This ensures all ViewModel properties have correct values when XAML bindings activate
        var guiSettings = Task.Run(async () => await _guiSettingsService.LoadAsync()).Result;

        // Apply settings to ViewModel
        GuiSettingsService.ApplyToViewModel(_viewModel, guiSettings);

        // Restore drive state from master settings file
        // This loads disk images into drives based on saved state
        _driveStateService.RestoreDriveState(guiSettings.DriveState);

        var window = new MainWindow();
        window.Initialize(_viewModel, _machine, _refreshTicker, _driveStateService, _guiSettingsService);

        // Restore window position/size BEFORE showing (Windows 11 best practice)
        // This gives Windows 11 less opportunity to override our saved position
        if (guiSettings.Window != null)
        {
            var isMaximized = guiSettings.Window.IsMaximized ?? false;

            // For maximized windows: set position/size first (as restore bounds), THEN maximize in OnOpened
            // For normal windows: just set position/size normally
            if (isMaximized)
            {
                // Set the normal bounds - these become "restore bounds" for when user un-maximizes
                if (guiSettings.Window.Left.HasValue && guiSettings.Window.Top.HasValue)
                {
                    window.Position = new Avalonia.PixelPoint(guiSettings.Window.Left.Value, guiSettings.Window.Top.Value);
                }

                if (guiSettings.Window.Width.HasValue && guiSettings.Window.Height.HasValue)
                {
                    window.Width = guiSettings.Window.Width.Value;
                    window.Height = guiSettings.Window.Height.Value;
                }

                // Don't set WindowState here - let OnOpened do it after window is shown
                // Store a flag so OnOpened knows to maximize
                window.Tag = "ShouldMaximize";
            }
            else
            {
                // Normal (non-maximized) restore - use WindowSettingsHelper for validation
                WindowSettingsHelper.Restore(window, guiSettings.Window);
            }
        }
        else
        {
            // No saved window settings - use defaults (centered)
            WindowSettingsHelper.Restore(window, null);
        }

        return window;
    }

    }
