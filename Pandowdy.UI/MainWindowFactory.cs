using System;
using Pandowdy.UI.ViewModels;
using Pandowdy.UI.Interfaces;
using Pandowdy.UI.Helpers;
using Pandowdy.EmuCore;
using Pandowdy.EmuCore.Interfaces;

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
/// The emulator core control interface (IEmulatorCoreInterface) for commanding operations from the UI thread.
/// Provides thread-safe command queueing for Reset, UserReset, EnqueueKey, SetPushButton, and execution
/// control via RunAsync, Clock, and ThrottleEnabled. Must not be null.
/// </param>
/// <param name="frameProvider">
/// The frame provider supplying rendered video frames (560x192 pixels) from the emulator
/// to the display control. Must not be null.
/// </param>
/// <param name="refreshTicker">
/// The 60 Hz refresh ticker that triggers periodic display updates, ensuring smooth
/// animation and responsive UI. Must not be null.
/// </param>
public sealed class MainWindowFactory(
    MainWindowViewModel viewModel,
    IEmulatorCoreInterface machine,
    IFrameProvider frameProvider,
    IRefreshTicker refreshTicker) : IMainWindowFactory
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
    /// Emulator core control interface for commanding operations from the UI thread.
    /// </summary>
    /// <remarks>
    /// Validated as non-null in constructor. Passed to MainWindow.Initialize() which attaches
    /// it to the Apple2Display control for keyboard input injection and emulator control commands.
    /// Uses <see cref="IEmulatorCoreInterface"/> abstraction instead of concrete VA2M type,
    /// decoupling the UI from emulator implementation details and providing an explicit thread-safe
    /// contract that prevents accidental cross-thread calls.
    /// </remarks>
    private readonly IEmulatorCoreInterface _machine = machine ?? throw new ArgumentNullException(nameof(machine));
    
    /// <summary>
    /// Frame provider supplying rendered video frames from the emulator.
    /// </summary>
    /// <remarks>
    /// Validated as non-null in constructor. Passed to MainWindow.Initialize() which attaches
    /// it to the Apple2Display control for rendering 560x192 pixel frames.
    /// </remarks>
    private readonly IFrameProvider _frameProvider = frameProvider ?? throw new ArgumentNullException(nameof(frameProvider));
    
    /// <summary>
    /// 60 Hz refresh ticker for driving periodic display updates.
    /// </summary>
    /// <remarks>
    /// Validated as non-null in constructor. Passed to MainWindow.Initialize() which subscribes
    /// to its Stream to trigger RequestRefresh() calls on the display at 60 Hz.
    /// </remarks>
    private readonly IRefreshTicker _refreshTicker = refreshTicker ?? throw new ArgumentNullException(nameof(refreshTicker));

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
    /// <item>Return fully-initialized window</item>
    /// </list>
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
    /// <item>Machine and frame provider attached to Apple2Display</item>
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
        var window = new MainWindow();
        window.Initialize(_viewModel, _machine, _frameProvider, _refreshTicker);
        
        // Restore window position/size BEFORE showing (Windows 11 best practice)
        // This gives Windows 11 less opportunity to override our saved position
        var settings = WindowSettingsHelper.Load();
        
        // For maximized windows: set position/size first (as restore bounds), THEN maximize in OnOpened
        // For normal windows: just set position/size normally
        if (settings != null && settings.IsMaximized)
        {
            // Set the normal bounds - these become "restore bounds" for when user un-maximizes
            window.Position = new Avalonia.PixelPoint(settings.Left, settings.Top);
            window.Width = settings.Width;
            window.Height = settings.Height;
            // Don't set WindowState here - let OnOpened do it after window is shown
            // Store a flag so OnOpened knows to maximize
            window.Tag = "ShouldMaximize";
        }
        else
        {
            // Normal (non-maximized) restore
            WindowSettingsHelper.Restore(window, settings);
        }
        
        return window;
    }
}
