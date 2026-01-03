using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using ReactiveUI;
using ReactiveUI.Avalonia;
using Pandowdy.UI.ViewModels;
using Pandowdy.UI.Interfaces;
using Pandowdy.EmuCore;
using Pandowdy.EmuCore.Interfaces;
using Pandowdy.UI._hold_;

namespace Pandowdy.UI;

/// <summary>
/// Main application window for the Pandowdy Apple IIe emulator.
/// </summary>
/// <remarks>
/// <para>
/// <strong>⚠️ Construction:</strong> Do not construct this class directly. Use <see cref="MainWindowFactory"/>
/// to create properly initialized instances with all dependencies injected.
/// </para>
/// <para>
/// <strong>Architecture:</strong> This class serves as the "View" in the MVVM pattern, handling
/// UI-specific concerns like window lifecycle, keyboard focus management, and bridging between
/// the view model and emulator core components.
/// </para>
/// <para>
/// <strong>Dependency Injection Constraint:</strong> Avalonia requires windows to have a
/// parameterless constructor for XAML loading. To work around this limitation while maintaining
/// testability and avoiding service locator anti-pattern, this class uses a two-phase initialization:
/// <list type="number">
/// <item><strong>Constructor:</strong> Parameterless, called by XAML loader, minimal initialization</item>
/// <item><strong>Initialize():</strong> Called immediately after construction by MainWindowFactory, injects dependencies</item>
/// </list>
/// Both phases are handled automatically by <see cref="MainWindowFactory.Create"/>.
/// </para>
/// <para>
/// <strong>Key Responsibilities:</strong>
/// <list type="bullet">
/// <item>Window lifecycle management (OnOpened, OnClosed)</item>
/// <item>Emulator thread management (start/stop/reset)</item>
/// <item>Keyboard input routing and accelerator handling</item>
/// <item>Settings persistence (save/restore window size and preferences)</item>
/// <item>Focus management (keeping keyboard focus on display)</item>
/// <item>Bridging view model commands to emulator actions</item>
/// </list>
/// </para>
/// <para>
/// <strong>Thread Coordination:</strong> Manages cross-thread communication between:
/// <list type="bullet">
/// <item>UI thread (Avalonia dispatcher)</item>
/// <item>Emulator thread (Task.Run background thread)</item>
/// <item>Refresh ticker (60 Hz UI updates)</item>
/// </list>
/// </para>
/// <para>
/// <strong>Usage Example:</strong>
/// <code>
/// // Correct: Use the factory
/// var factory = serviceProvider.GetRequiredService&lt;IMainWindowFactory&gt;();
/// var mainWindow = factory.Create();
/// mainWindow.Show();
/// 
/// // Incorrect: Manual construction
/// var mainWindow = new MainWindow(); // Missing dependencies!
/// </code>
/// </para>
/// </remarks>
public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
{
    #region Private Fields

    //  private readonly AppHook mAppHook = new(new SimpleMessageLog());
    
    /// <summary>
    /// Temporary disk read test functionality (will be removed in future refactoring).
    /// </summary>
    private DiskReadTestTemp? mDiskReadTest;
    
    /// <summary>
    /// Last used directory path for disk file operations.
    /// </summary>
    private string mLastDiskPath = "E:\\develop\\Pandowdy";

    /// <summary>
    /// Apple IIe emulator machine instance (injected via Initialize).
    /// </summary>
    private VA2M? _machine;
    
    /// <summary>
    /// Cancellation token source for controlling emulator thread lifetime.
    /// </summary>
    private CancellationTokenSource? _emuCts;
    
    /// <summary>
    /// Task representing the running emulator thread.
    /// </summary>
    private Task? _emuTask;

    /// <summary>
    /// 60 Hz refresh ticker for driving display updates (injected via Initialize).
    /// </summary>
    private IRefreshTicker? _refreshTicker;
    
    /// <summary>
    /// Subscription to refresh ticker stream, disposed on window close.
    /// </summary>
    private IDisposable? _refreshSub;
    
    /// <summary>
    /// True while mouse pointer is over the menu bar (prevents keyboard capture).
    /// </summary>
    private bool _menuPointerActive;
    
    /// <summary>
    /// Guard flag ensuring Initialize() is called exactly once.
    /// </summary>
    private bool _depsInjected;

    /// <summary>
    /// Caps lock emulation state (default ON for Apple IIe authenticity).
    /// </summary>
    private bool _capsLockEnabled = true;
    
    /// <summary>
    /// Soft switch status panel visibility state (default visible).
    /// </summary>
    private bool _showSoftSwitchStatus = true;

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets whether caps lock emulation is currently enabled.
    /// </summary>
    /// <value>True to convert lowercase to uppercase, false to preserve case.</value>
    /// <remarks>
    /// Exposed to Apple2Display control for keyboard input processing.
    /// The Apple IIe keyboard was uppercase-only, so this defaults to true for authenticity.
    /// </remarks>
    public bool IsCapsLockEnabledForInput => _capsLockEnabled;
    
    /// <summary>
    /// Gets or sets whether the soft switch status panel is visible.
    /// </summary>
    /// <value>True to show the panel, false to hide it.</value>
    /// <remarks>
    /// Controls the visibility of the panel displaying Apple IIe soft switch states
    /// (memory mapping, video modes, pushbuttons, etc.). Persisted to settings file.
    /// </remarks>
    public bool ShowSoftSwitchStatus
    {
        get => _showSoftSwitchStatus;
        set
        {
            if (_showSoftSwitchStatus != value)
            {
                _showSoftSwitchStatus = value;
                UpdateSoftSwitchStatusVisibility();
            }
        }
    }

    #endregion

    #region Constructor and Initialization

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>⚠️ Important:</strong> Do not construct MainWindow directly. Use <see cref="MainWindowFactory"/>
    /// instead, which properly handles two-phase initialization and dependency injection.
    /// </para>
    /// <para>
    /// <strong>XAML Requirement:</strong> This parameterless constructor is required by Avalonia's
    /// XAML loader. Heavy initialization and dependency injection are deferred to the
    /// <see cref="Initialize"/> method.
    /// </para>
    /// <para>
    /// <strong>Setup:</strong>
    /// <list type="bullet">
    /// <item>Loads XAML via InitializeComponent()</item>
    /// <item>Registers menu pointer event handlers for focus management</item>
    /// <item>Does NOT attach machine or frame provider (deferred to Initialize)</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Call Sequence:</strong> This constructor is called first by the XAML loader or
    /// MainWindowFactory, immediately followed by <see cref="Initialize"/> (managed by MainWindowFactory).
    /// </para>
    /// <para>
    /// <strong>Recommended Pattern:</strong>
    /// <code>
    /// // Correct: Use factory
    /// var factory = serviceProvider.GetRequiredService&lt;IMainWindowFactory&gt;();
    /// var window = factory.Create();
    /// 
    /// // Incorrect: Manual construction (missing dependencies!)
    /// var window = new MainWindow();
    /// </code>
    /// </para>
    /// </remarks>
    public MainWindow()
    {
        InitializeComponent();
        
        // Setup menu interaction handlers using x:Name generated field or fallback
        var mainMenu = GetMainMenu();
        if (mainMenu != null)
        {
            mainMenu.PointerEntered += (_, __) => _menuPointerActive = true;
            mainMenu.PointerExited += (_, __) => _menuPointerActive = false;
        }
        
        // No FindControl calls needed - controls are available via x:Name fields (with fallback)
        // Defer attaching machine/frame until Initialize, which should be called next.
    }

    /// <summary>
    /// Initializes the MainWindow with all required dependencies.
    /// Must be called immediately after construction (handled by MainWindowFactory).
    /// </summary>
    /// <param name="viewModel">Main window view model containing UI state and commands.</param>
    /// <param name="machine">Apple IIe emulator machine instance.</param>
    /// <param name="frameProvider">Provider supplying rendered video frames.</param>
    /// <param name="refreshTicker">60 Hz ticker for driving display updates.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown if Initialize is called more than once, or if ScreenDisplay control is not found in XAML.
    /// </exception>
    /// <remarks>
    /// <para>
    /// <strong>⚠️ Important:</strong> Do not call this method directly. Use <see cref="MainWindowFactory.Create"/>
    /// which handles the complete construction and initialization sequence correctly.
    /// </para>
    /// <para>
    /// <strong>Two-Phase Initialization:</strong> This method completes the initialization started
    /// by the parameterless constructor, injecting all dependencies that couldn't be provided
    /// through constructor parameters due to Avalonia's XAML requirements.
    /// </para>
    /// <para>
    /// <strong>Why Two-Phase?</strong> Avalonia requires windows to have parameterless constructors
    /// for XAML compilation. Since we can't inject dependencies via constructor, we use this separate
    /// Initialize() method called by MainWindowFactory immediately after construction.
    /// </para>
    /// <para>
    /// <strong>Initialization Steps:</strong>
    /// <list type="number">
    /// <item>Validate single initialization (throw if called twice)</item>
    /// <item>Set ViewModel and DataContext</item>
    /// <item>Store machine and refresh ticker references</item>
    /// <item>Attach machine and frame provider to Apple2Display control</item>
    /// <item>Initialize SoftSwitchStatusPanel with its view model</item>
    /// <item>Set up ReactiveUI subscriptions for view model property changes</item>
    /// <item>Bridge command executions to emulator actions</item>
    /// <item>Restore settings from configuration file</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Reactive Subscriptions:</strong> Subscribes to view model properties and propagates
    /// changes to emulator and display:
    /// <list type="bullet">
    /// <item><strong>ThrottleEnabled:</strong> Controls CPU speed (1.023 MHz vs unthrottled)</item>
    /// <item><strong>CapsLockEnabled:</strong> Controls keyboard uppercase conversion</item>
    /// <item><strong>ShowScanLines:</strong> Controls CRT scanline effect</item>
    /// <item><strong>ForceMonochrome:</strong> Controls color vs monochrome display</item>
    /// <item><strong>DecreaseContrast:</strong> Controls contrast reduction</item>
    /// <item><strong>MonoMixed:</strong> Controls mixed mode text defring</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Command Bridging:</strong> Links view model commands to code-behind event handlers:
    /// <list type="bullet">
    /// <item>StartEmu → OnEmuStartClicked</item>
    /// <item>StopEmu → OnEmuStopClicked</item>
    /// <item>ResetEmu → OnEmuResetClicked</item>
    /// <item>StepOnce → OnEmuStepOnceClicked</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Recommended Pattern:</strong>
    /// <code>
    /// // Let the factory handle everything:
    /// var factory = serviceProvider.GetRequiredService&lt;IMainWindowFactory&gt;();
    /// var window = factory.Create(); // Calls both new MainWindow() and Initialize()
    /// </code>
    /// </para>
    /// </remarks>
    public void Initialize(MainWindowViewModel viewModel, VA2M machine, IFrameProvider frameProvider, IRefreshTicker refreshTicker)
    {
        if (_depsInjected)
        {
            throw new InvalidOperationException("Dependencies already initialized.");
        }
        _depsInjected = true;
        ViewModel = viewModel;
        DataContext = viewModel;
        _machine = machine;
        _refreshTicker = refreshTicker;

        // Initialize child controls with their dependencies
        // Use x:Name generated field or fall back to FindControl if XAML compilation didn't generate it
        var screenDisplay = ScreenDisplay ?? this.FindControl<Apple2Display>("ScreenDisplay");
        if (screenDisplay != null)
        {
            screenDisplay.AttachMachine(_machine);
            screenDisplay.AttachFrameProvider(frameProvider);
            screenDisplay.Focus();
        }
        else
        {
            throw new InvalidOperationException("ScreenDisplay control not found. Ensure x:Name='ScreenDisplay' is set in XAML.");
        }
        
        // Initialize status panel with its view model
        var statusPanel = SoftSwitchStatusPanel ?? this.FindControl<SoftSwitchStatusPanel>("SoftSwitchStatusPanel");
        statusPanel?.Initialize(viewModel.SystemStatus);

        this.WhenActivated(disposables =>
        {
            var vm = ViewModel ?? (DataContext as MainWindowViewModel);
            if (vm != null)
            {
                var s1 = vm.WhenAnyValue(x => x.ThrottleEnabled)
                    .Subscribe(v => { if (_machine != null) { _machine.ThrottleEnabled = v; } });
                disposables.Add(s1);
                var s2 = vm.WhenAnyValue(x => x.CapsLockEnabled)
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Subscribe(v =>
                    {
                        _capsLockEnabled = v;
                    });
                disposables.Add(s2);
                var s3 = vm.WhenAnyValue(x => x.ShowScanLines)
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Subscribe(v =>
                    {
                        var screen = GetScreenDisplay();
                        if (screen != null)
                        {
                            screen.ShowScanLines = v;
                            screen.RequestRefresh();
                        }
                    });
                disposables.Add(s3);
                var s4 = vm.WhenAnyValue(x => x.ForceMonochrome)
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Subscribe(v =>
                    {
                        var screen = GetScreenDisplay();
                        if (screen != null)
                        {
                            screen.ForceMono = v;
                            screen.RequestRefresh();
                        }
                    });
                disposables.Add(s4);
                var s5 = vm.WhenAnyValue(x => x.DecreaseContrast)
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Subscribe(v =>
                    {
                        var screen = GetScreenDisplay();
                        if (screen != null)
                        {
                            screen.UseNonLumaContrastMask = v;
                            screen.RequestRefresh();
                        }
                    });
                disposables.Add(s5);
                var s6 = vm.WhenAnyValue(x => x.MonoMixed)
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Subscribe(v =>
                    {
                        var screen = GetScreenDisplay();
                        if (screen != null)
                        {
                            screen.DefringeMixedText = v;
                            screen.RequestRefresh();
                        }
                    });
                disposables.Add(s6);

                // Bridge emulator commands to actions
                var c1 = vm.StartEmu.Subscribe(_ => OnEmuStartClicked(this, new RoutedEventArgs()));
                disposables.Add(c1);
                var c2 = vm.StopEmu.Subscribe(_ => OnEmuStopClicked(this, new RoutedEventArgs()));
                disposables.Add(c2);
                var c3 = vm.ResetEmu.Subscribe(_ => OnEmuResetClicked(this, new RoutedEventArgs()));
                disposables.Add(c3);
                var c4 = vm.StepOnce.Subscribe(_ => OnEmuStepOnceClicked(this, new RoutedEventArgs()));
                disposables.Add(c4);
            }
        });

        RestoreSettingsFromConfigFile();
    }

    /// <summary>
    /// Loads the XAML markup for this window.
    /// </summary>
    /// <remarks>
    /// Called by the constructor to load the MainWindow.axaml file and create the visual tree.
    /// </remarks>
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    #endregion

    #region Control Access Helpers

    /// <summary>
    /// Updates the visibility of the soft switch status panel based on current setting.
    /// </summary>
    /// <remarks>
    /// Finds the SoftSwitchStatusPanel control via x:Name or FindControl fallback and
    /// sets its IsVisible property to match _showSoftSwitchStatus.
    /// </remarks>
    private void UpdateSoftSwitchStatusVisibility()
    {
        var statusPanel = SoftSwitchStatusPanel ?? this.FindControl<SoftSwitchStatusPanel>("SoftSwitchStatusPanel");
        if (statusPanel != null)
        {
            statusPanel.IsVisible = _showSoftSwitchStatus;
        }
    }
    
    /// <summary>
    /// Gets the Apple2Display control with fallback to FindControl.
    /// </summary>
    /// <returns>Apple2Display control instance, or null if not found.</returns>
    /// <remarks>
    /// First attempts to use the x:Name generated field, falls back to FindControl if needed.
    /// </remarks>
    private Apple2Display? GetScreenDisplay() => ScreenDisplay ?? this.FindControl<Apple2Display>("ScreenDisplay");
    
    /// <summary>
    /// Gets the main menu control with fallback to FindControl.
    /// </summary>
    /// <returns>Menu control instance, or null if not found.</returns>
    /// <remarks>
    /// First attempts to use the x:Name generated field, falls back to FindControl if needed.
    /// </remarks>
    private Menu? GetMainMenu() => MainMenu ?? this.FindControl<Menu>("MainMenu");

    #endregion

    #region Window Lifecycle Events

    /// <summary>
    /// Called when the window is opened and visible.
    /// </summary>
    /// <param name="e">Event arguments.</param>
    /// <remarks>
    /// <para>
    /// <strong>Startup Sequence:</strong>
    /// <list type="number">
    /// <item>Set keyboard focus to Apple2Display control</item>
    /// <item>Start the 60 Hz refresh ticker</item>
    /// <item>Subscribe to refresh stream and trigger display updates</item>
    /// <item>Post command to start emulator (begins emulation automatically)</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Refresh Ticker:</strong> Subscribes to 60 Hz timing signal and calls
    /// screenDisplay.RequestRefresh() on each tick. All updates are marshaled to the UI
    /// thread via RxApp.MainThreadScheduler.
    /// </para>
    /// <para>
    /// <strong>Auto-Start:</strong> Uses Dispatcher.UIThread.Post to start the emulator after
    /// the window is fully opened, ensuring smooth startup without blocking the UI thread.
    /// </para>
    /// </remarks>
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (!_depsInjected) { return; }
        
        var screenDisplay = ScreenDisplay ?? this.FindControl<Apple2Display>("ScreenDisplay");
        screenDisplay?.Focus();
        
        if (_refreshTicker != null && screenDisplay != null)
        {
            _refreshTicker.Start();
            _refreshSub = _refreshTicker.Stream
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ =>
                {
                    // Signal screen that it's OK to render at 60Hz
                    screenDisplay.RequestRefresh();
                });
        }
        Dispatcher.UIThread.Post(() => OnEmuStartClicked(this, new RoutedEventArgs()));
    }

    /// <summary>
    /// Called when the window is closing.
    /// </summary>
    /// <param name="e">Event arguments.</param>
    /// <remarks>
    /// <para>
    /// <strong>Shutdown Sequence:</strong>
    /// <list type="number">
    /// <item>Save window settings to configuration file</item>
    /// <item>Dispose refresh ticker subscription</item>
    /// <item>Stop refresh ticker</item>
    /// <item>Stop emulator thread (cancel token)</item>
    /// <item>Call base.OnClosed()</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Clean Shutdown:</strong> Ensures all background threads and subscriptions are
    /// properly disposed to prevent resource leaks and allow graceful application exit.
    /// </para>
    /// </remarks>
    protected override void OnClosed(EventArgs e)
    {
        SaveSettingsToConfigFile();
        _refreshSub?.Dispose();
        _refreshSub = null;
        _refreshTicker?.Stop();
        StopEmulator();
        base.OnClosed(e);
    }

    /// <summary>
    /// Called when the window receives keyboard focus.
    /// </summary>
    /// <param name="e">Focus event arguments.</param>
    /// <remarks>
    /// If the pointer is not over the menu bar, redirects focus to the Apple2Display control
    /// to ensure keyboard input is captured for emulator input.
    /// </remarks>
    protected override void OnGotFocus(GotFocusEventArgs e)
    {
        base.OnGotFocus(e);
        if (!_menuPointerActive)
        {
            GetScreenDisplay()?.Focus();
        }
    }

    /// <summary>
    /// Called when the window receives a pointer (mouse) press event.
    /// </summary>
    /// <param name="e">Pointer pressed event arguments.</param>
    /// <remarks>
    /// If the pointer is not over the menu bar, redirects focus to the Apple2Display control
    /// to ensure keyboard input is captured for emulator input after mouse clicks.
    /// </remarks>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!_menuPointerActive)
        {
            GetScreenDisplay()?.Focus();
        }
    }

    #endregion

    #region Menu Management

    /// <summary>
    /// Checks if any submenu is currently open.
    /// </summary>
    /// <returns>True if at least one MenuItem has IsSubMenuOpen = true, false otherwise.</returns>
    /// <remarks>
    /// Used to prevent keyboard input from being sent to the emulator while menus are open.
    /// </remarks>
    private bool IsAnyMenuOpen()
    {
        var mainMenu = GetMainMenu();
        if (mainMenu == null)
        {
            return false;
        }
        foreach (var item in mainMenu.Items)
        {
            if (item is MenuItem mi && mi.IsSubMenuOpen)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Closes all open submenus and returns focus to the display.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Iterates through all menu items and sets IsSubMenuOpen = false. Clears the
    /// _menuPointerActive flag and restores focus to the Apple2Display control.
    /// </para>
    /// <para>
    /// Called when the user presses Escape or clicks outside the menu while a submenu is open.
    /// </para>
    /// </remarks>
    private void CloseAllMenus()
    {
        var mainMenu = GetMainMenu();
        if (mainMenu == null)
        {
            return;
        }
        foreach (var item in mainMenu.Items)
        {
            if (item is MenuItem mi)
            {
                mi.IsSubMenuOpen = false;
            }
        }
        _menuPointerActive = false;
        GetScreenDisplay()?.Focus();
    }

    #endregion

    #region Emulator Control Methods

    /// <summary>
    /// Handles the emulator start command.
    /// </summary>
    /// <param name="sender">Event sender (menu item or command).</param>
    /// <param name="e">Routed event arguments.</param>
    /// <remarks>
    /// <para>
    /// <strong>Startup Sequence:</strong>
    /// <list type="number">
    /// <item>Validate dependencies are injected</item>
    /// <item>Check if emulator is already running (if so, return)</item>
    /// <item>Reset machine to initial state</item>
    /// <item>Create cancellation token source</item>
    /// <item>Start Task.Run with machine.RunAsync(token, 60)</item>
    /// <item>Set up continuation to clean up on completion</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Thread Management:</strong> The emulator runs on a background thread (Task.Run)
    /// to avoid blocking the UI. The continuation ensures cleanup occurs on the UI thread
    /// via Dispatcher.UIThread.Post().
    /// </para>
    /// <para>
    /// <strong>Target Frame Rate:</strong> Passes 60 Hz target to RunAsync for 60 FPS emulation.
    /// </para>
    /// <para>
    /// <strong>Exception Handling:</strong> Catches OperationCanceledException (normal shutdown)
    /// and generic exceptions (logged but swallowed to prevent crashes).
    /// </para>
    /// </remarks>
    private async void OnEmuStartClicked(object? sender, RoutedEventArgs e)
    {
        if (!_depsInjected || _machine is null) { return; }
        if (_emuCts != null)
        {
            return;
        }
        _machine.Reset();
        _emuCts = new CancellationTokenSource();
        var token = _emuCts.Token;
        _emuTask = Task.Run(async () =>
        {
            try
            {
                await _machine.RunAsync(token, 60).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception)
            {
            }
        });
        _ = _emuTask.ContinueWith(t =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                _emuCts?.Dispose();
                _emuCts = null;
                _emuTask = null;
            });
        });
    }

    /// <summary>
    /// Handles the emulator stop command.
    /// </summary>
    /// <param name="sender">Event sender (menu item or command).</param>
    /// <param name="e">Routed event arguments.</param>
    /// <remarks>
    /// Delegates to StopEmulator() to cancel the emulator thread.
    /// </remarks>
    private void OnEmuStopClicked(object? sender, RoutedEventArgs e) => StopEmulator();
    
    /// <summary>
    /// Handles the emulator reset command (warm reset / Ctrl+Reset).
    /// </summary>
    /// <param name="sender">Event sender (menu item or command).</param>
    /// <param name="e">Routed event arguments.</param>
    /// <remarks>
    /// Calls machine.UserReset() which performs a warm reset of the Apple IIe
    /// (similar to pressing Ctrl+Reset on real hardware). Does not stop/restart the emulator thread.
    /// </remarks>
    private void OnEmuResetClicked(object? sender, RoutedEventArgs e) 
    { 
        if (_depsInjected) 
        { 
            _machine?.UserReset(); 
        } 
    }

    /// <summary>
    /// Handles the single-step command (execute one CPU instruction).
    /// </summary>
    /// <param name="sender">Event sender (menu item or command).</param>
    /// <param name="e">Routed event arguments.</param>
    /// <remarks>
    /// <para>
    /// <strong>Step Mode:</strong> Only works when the emulator is stopped (not running).
    /// Executes exactly one CPU clock cycle via machine.Clock().
    /// </para>
    /// <para>
    /// <strong>Debugging Use:</strong> Useful for debugging and single-stepping through code
    /// to observe CPU state changes one instruction at a time.
    /// </para>
    /// </remarks>
    private void OnEmuStepOnceClicked(object? sender, RoutedEventArgs e)
    {
        if (!_depsInjected || _emuCts != null || _machine is null)
        {
            return;
        }
        _machine.Clock();
    }

    /// <summary>
    /// Stops the running emulator by cancelling the background thread.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Calls _emuCts.Cancel() if the cancellation token source exists. The emulator
    /// thread catches OperationCanceledException and exits cleanly. The continuation
    /// set up in OnEmuStartClicked handles cleanup.
    /// </para>
    /// <para>
    /// If the emulator is not running (_emuCts is null), this method does nothing.
    /// </para>
    /// </remarks>
    private void StopEmulator()
    {
        if (_emuCts == null)
        {
            return;
        }
        _emuCts.Cancel();
    }

    #endregion

    #region Settings Persistence

    /// <summary>
    /// Restores window settings from the configuration file.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Settings Restored:</strong>
    /// <list type="bullet">
    /// <item>Window Width and Height</item>
    /// <item>ShowScanLines (CRT scanline effect)</item>
    /// <item>MonoMixed (mixed mode text defring)</item>
    /// <item>ForceMonochrome (color vs monochrome)</item>
    /// <item>DecreaseContrast (contrast reduction)</item>
    /// <item>ThrottleEnabled (CPU speed control, defaults to true)</item>
    /// <item>ShowSoftSwitchStatus (panel visibility, defaults to true)</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>File Location:</strong> %AppData%\LydianScaleSoftware\Pandowdy\settings.json
    /// </para>
    /// <para>
    /// <strong>Error Handling:</strong> Silently catches and ignores all exceptions (missing file,
    /// corrupt JSON, etc.). Uses default values if settings cannot be loaded.
    /// </para>
    /// </remarks>
    private void RestoreSettingsFromConfigFile()
    {
        try
        {
            var path = GetConfigPath();
            if (!File.Exists(path))
            {
                return;
            }
            var json = File.ReadAllText(path);
            var data = JsonSerializer.Deserialize<SettingsConfig>(json);
            if (data == null)
            {
                return;
            }
            if (data.Width > 0 && data.Height > 0)
            {
                Width = data.Width;
                Height = data.Height;
            }
            if (ViewModel != null)
            {
                if (data.ShowScanLines.HasValue) { ViewModel.ShowScanLines = data.ShowScanLines.Value; }
                if (data.MonoMixed.HasValue) { ViewModel.MonoMixed = data.MonoMixed.Value; }
                if (data.ForceMonochrome.HasValue) { ViewModel.ForceMonochrome = data.ForceMonochrome.Value; }
                if (data.DecreaseContrast.HasValue) { ViewModel.DecreaseContrast = data.DecreaseContrast.Value; }
                if (data.ThrottleEnabled.HasValue) { ViewModel.ThrottleEnabled = data.ThrottleEnabled.Value; } else { ViewModel.ThrottleEnabled = true; }
            }
            if (data.ShowSoftSwitchStatus.HasValue) 
            { 
                ShowSoftSwitchStatus = data.ShowSoftSwitchStatus.Value; 
            }
            else 
            { 
                ShowSoftSwitchStatus = true; 
            }
        }
        catch { }
    }

    /// <summary>
    /// Saves window settings to the configuration file.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Settings Saved:</strong>
    /// <list type="bullet">
    /// <item>Window Width and Height</item>
    /// <item>ShowScanLines, MonoMixed, ForceMonochrome, DecreaseContrast</item>
    /// <item>ThrottleEnabled</item>
    /// <item>ShowSoftSwitchStatus</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Format:</strong> JSON with indentation for human readability.
    /// </para>
    /// <para>
    /// <strong>File Location:</strong> %AppData%\LydianScaleSoftware\Pandowdy\settings.json
    /// Directory is created if it doesn't exist.
    /// </para>
    /// <para>
    /// <strong>Error Handling:</strong> Silently catches and ignores all exceptions (I/O errors,
    /// permission issues, etc.). Settings loss is non-fatal.
    /// </para>
    /// </remarks>
    private void SaveSettingsToConfigFile()
    {
        try
        {
            var data = new SettingsConfig
            {
                Width = (int)Width,
                Height = (int)Height,
                ShowScanLines = ViewModel?.ShowScanLines,
                MonoMixed = ViewModel?.MonoMixed,
                DecreaseContrast = ViewModel?.DecreaseContrast,
                ForceMonochrome = ViewModel?.ForceMonochrome,
                ThrottleEnabled = ViewModel?.ThrottleEnabled,
                ShowSoftSwitchStatus = ShowSoftSwitchStatus,
            };
#pragma warning disable CA1869 // Cache and reuse 'JsonSerializerOptions' instances
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
#pragma warning restore CA1869 // Cache and reuse 'JsonSerializerOptions' instances
            File.WriteAllText(GetConfigPath(), json);
        }
        catch { }
    }

    /// <summary>
    /// Gets the full path to the settings configuration file.
    /// </summary>
    /// <returns>Full path: %AppData%\LydianScaleSoftware\Pandowdy\settings.json</returns>
    /// <remarks>
    /// Creates the directory structure if it doesn't exist. Uses ApplicationData special
    /// folder for cross-platform compatibility.
    /// </remarks>
    private static string GetConfigPath()
    {
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(baseDir, "LydianScaleSoftware", "Pandowdy");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "settings.json");
    }

    /// <summary>
    /// Data transfer object for persisting window settings to JSON.
    /// </summary>
    /// <remarks>
    /// All properties are nullable to distinguish between "not set" and explicit false values.
    /// </remarks>
    private sealed class SettingsConfig
    {
        /// <summary>Gets or sets the window width in pixels.</summary>
        public int Width { get; set; }
        /// <summary>Gets or sets the window height in pixels.</summary>
        public int Height { get; set; }
        /// <summary>Gets or sets whether to show CRT scanlines.</summary>
        public bool? ShowScanLines { get; set; }
        /// <summary>Gets or sets whether to use monochrome in mixed mode text.</summary>
        public bool? MonoMixed { get; set; }
        /// <summary>Gets or sets whether to decrease display contrast.</summary>
        public bool? DecreaseContrast { get; set; }
        /// <summary>Gets or sets whether to force monochrome display.</summary>
        public bool? ForceMonochrome { get; set; }
        /// <summary>Gets or sets whether CPU throttling is enabled.</summary>
        public bool? ThrottleEnabled { get; set; }
        /// <summary>Gets or sets whether the soft switch status panel is visible.</summary>
        public bool? ShowSoftSwitchStatus { get; set; }
    }

    #endregion

    #region Menu and Keyboard Handlers

    /// <summary>
    /// Handles the File > Quit menu command.
    /// </summary>
    /// <param name="sender">Event sender (menu item).</param>
    /// <param name="e">Routed event arguments.</param>
    /// <remarks>
    /// Closes the window, triggering the OnClosed event which handles cleanup and settings save.
    /// </remarks>
    private void OnQuitClicked(object? sender, RoutedEventArgs e) => Close();
    
    /// <summary>
    /// Handles the View > Soft Switch Status menu command.
    /// </summary>
    /// <param name="sender">Event sender (menu item).</param>
    /// <param name="e">Routed event arguments.</param>
    /// <remarks>
    /// Toggles the visibility of the soft switch status panel.
    /// </remarks>
    private void OnToggleSoftSwitchStatusClicked(object? sender, RoutedEventArgs e)
    {
        ShowSoftSwitchStatus = !ShowSoftSwitchStatus;
    }

    /// <summary>
    /// Handles key down events at the window level (before routing to child controls).
    /// </summary>
    /// <param name="sender">Event sender (this window).</param>
    /// <param name="e">Key event arguments.</param>
    /// <remarks>
    /// <para>
    /// <strong>Processing Order:</strong>
    /// <list type="number">
    /// <item>If menu is active: Try HandleAccelerator, close menu if open, try TryInjectSpecialKey</item>
    /// <item>If menu is not active: Try HandleAccelerator, then return focus to display</item>
    /// </list>
    /// </para>
    /// <para>
    /// This ensures keyboard shortcuts work both when menus are open and when typing into
    /// the emulator display.
    /// </para>
    /// </remarks>
    private void OnMainWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (_menuPointerActive || IsAnyMenuOpen())
        {
            if (HandleAccelerator(e))
            {
                e.Handled = true;
                return;
            }
            if (IsAnyMenuOpen())
            {
                CloseAllMenus();
            }
            if (!TryInjectSpecialKey(e))
            {
            }
            return;
        }
        if (HandleAccelerator(e))
        {
            e.Handled = true;
            return;
        }
        GetScreenDisplay()?.Focus();
    }

    /// <summary>
    /// Handles key up events for Apple IIe pushbutton emulation.
    /// </summary>
    /// <param name="e">Key event arguments.</param>
    /// <remarks>
    /// <para>
    /// <strong>Pushbutton Mapping:</strong>
    /// <list type="bullet">
    /// <item>F1: Pushbutton 0 (game controller button)</item>
    /// <item>F2: Pushbutton 1 (game controller button)</item>
    /// <item>Shift (Left/Right): Pushbutton 2 (open-apple/closed-apple)</item>
    /// </list>
    /// </para>
    /// <para>
    /// Releases the corresponding pushbutton when the key is released. Marks event as handled
    /// to prevent further processing.
    /// </para>
    /// </remarks>
    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (e.Key == Key.F1)
        {
            _machine?.SetPushButton(0, false); // pushbutton released
            e.Handled = true;
        }
        else if (e.Key == Key.F2)
        {
            _machine?.SetPushButton(1, false); // pushbutton 2 released
            e.Handled = true;
        }
        else if (e.Key == Key.LeftShift || e.Key == Key.RightShift)
        {
            _machine?.SetPushButton(2, false); // pushbutton 3 (shift) released
            e.Handled = true;
        }
    }

    /// <summary>
    /// Handles key down events for Apple IIe pushbutton emulation.
    /// </summary>
    /// <param name="e">Key event arguments.</param>
    /// <remarks>
    /// <para>
    /// Currently only handles Shift key for pushbutton 2. Does NOT mark event as handled
    /// so shift combinations (Shift+F5, etc.) still work.
    /// </para>
    /// <para>
    /// F1 and F2 pushbutton presses are handled in HandleAccelerator() instead.
    /// </para>
    /// </remarks>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.LeftShift || e.Key == Key.RightShift)
        {
            _machine?.SetPushButton(2, true); // pushbutton 3 (shift) pressed
            // do not mark handled so other shift combos still work
        }
    }

    /// <summary>
    /// Handles keyboard accelerators (shortcuts) for menu commands and display options.
    /// </summary>
    /// <param name="e">Key event arguments containing key and modifiers.</param>
    /// <returns>True if accelerator was handled, false to continue processing.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Alt + Key Accelerators:</strong>
    /// <list type="bullet">
    /// <item>Alt+S: Toggle scanlines</item>
    /// <item>Alt+M: Toggle monochrome</item>
    /// <item>Alt+D: Toggle decrease contrast</item>
    /// <item>Alt+X: Toggle mono mixed mode</item>
    /// <item>Alt+W: Toggle soft switch status panel</item>
    /// <item>Alt+F4: Close window</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Function Key Accelerators:</strong>
    /// <list type="bullet">
    /// <item>F1: Pushbutton 0 press</item>
    /// <item>F2: Pushbutton 1 press</item>
    /// <item>F5: Start emulator</item>
    /// <item>F6: Toggle caps lock</item>
    /// <item>F7: Toggle throttle</item>
    /// <item>F10: Single step</item>
    /// <item>Shift+F5: Stop emulator</item>
    /// <item>Ctrl+F12: Reset emulator</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Special Key Combinations:</strong>
    /// <list type="bullet">
    /// <item>Ctrl+Shift+2: Inject null byte (0x00) to emulator</item>
    /// </list>
    /// </para>
    /// </remarks>
    private bool HandleAccelerator(KeyEventArgs e)
    {
        if ((e.KeyModifiers & KeyModifiers.Alt) != 0)
        {
            switch (e.Key)
            {
                case Key.S:
                    ViewModel?.ToggleScanLines.Execute().Subscribe();
                    return true;
                case Key.M:
                    ViewModel?.ToggleMonochrome.Execute().Subscribe();
                    return true;
                case Key.D:
                    ViewModel?.ToggleDecreaseContrast.Execute().Subscribe();
                    return true;
                case Key.X:
                    ViewModel?.ToggleMonoMixed.Execute().Subscribe();
                    return true;
                case Key.W:
                    ShowSoftSwitchStatus = !ShowSoftSwitchStatus;
                    return true;
                case Key.F4:
                    Close();
                    return true;
            }
        }
        switch (e.Key)
        {
            case Key.F1:
                _machine?.SetPushButton(0, true); // pushbutton pressed
                return true;
            case Key.F2:
                _machine?.SetPushButton(1, true); // pushbutton 2 pressed
                return true;
            case Key.F5:
                ViewModel?.StartEmu.Execute().Subscribe();
                return true;
            case Key.F6:
                ViewModel?.ToggleCapsLock.Execute().Subscribe();
                return true;
            case Key.F7:
                ViewModel?.ToggleThrottle.Execute().Subscribe();
                return true;
            case Key.F10:
                ViewModel?.StepOnce.Execute().Subscribe();
                return true;
        }
        if (e.Key == Key.F5 && (e.KeyModifiers & KeyModifiers.Shift) != 0)
        {
            ViewModel?.StopEmu.Execute().Subscribe();
            return true;
        }
        if ((e.KeyModifiers & KeyModifiers.Control) != 0 && (e.KeyModifiers & KeyModifiers.Shift) != 0 && e.Key == Key.D2)
        {
            _machine?.InjectKey(0x00);
            e.Handled = true;
            return true;
        }
        if (e.Key == Key.F12 && (e.KeyModifiers & KeyModifiers.Control) != 0)
        {
            ViewModel?.ResetEmu.Execute().Subscribe();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Attempts to inject special control keys directly to the emulator.
    /// </summary>
    /// <param name="e">Key event arguments.</param>
    /// <returns>True if key was injected, false otherwise.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Control Characters:</strong> Ctrl+A through Ctrl+Z generate ASCII control
    /// characters 0x01-0x1A, matching Apple IIe keyboard behavior.
    /// </para>
    /// <para>
    /// <strong>Special Keys:</strong>
    /// <list type="bullet">
    /// <item>Up: 0x0B</item>
    /// <item>Down: 0x0A</item>
    /// <item>Left: 0x08</item>
    /// <item>Right: 0x15</item>
    /// <item>Delete: 0x7F</item>
    /// <item>Enter: 0x0D (carriage return)</item>
    /// <item>Tab: 0x09</item>
    /// <item>Escape: 0x1B</item>
    /// <item>Backspace: 0x08 (normal), 0x7F (with Shift)</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>High Bit:</strong> All injected keys have bit 7 set (OR with 0x80) to match
    /// Apple IIe keyboard latch format.
    /// </para>
    /// </remarks>
    private bool TryInjectSpecialKey(KeyEventArgs e)
    {
        if ((e.KeyModifiers & KeyModifiers.Control) != 0 && e.Key >= Key.A && e.Key <= Key.Z)
        {
            byte ctrl = (byte)(e.Key - Key.A + 1);
            _machine?.InjectKey((byte)(ctrl | 0x80));
            e.Handled = true;
            return true;
        }
        byte? ascii = e.Key switch
        {
            Key.Up => (byte)0x0B,
            Key.Down => (byte)0x0A,
            Key.Left => (byte)0x08,
            Key.Right => (byte)0x15,
            Key.Delete => (byte)0x7F,
            Key.Enter => (byte)'\r',
            Key.Tab => (byte)'\t',
            Key.Escape => (byte)0x1B,
            Key.Back => (e.KeyModifiers & KeyModifiers.Shift) != 0 ? (byte)0x7F : (byte)0x08,
            _ => null
        };
        if (ascii.HasValue)
        {
            _machine?.InjectKey((byte)(ascii.Value | 0x80));
            e.Handled = true;
            return true;
        }
        return false;
    }

    #endregion
}
