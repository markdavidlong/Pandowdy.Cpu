// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using ReactiveUI;
using ReactiveUI.Avalonia;
using Pandowdy.UI.ViewModels;
using Pandowdy.UI.Interfaces;
using Pandowdy.UI.Helpers;
using Pandowdy.EmuCore;
using Pandowdy.EmuCore.Interfaces;
using Pandowdy.UI._hold_;

namespace Pandowdy.UI;

/// <summary>
/// Main application window for the Pandowdy Apple IIe emulator.
/// </summary>
/// <remarks>
/// <para>
/// <strong>âš ï¸ Construction:</strong> Do not construct this class directly. Use <see cref="MainWindowFactory"/>
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
#pragma warning disable CS0169 // Field is never used - reserved for future disk image support
    private DiskReadTestTemp? mDiskReadTest;
#pragma warning restore CS0169
    
    /// <summary>
    /// Last used directory path for disk file operations.
    /// </summary>
#pragma warning disable CS0414 // Field is assigned but never used - reserved for future disk image support
    private string mLastDiskPath = "E:\\develop\\Pandowdy";
#pragma warning restore CS0414

    /// <summary>
    /// Emulator core control interface for commanding emulator operations from UI thread (injected via Initialize).
    /// </summary>
    /// <remarks>
    /// Uses the <see cref="IEmulatorCoreInterface"/> abstraction instead of concrete VA2M type.
    /// This allows the UI to remain decoupled from the emulator implementation details while
    /// providing thread-safe command queueing for Reset, UserReset, EnqueueKey, SetPushButton,
    /// and execution control via RunAsync, Clock, and ThrottleEnabled.
    /// </remarks>
    private IEmulatorCoreInterface? _machine;
    
    /// <summary>
    /// Cancellation token source for controlling emulator thread lifetime.
    /// </summary>
    private CancellationTokenSource? _emuCts;

    /// <summary>
    /// Task representing the running emulator thread.
    /// </summary>
    private Task? _emuTask;

    /// <summary>
    /// Lock object for synchronizing emulator start/stop operations.
    /// </summary>
    /// <remarks>
    /// Prevents race conditions when rapidly toggling pause/continue (F5) which could
    /// leave the emulator in an inconsistent state where _emuCts is not null but the
    /// emulator thread has already exited.
    /// </remarks>
    private readonly object _emuStateLock = new();

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
    /// Saved "normal" (non-maximized) window bounds for proper restoration.
    /// Updated whenever window size/position changes while not maximized.
    /// </summary>
    private (int Left, int Top, int Width, int Height)? _normalBounds;

    /// <summary>
    /// Tracks the previous WindowState to detect state transitions.
    /// Used to prevent capturing maximized dimensions as "normal" bounds.
    /// </summary>
    private WindowState _previousWindowState = WindowState.Normal;

    /// <summary>
    /// Circular buffer of recent window position/size changes with timestamps.
    /// Used to find the last "real" user-set bounds before maximize.
    /// </summary>
    private readonly System.Collections.Generic.Queue<(int Left, int Top, int Width, int Height, DateTime Timestamp)> _sizeHistory = new();

    /// <summary>
    /// Maximum number of size history entries to keep.
    /// </summary>
    private const int MaxSizeHistoryCount = 5;

    /// <summary>
    /// Time threshold for considering a size change "recent" (milliseconds).
    /// Sizes older than this before maximize are considered valid user-set sizes.
    /// </summary>
    private const int SizeHistoryThresholdMs = 500;

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

    #endregion

    #region Constructor and Initialization

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>âš ï¸ Important:</strong> Do not construct MainWindow directly. Use <see cref="MainWindowFactory"/>
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
        
        // Track window size changes to save "normal" bounds (for maximized restoration)
        this.PropertyChanged += OnWindowPropertyChanged;
        
        // No FindControl calls needed - controls are available via x:Name fields (with fallback)
        // Defer attaching machine/frame until Initialize, which should be called next.
    }

    /// <summary>
    /// Handles window property changes to track normal (non-maximized) bounds.
    /// </summary>
    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        // Track window state changes FIRST
        if (e.Property == WindowStateProperty)
        {
            var oldState = _previousWindowState;
            var newState = WindowState;
            System.Diagnostics.Debug.WriteLine($"[MainWindow] WindowState changed: {oldState} â†’ {newState}");
            
            _previousWindowState = newState;
            
            // If transitioning TO Maximized, find the last valid user-set size from history
            if (oldState == WindowState.Normal && newState == WindowState.Maximized)
            {
                var now = DateTime.UtcNow;
                var threshold = TimeSpan.FromMilliseconds(SizeHistoryThresholdMs);
                
                // Walk backwards through history to find a size that's old enough to be a real user action
                (int Left, int Top, int Width, int Height)? validBounds = null;
                var historyArray = _sizeHistory.ToArray();
                
                for (int i = historyArray.Length - 1; i >= 0; i--)
                {
                    var entry = historyArray[i];
                    var age = now - entry.Timestamp;
                    
                    System.Diagnostics.Debug.WriteLine($"[MainWindow] History[{i}]: {entry.Width}x{entry.Height} at ({entry.Left},{entry.Top}) age={age.TotalMilliseconds:F0}ms");
                    
                    // Find the first entry that's older than our threshold
                    if (age >= threshold)
                    {
                        validBounds = (entry.Left, entry.Top, entry.Width, entry.Height);
                        System.Diagnostics.Debug.WriteLine($"[MainWindow] Found valid pre-maximize bounds: {entry.Width}x{entry.Height} at ({entry.Left},{entry.Top})");
                        break;
                    }
                }
                
                // If we found a valid historical size, use it; otherwise fall back to current
                if (validBounds.HasValue)
                {
                    _normalBounds = validBounds.Value;
                    System.Diagnostics.Debug.WriteLine($"[MainWindow] Captured normal bounds from history: {_normalBounds.Value.Width}x{_normalBounds.Value.Height} at ({_normalBounds.Value.Left},{_normalBounds.Value.Top})");
                }
                else if (_sizeHistory.Count > 0)
                {
                    // Fall back to oldest entry in history if all are too recent
                    var oldest = historyArray[0];
                    _normalBounds = (oldest.Left, oldest.Top, oldest.Width, oldest.Height);
                    System.Diagnostics.Debug.WriteLine($"[MainWindow] Using oldest history entry: {_normalBounds.Value.Width}x{_normalBounds.Value.Height} at ({_normalBounds.Value.Left},{_normalBounds.Value.Top})");
                }
                else
                {
                    // Last resort: use current bounds (shouldn't happen if history is working)
                    _normalBounds = (Position.X, Position.Y, (int)Width, (int)Height);
                    System.Diagnostics.Debug.WriteLine($"[MainWindow] Fallback to current bounds: {_normalBounds.Value.Width}x{_normalBounds.Value.Height} at ({_normalBounds.Value.Left},{_normalBounds.Value.Top})");
                }
            }
            return;
        }
        
        // Track size changes in history ONLY when in Normal state
        if ((e.Property == WidthProperty || e.Property == HeightProperty))
        {
            // Only track when window is Normal (not maximized/minimized)
            if (WindowState == WindowState.Normal && _previousWindowState == WindowState.Normal)
            {
                var newLeft = Position.X;
                var newTop = Position.Y;
                var newWidth = (int)Width;
                var newHeight = (int)Height;
                var now = DateTime.UtcNow;
                
                // Add to circular buffer
                _sizeHistory.Enqueue((newLeft, newTop, newWidth, newHeight, now));
                
                // Keep buffer size limited
                while (_sizeHistory.Count > MaxSizeHistoryCount)
                {
                    _sizeHistory.Dequeue();
                }
                
                // Also update _normalBounds directly for non-maximize scenarios
                _normalBounds = (newLeft, newTop, newWidth, newHeight);
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Added to history: {newWidth}x{newHeight} at ({newLeft},{newTop}) (history size={_sizeHistory.Count})");
            }
        }
    }

    /// <summary>
    /// Initializes MainWindow with required dependencies (phase 2 of two-phase construction).
    /// </summary>
    /// <param name="viewModel">Main window view model containing UI state and commands.</param>
    /// <param name="machine">Emulator core control interface providing complete control surface.</param>
    /// <param name="refreshTicker">60 Hz ticker for driving display updates.</param>
    /// <exception cref="InvalidOperationException">Thrown if Initialize() is called more than once.</exception>
    /// <remarks>
    /// <para>
    /// <strong>âš ï¸ Two-Phase Construction:</strong> This method must be called exactly once
    /// immediately after construction. Do not call directly - use <see cref="MainWindowFactory.Create"/>
    /// which handles both phases automatically.
    /// </para>
    /// <para>
    /// <strong>Simplified Dependencies:</strong> With <see cref="IEmulatorCoreInterface"/> observable
    /// accessors, this method now only needs 3 parameters instead of 4. Frame provider is accessed
    /// through <see cref="IEmulatorCoreInterface.FrameProvider"/> instead of being a separate parameter.
    /// </para>
    /// <para>
    /// <strong>Dependency Injection:</strong> This method accepts:
    /// <list type="bullet">
    /// <item><strong>viewModel:</strong> Provides UI state, settings, and ReactiveCommands</item>
    /// <item><strong>machine:</strong> Emulator core interface (IEmulatorCoreInterface) providing:
    ///     <list type="bullet">
    ///     <item>Command queueing (Reset, EnqueueKey, etc.)</item>
    ///     <item>Execution control (RunAsync, Clock, ThrottleEnabled)</item>
    ///     <item>Observable accessors (EmulatorState, FrameProvider, SystemStatus)</item>
    ///     </list>
    /// </item>
    /// <item><strong>refreshTicker:</strong> Drives 60 Hz display updates</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>The Firm Seam:</strong> The machine parameter uses <see cref="IEmulatorCoreInterface"/>
    /// as the single point of contact between UI and emulator core. This provides:
    /// <list type="bullet">
    /// <item><strong>Explicit Contract:</strong> Everything the UI needs is defined in one interface</item>
    /// <item><strong>Thread Safety:</strong> Clear guarantees about which methods are thread-safe</item>
    /// <item><strong>Encapsulation:</strong> No access to implementation details (Bus, MemoryPool)</item>
    /// <item><strong>Testability:</strong> Single interface to mock for UI testing</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Child Control Initialization:</strong> Attaches machine and frame provider to:
    /// <list type="bullet">
    /// <item><strong>ScreenDisplay:</strong> Apple2Display control for video rendering (gets FrameProvider from machine)</item>
    /// <item><strong>SoftSwitchStatusPanel:</strong> Status panel for soft switch display</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Reactive Bindings:</strong> Sets up subscriptions for:
    /// <list type="bullet">
    /// <item><strong>ThrottleEnabled:</strong> Controls emulator speed limiting</item>
    /// <item><strong>CapsLockEnabled:</strong> Controls uppercase-only keyboard mode</item>
    /// <item><strong>ShowScanLines:</strong> Controls CRT scanline visual effect</item>
    /// <item><strong>ForceMonochrome:</strong> Controls color vs monochrome display</item>
    /// <item><strong>DecreaseContrast:</strong> Controls contrast reduction</item>
    /// <item><strong>MonoMixed:</strong> Controls mixed mode text defring</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Command Bridging:</strong> Links view model commands to code-behind event handlers:
    /// <list type="bullet">
    /// <item>StartEmu â†’ OnEmuStartClicked</item>
    /// <item>StopEmu â†’ OnEmuStopClicked</item>
    /// <item>ResetEmu â†’ OnEmuResetClicked</item>
    /// <item>StepOnce â†’ OnEmuStepOnceClicked</item>
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
    public void Initialize(MainWindowViewModel viewModel, IEmulatorCoreInterface machine, IRefreshTicker refreshTicker)
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
            screenDisplay.AttachFrameProvider(machine.FrameProvider);
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
                var s7 = vm.WhenAnyValue(x => x.ShowSoftSwitchStatus)
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Subscribe(v =>
                    {
                        UpdateSoftSwitchStatusVisibility(v);
                    });
                disposables.Add(s7);
                var s8 = vm.WhenAnyValue(x => x.ShowDiskStatus)
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Subscribe(v =>
                    {
                        UpdateDiskStatusVisibility(v);
                    });
                disposables.Add(s8);

                // Bridge emulator commands to actions
                var c1 = vm.StartEmu.Subscribe(_ => OnEmuStartClicked(this, new RoutedEventArgs()));
                disposables.Add(c1);
                var c2 = vm.StopEmu.Subscribe(_ => OnEmuStopClicked(this, new RoutedEventArgs()));
                disposables.Add(c2);
                var c3 = vm.ResetEmu.Subscribe(_ => OnEmuResetClicked(this, new RoutedEventArgs()));
                disposables.Add(c3);
                var c4 = vm.StepOnce.Subscribe(_ => OnEmuStepOnceClicked(this, new RoutedEventArgs()));
                disposables.Add(c4);
                var c5 = vm.TogglePauseOrContinue.Subscribe(_ => OnTogglePauseOrContinue());
                disposables.Add(c5);
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
    /// <param name="isVisible">Whether the panel should be visible.</param>
    /// <remarks>
    /// Finds the SoftSwitchStatusPanel control via x:Name or FindControl fallback and
    /// sets its IsVisible property to match the provided value.
    /// </remarks>
    private void UpdateSoftSwitchStatusVisibility(bool isVisible)
    {
        var statusPanel = SoftSwitchStatusPanel ?? this.FindControl<SoftSwitchStatusPanel>("SoftSwitchStatusPanel");
        if (statusPanel != null)
        {
            statusPanel.IsVisible = isVisible;
        }
    }

    /// <summary>
    /// Updates the visibility of the disk status panel based on current setting.
    /// </summary>
    /// <param name="isVisible">Whether the panel should be visible.</param>
    /// <remarks>
    /// Finds the DiskStatusPanel control via x:Name or FindControl fallback and
    /// sets its IsVisible property to match the provided value.
    /// </remarks>
    private void UpdateDiskStatusVisibility(bool isVisible)
    {
        var diskPanel = DiskStatusPanel ?? this.FindControl<Controls.DiskStatusPanel>("DiskStatusPanel");
        if (diskPanel != null)
        {
            diskPanel.IsVisible = isVisible;
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

    #region WindowLifecycle Events

    /// <summary>
    /// Called when the window is opened and visible.
    /// </summary>
    /// <param name="e">Event arguments.</param>
    /// <remarks>
    /// <para>
    /// <strong>Startup Sequence:</strong>
    /// <list type="number">
    /// <item>Apply Windows 11 position fallback (reapply position after 100ms delay)</item>
    /// <item>Set keyboard focus to Apple2Display control</item>
    /// <item>Start the 60 Hz refresh ticker</item>
    /// <item>Subscribe to refresh stream and trigger display updates</item>
    /// <item>Post command to start emulator (begins emulation automatically)</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Windows 11 Workaround:</strong> Even though we set position before Show() in
    /// MainWindowFactory, Windows 11 sometimes overrides it. This fallback reapplies the position
    /// 100ms after the window opens, which usually succeeds where the initial attempt failed.
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
        
        // If MainWindowFactory flagged this window to be maximized, do it now
        // (after the window is shown, so restore bounds are properly set)
        if (Tag is string tag && tag == "ShouldMaximize")
        {
            WindowState = WindowState.Maximized;
            Tag = null; // Clear the flag
        }
        
        // Windows 11 workaround: Reapply saved position after a short delay
        // This gives Windows 11 time to do its "smart" placement, then we override it with our saved position
        ApplyWindows11PositionFallback();
        
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
        // Initial startup: Reset + Start
        Dispatcher.UIThread.Post(() => InitialStartup());
    }

    /// <summary>
    /// Applies Windows 11 position fallback by reapplying saved position after a delay.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Why This Works:</strong> Windows 11 has a window placement algorithm that runs
    /// after the window is shown. By waiting 100ms and then reapplying the position, we give
    /// Windows time to do its thing, then override it with our saved position.
    /// </para>
    /// <para>
    /// <strong>Platform Detection:</strong> Only applies on Windows 11+ (build 22000+).
    /// Other platforms don't need this workaround.
    /// </para>
    /// </remarks>
    private void ApplyWindows11PositionFallback()
    {
        // Only needed on Windows 11 (build 22000+)
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            return;
        }

        Task.Delay(100).ContinueWith(_ =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    var settings = WindowSettingsHelper.Load();
                    if (settings != null && WindowStartupLocation == WindowStartupLocation.Manual)
                    {
                        // Don't apply fallback if window is maximized - the restore bounds are already set
                        if (settings.IsMaximized)
                        {
                            return;
                        }
                        
                        // Reapply position - often succeeds where the initial attempt failed
                        Position = new Avalonia.PixelPoint(settings.Left, settings.Top);
                        
                        // Only reapply size if not maximized
                        if (!settings.IsMaximized)
                        {
                            Width = settings.Width;
                            Height = settings.Height;
                        }
                    }
                }
                catch
                {
                    // Silently ignore - fallback is best-effort
                }
            });
        });
    }

    /// <summary>
    /// Called when the window is closing (before disposal).
    /// </summary>
    /// <param name="e">Cancel event arguments (can cancel close).</param>
    /// <remarks>
    /// <para>
    /// <strong>Shutdown Sequence:</strong>
    /// <list type="number">
    /// <item>Save window position/size to configuration file</item>
    /// <item>Save display settings to configuration file</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Important:</strong> Settings are saved in OnClosing (not OnClosed) because
    /// by the time OnClosed fires, the window's platform implementation has been disposed
    /// and we can't access Position, Width, Height, or Screens Properties.
    /// </para>
    /// <para>
    /// <strong>Settings Persistence:</strong> Saves both window geometry (position/size) and
    /// display settings (scanlines, colors, etc.) to separate configuration files for cleaner
    /// organization and independent loading. Uses tracked "normal" bounds if window is maximized
    /// to ensure proper restoration.
    /// </para>
    /// </remarks>
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        
        System.Diagnostics.Debug.WriteLine($"[MainWindow.OnClosing] WindowState={WindowState}, _normalBounds.HasValue={_normalBounds.HasValue}");
        System.Diagnostics.Debug.WriteLine($"[MainWindow.OnClosing] Current size: {(int)Width}x{(int)Height} at ({Position.X},{Position.Y})");
        
        // Save window position/size (use normal bounds if maximized)
        if (WindowState == WindowState.Maximized && _normalBounds.HasValue)
        {
            System.Diagnostics.Debug.WriteLine("[MainWindow.OnClosing] Using maximized save path with normal bounds");
            
            // Create a temporary settings object with normal bounds
            var settings = new Models.WindowSettings
            {
                Left = _normalBounds.Value.Left,
                Top = _normalBounds.Value.Top,
                Width = _normalBounds.Value.Width,
                Height = _normalBounds.Value.Height,
                IsMaximized = true,
                MonitorName = Screens.ScreenFromWindow(this)?.DisplayName,
                MonitorBounds = Screens.ScreenFromWindow(this) != null 
                    ? $"{Screens.ScreenFromWindow(this)!.Bounds.X},{Screens.ScreenFromWindow(this)!.Bounds.Y},{Screens.ScreenFromWindow(this)!.Bounds.Width},{Screens.ScreenFromWindow(this)!.Bounds.Height}"
                    : null
            };
            
            // Save with normal bounds
            try
            {
#pragma warning disable CA1869 // Cache and reuse 'JsonSerializerOptions' instances
                var json = System.Text.Json.JsonSerializer.Serialize(settings, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
#pragma warning restore CA1869
                var path = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "LydianScaleSoftware", "Pandowdy", "window-settings.json");

                var dir = System.IO.Path.GetDirectoryName(path);
                if (dir != null)
                {
                    Directory.CreateDirectory(dir);
                }
                File.WriteAllText(path, json);
                System.Diagnostics.Debug.WriteLine($"[MainWindow.OnClosing] Maximized save completed to: {path}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow.OnClosing] Maximized save failed: {ex.Message}");
                // Silently ignore - settings loss is non-fatal
            }
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("[MainWindow.OnClosing] Using normal save path (WindowSettingsHelper)");
            // Normal save for non-maximized windows
            WindowSettingsHelper.Save(this);
        }
        
        // Save display settings (scan-lines, colors, etc.)
        SaveSettingsToConfigFile();
    }

    /// <summary>
    /// Called when the window is closed (after disposal).
    /// </summary>
    /// <param name="e">Event arguments.</param>
    /// <remarks>
    /// <para>
    /// <strong>Cleanup Sequence:</strong>
    /// <list type="number">
    /// <item>Dispose refresh ticker subscription</item>
    /// <item>Stop refresh ticker</item>
    /// <item>Stop emulator thread (cancel token)</item>
    /// <item>Call base.OnClosed()</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Note:</strong> Settings are saved in OnClosing, not here, because by this point
    /// the window's platform implementation has been disposed and we can't access window properties.
    /// </para>
    /// <para>
    /// <strong>Clean Shutdown:</strong> Ensures all background threads and subscriptions are
    /// properly disposed to prevent resource leaks and allow graceful application exit.
    /// </para>
    /// </remarks>
    protected override void OnClosed(EventArgs e)
    {
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
    /// Performs initial emulator startup (called once when window opens).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Purpose:</strong> Separates initial startup from Debug→Start menu functionality.
    /// Initial startup requires a reset to establish known state, while Start menu should
    /// resume execution without resetting.
    /// </para>
    /// <para>
    /// <strong>Startup Sequence:</strong>
    /// <list type="number">
    /// <item>Reset machine to initial state (_machine.Reset())</item>
    /// <item>Start emulator thread (OnEmuStartClicked)</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Called From:</strong> OnOpened event handler after window initialization completes.
    /// </para>
    /// </remarks>
    private void InitialStartup()
    {
        if (!_depsInjected || _machine is null) { return; }

        // Reset to establish known initial state
        _machine.Reset();

        // Start emulator thread
        OnEmuStartClicked(this, new RoutedEventArgs());
    }

    /// <summary>
    /// Starts or resumes the emulator (Debug→Start menu command).
    /// </summary>
    /// <param name="sender">Event sender (menu item or command).</param>
    /// <param name="e">Routed event arguments.</param>
    /// <remarks>
    /// <para>
    /// <strong>Behavior:</strong> Starts the emulator thread if not already running.
    /// Does NOT reset the machine - preserves current CPU/memory state for debugging.
    /// Use Reset menu command (Ctrl+F12) to explicitly reset.
    /// </para>
    /// <para>
    /// <strong>Startup Sequence:</strong>
    /// <list type="number">
    /// <item>Validate dependencies are injected</item>
    /// <item>Check if emulator is already running (if so, return)</item>
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
        if (!_depsInjected || _machine is null)
        {
            return;
        }

        // Use lock to prevent race conditions with rapid F5 toggling
        lock (_emuStateLock)
        {
            // If there's a pending task that hasn't been cleaned up, wait for it
            if (_emuTask != null && !_emuTask.IsCompleted)
            {
                // Already running - don't start another
                return;
            }

            // Clean up any completed task state (handles case where continuation hasn't run yet)
            if (_emuCts != null)
            {
                _emuCts.Dispose();
                _emuCts = null;
            }
            _emuTask = null;

            // Create new cancellation token source
            _emuCts = new CancellationTokenSource();
        }

        var token = _emuCts.Token;

        // Update running state before starting
        if (ViewModel != null)
        {
            ViewModel.IsRunning = true;
        }

        _emuTask = Task.Run(async () =>
        {
            try
            {
                await _machine.RunAsync(token, 60).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation - expected during pause
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Emulator exception: {ex.Message}");
            }
        });

        // Continuation to update UI state when task completes
        _ = _emuTask.ContinueWith(t =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                lock (_emuStateLock)
                {
                    // Only clean up if this is still our active task
                    if (_emuTask == t)
                    {
                        _emuCts?.Dispose();
                        _emuCts = null;
                        _emuTask = null;
                    }
                }

                // Update running state after stopping (only if we're the one who stopped)
                if (ViewModel != null && ViewModel.IsRunning)
                {
                    // IsRunning might already be false from StopEmulator - only update if needed
                    ViewModel.IsRunning = false;
                }
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
    /// Toggles between pause and continue states (Mac-style toggle menu item).
    /// </summary>
    /// <remarks>
    /// <para>
    /// If the emulator is running, pauses it. If paused, continues execution.
    /// This provides a single F5 shortcut that does the right thing based on current state.
    /// </para>
    /// <para>
    /// The menu item text automatically updates via <see cref="MainWindowViewModel.PauseOrContinueText"/>
    /// to show "Pause" when running or "Continue" when paused.
    /// </para>
    /// </remarks>
    private void OnTogglePauseOrContinue()
    {
        if (ViewModel == null) { return; }

        if (ViewModel.IsRunning)
        {
            // Currently running - pause it
            OnEmuStopClicked(this, new RoutedEventArgs());
        }
        else
        {
            // Currently paused - continue it
            OnEmuStartClicked(this, new RoutedEventArgs());
        }
    }

    /// <summary>
    /// Handles the emulator reset command (full system reset / power cycle).
    /// </summary>
    /// <param name="sender">Event sender (menu item or command).</param>
    /// <param name="e">Routed event arguments.</param>
    /// <remarks>
    /// <para>
    /// <strong>Thread Safety:</strong> Calls machine.Reset() which enqueues the reset
    /// operation for execution on the emulator thread at the next instruction boundary.
    /// This is safe to call from the UI thread.
    /// </para>
    /// <para>
    /// <strong>Full System Reset:</strong> Performs a complete hardware reset equivalent to
    /// powering off and on the Apple IIe. Clears keyboard latch, resets CPU, soft switches,
    /// memory bank mappings, and cycle counters. This is equivalent to a cold boot.
    /// </para>
    /// <para>
    /// <strong>Instruction Atomicity:</strong> The reset will be deferred until the current
    /// CPU instruction completes, maintaining 6502 atomic instruction guarantees.
    /// </para>
    /// </remarks>
    private void OnEmuResetClicked(object? sender, RoutedEventArgs e) 
    { 
        if (_depsInjected) 
        { 
            _machine?.Reset(); 
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
    /// thread catches OperationCanceledException and exits cleanly.
    /// </para>
    /// <para>
    /// If the emulator is not running (_emuCts is null), this method does nothing.
    /// </para>
    /// <para>
    /// <strong>Race Condition Prevention:</strong> Uses lock to synchronize with
    /// OnEmuStartClicked, preventing issues when rapidly toggling F5 (pause/continue).
    /// </para>
    /// <para>
    /// <strong>UI State Update:</strong> Immediately updates IsRunning to false for
    /// responsive menu state changes, before waiting for the background thread to exit.
    /// </para>
    /// </remarks>
    private void StopEmulator()
    {
        CancellationTokenSource? cts;
        Task? task;

        lock (_emuStateLock)
        {
            cts = _emuCts;
            task = _emuTask;

            if (cts == null)
            {
                return;
            }
        }

        // Update UI state immediately for responsive feedback
        if (ViewModel != null)
        {
            ViewModel.IsRunning = false;
        }

        // Cancel the token (this signals RunAsync to exit)
        cts.Cancel();

        // Wait briefly for the task to acknowledge cancellation
        // This prevents the race condition where Start is called before the old task exits
        if (task != null)
        {
            try
            {
                // Wait up to 100ms for clean shutdown - should be near-instant
                task.Wait(100);
            }
            catch (AggregateException)
            {
                // Task may throw OperationCanceledException wrapped in AggregateException
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] StopEmulator wait exception: {ex.Message}");
            }
        }

        // Clean up state immediately (don't wait for continuation)
        lock (_emuStateLock)
        {
            if (_emuCts == cts)
            {
                _emuCts?.Dispose();
                _emuCts = null;
            }
            if (_emuTask == task)
            {
                _emuTask = null;
            }
        }
    }

    #endregion

    #region Settings Persistence

    /// <summary>
    /// Restores display settings from the configuration file.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Settings Restored:</strong>
    /// <list type="bullet">
    /// <item>ShowScanLines (CRT scanline effect)</item>
    /// <item>MonoMixed (mixed mode text defring)</item>
    /// <item>ForceMonochrome (color vs monochrome)</item>
    /// <item>DecreaseContrast (contrast reduction)</item>
    /// <item>ThrottleEnabled (CPU speed control, defaults to true)</item>
    /// <item>ShowSoftSwitchStatus (panel visibility, defaults to true)</item>
    /// <item>ShowDiskStatus (disk panel visibility, defaults to true)</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Note:</strong> Window position and size are now handled by WindowSettingsHelper
    /// and stored in a separate file (window-settings.json).
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
            // Note: Width and Height are now handled by WindowSettingsHelper
            if (ViewModel != null)
            {
                if (data.ShowScanLines.HasValue) { ViewModel.ShowScanLines = data.ShowScanLines.Value; }
                if (data.MonoMixed.HasValue) { ViewModel.MonoMixed = data.MonoMixed.Value; }
                if (data.ForceMonochrome.HasValue) { ViewModel.ForceMonochrome = data.ForceMonochrome.Value; }
                if (data.DecreaseContrast.HasValue) { ViewModel.DecreaseContrast = data.DecreaseContrast.Value; }
                if (data.ThrottleEnabled.HasValue) { ViewModel.ThrottleEnabled = data.ThrottleEnabled.Value; } else { ViewModel.ThrottleEnabled = true; }
                if (data.ShowSoftSwitchStatus.HasValue) { ViewModel.ShowSoftSwitchStatus = data.ShowSoftSwitchStatus.Value; } else { ViewModel.ShowSoftSwitchStatus = true; }
                if (data.ShowDiskStatus.HasValue) { ViewModel.ShowDiskStatus = data.ShowDiskStatus.Value; } else { ViewModel.ShowDiskStatus = false; }
            }
        }
        catch { }
    }

    /// <summary>
    /// Saves display settings to the configuration file.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Settings Saved:</strong>
    /// <list type="bullet">
    /// <item>ShowScanLines, MonoMixed, ForceMonochrome, DecreaseContrast</item>
    /// <item>ThrottleEnabled</item>
    /// <item>ShowSoftSwitchStatus</item>
    /// <item>ShowDiskStatus</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Note:</strong> Window position and size are now handled by WindowSettingsHelper
    /// and stored in a separate file (window-settings.json).
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
                // Note: Width/Height now handled by WindowSettingsHelper
                ShowScanLines = ViewModel?.ShowScanLines,
                MonoMixed = ViewModel?.MonoMixed,
                DecreaseContrast = ViewModel?.DecreaseContrast,
                ForceMonochrome = ViewModel?.ForceMonochrome,
                ThrottleEnabled = ViewModel?.ThrottleEnabled,
                ShowSoftSwitchStatus = ViewModel?.ShowSoftSwitchStatus,
                ShowDiskStatus = ViewModel?.ShowDiskStatus,
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
    /// Data transfer object for persisting display settings to JSON.
    /// </summary>
    /// <remarks>
    /// <para>
    /// All properties are nullable to distinguish between "not set" and explicit false values.
    /// </para>
    /// <para>
    /// <strong>Note:</strong> Window position and size are stored separately in window-settings.json
    /// via WindowSettingsHelper for cleaner organization and to support multi-monitor tracking.
    /// </para>
    /// </remarks>
    private sealed class SettingsConfig
    {
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
        /// <summary>Gets or sets whether the disk status panel is visible.</summary>
        public bool? ShowDiskStatus { get; set; }
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
    /// Handles the Edit > Paste menu command.
    /// </summary>
    /// <param name="sender">Event sender (menu item).</param>
    /// <param name="e">Routed event arguments.</param>
    /// <remarks>
    /// <para>
    /// <strong>Operation:</strong> Retrieves text from clipboard and feeds it character-by-character
    /// to the emulator keyboard buffer via the Apple2Display control's PasteFromClipboard method.
    /// </para>
    /// <para>
    /// <strong>Character Filtering:</strong> Non-ASCII characters (> 127) are ignored. The
    /// QueuedKeyHandler automatically queues keys and feeds them at a controlled rate (default 50ms
    /// between keys) to prevent overwhelming Apple IIe software.
    /// </para>
    /// <para>
    /// <strong>Use Cases:</strong>
    /// <list type="bullet">
    /// <item>Paste BASIC programs from external editor</item>
    /// <item>Paste DOS commands for batch execution</item>
    /// <item>Paste configuration or file paths</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Keyboard Shortcut:</strong> Accessible via Ctrl+Shift+V. Note that Ctrl+V alone
    /// is reserved for sending to the emulator (Ctrl+V = ASCII 0x16).
    /// </para>
    /// </remarks>
    private void OnPasteClicked(object? sender, RoutedEventArgs e)
    {
        var display = GetScreenDisplay();
        display?.PasteFromClipboard();
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
    /// <strong>Ctrl+Alt + Key Accelerators:</strong>
    /// <list type="bullet">
    /// <item>Ctrl+Alt+S: Toggle scanlines</item>
    /// <item>Ctrl+Alt+M: Toggle monochrome</item>
    /// <item>Ctrl+Alt+D: Toggle decrease contrast</item>
    /// <item>Ctrl+Alt+X: Toggle mono mixed mode</item>
    /// <item>Ctrl+Alt+W: Toggle soft switch status panel</item>
    /// <item>Ctrl+Alt+K: Toggle disk status panel</item>
    /// <item>Alt+F4: Close window</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Function Key Accelerators:</strong>
    /// <list type="bullet">
    /// <item>F1: Pushbutton 0 press</item>
    /// <item>F2: Pushbutton 1 press</item>
    /// <item>F5: Toggle pause/continue</item>
    /// <item>F6: Single step</item>
    /// <item>F9: Toggle throttle</item>
    /// <item>F10: Toggle caps lock</item>
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
        if ((e.KeyModifiers & KeyModifiers.Control) != 0 && (e.KeyModifiers & KeyModifiers.Alt) != 0)
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
                    ViewModel?.ToggleSoftSwitchStatus.Execute().Subscribe();
                    return true;
                case Key.K:
                    ViewModel?.ToggleDiskStatus.Execute().Subscribe();
                    return true;
            }
        }
        if ((e.KeyModifiers & KeyModifiers.Alt) != 0)
        {
            switch (e.Key)
            {
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
                ViewModel?.TogglePauseOrContinue.Execute().Subscribe();
                return true;
            case Key.F6:
                ViewModel?.StepOnce.Execute().Subscribe();
                return true;
            case Key.F9:
                ViewModel?.ToggleThrottle.Execute().Subscribe();
                return true;
            case Key.F10:
                ViewModel?.ToggleCapsLock.Execute().Subscribe();
                return true;
        }
        if ((e.KeyModifiers & KeyModifiers.Control) != 0 && (e.KeyModifiers & KeyModifiers.Shift) != 0 && e.Key == Key.D2)
        {
            _machine?.EnqueueKey(0x00);
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
            _machine?.EnqueueKey((byte)(ctrl | 0x80));
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
            _machine?.EnqueueKey((byte)(ascii.Value | 0x80));
            e.Handled = true;
            return true;
        }
        return false;
    }

    #endregion
}
