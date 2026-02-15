// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Controls;
using ReactiveUI;
using Pandowdy.EmuCore.Interfaces;
using Pandowdy.EmuCore.Services;
using Pandowdy.UI.Interfaces;

namespace Pandowdy.UI.ViewModels;

/// <summary>
/// Main view model for the application window, coordinating child view models and UI state.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Architecture:</strong> This view model serves as the root composition point for all
/// child view models and coordinates application-level state and commands. It follows the MVVM
/// pattern with ReactiveUI for property change notifications and command execution.
/// </para>
/// <para>
/// <strong>Child View Models:</strong>
/// <list type="bullet">
/// <item><see cref="EmulatorState"/>: Displays CPU state (PC, cycles, BASIC line)</item>
/// <item><see cref="SystemStatus"/>: Displays soft switches and system status</item>
/// <item><see cref="CpuStatus"/>: Displays CPU registers and flags (60Hz update)</item>
/// </list>
/// </para>
/// <para>
/// <strong>Responsibility Separation:</strong> This view model manages UI-level state
/// (menu toggles, display options) but delegates emulator control to IEmulatorState.
/// The view (MainWindow.axaml.cs) bridges between these commands and actual emulator services.
/// </para>
/// <para>
/// <strong>Command Pattern:</strong> Uses ReactiveCommand for all user actions, enabling
/// easy binding to menu items and keyboard shortcuts while maintaining testability.
/// </para>
/// </remarks>
public sealed class MainWindowViewModel : ReactiveObject
{
    #region Child View Models

    /// <summary>
    /// Gets the view model for displaying emulator CPU state.
    /// </summary>
    /// <value>View model showing PC, cycles, and BASIC line number.</value>
    public EmulatorStateViewModel EmulatorState { get; }

    //public ErrorLogViewModel ErrorLog { get; }
    //public DisassemblyViewModel Disassembly { get; }

    /// <summary>
    /// Gets the view model for displaying system status (soft switches, buttons).
    /// </summary>
    /// <value>View model showing soft switch states and pushbutton status.</value>
    public SystemStatusViewModel SystemStatus { get; }

    /// <summary>
    /// Gets the view model for displaying disk drive status.
    /// </summary>
    /// <value>View model showing disk drive states (motor, track, disk images).</value>
    public DiskStatusPanelViewModel DiskStatus { get; }

    /// <summary>
    /// Gets the view model for displaying CPU register and flag status.
    /// </summary>
    /// <value>View model showing CPU registers (PC, A, X, Y, SP) and processor flags.</value>
    public CpuStatusPanelViewModel CpuStatus { get; }

    /// <summary>
    /// Gets the view model for the status bar (aggregates CPU status and system status).
    /// </summary>
    /// <value>View model managing status bar content including CPU state and MHz display.</value>
    public StatusBarViewModel StatusBar { get; }

    /// <summary>
    /// Gets the view model for the Peripherals menu (dynamic card/drive discovery).
    /// </summary>
    /// <value>View model managing the Peripherals menu structure with disk controllers and drives.</value>
    public PeripheralsMenuViewModel PeripheralsMenu { get; }

    #endregion

    #region Emulator Control Commands

    /// <summary>
    /// Gets the command to pause emulator execution.
    /// </summary>
    /// <value>Command that requests the emulator to pause (RunAsync loop exits).</value>
    /// <remarks>
    /// When executed, calls IEmulatorState.RequestPause() which signals the emulator
    /// to stop continuous execution. The emulator remains initialized and can be
    /// resumed with <see cref="ContinueCommand"/>.
    /// </remarks>
    public ReactiveCommand<Unit, Unit> PauseCommand { get; }
    
    /// <summary>
    /// Gets the command to continue emulator execution after pause.
    /// </summary>
    /// <value>Command that requests the emulator to resume running.</value>
    /// <remarks>
    /// When executed, calls IEmulatorState.RequestContinue() which signals the emulator
    /// to resume continuous execution (restart RunAsync loop).
    /// </remarks>
    public ReactiveCommand<Unit, Unit> ContinueCommand { get; }
    
    /// <summary>
    /// Gets the command to execute a single CPU instruction (step mode).
    /// </summary>
    /// <value>Command that executes one instruction while paused.</value>
    /// <remarks>
    /// When executed, calls IEmulatorState.RequestStep() which runs exactly one CPU
    /// clock cycle. Useful for debugging and single-stepping through code.
    /// </remarks>
    public ReactiveCommand<Unit, Unit> StepCommand { get; }

    /// <summary>
    /// Gets the command to start emulator execution.
    /// </summary>
    /// <value>Command that initiates continuous emulation.</value>
    /// <remarks>
    /// <para>
    /// <strong>Implementation Note:</strong> This command is currently a no-op placeholder.
    /// The actual start logic is handled by the view (MainWindow.axaml.cs) which calls
    /// VA2M.RunAsync() directly when the window is loaded.
    /// </para>
    /// <para>
    /// Future enhancement may move start/stop control into the view model for better testability.
    /// </para>
    /// </remarks>
    public ReactiveCommand<Unit, Unit> StartEmu { get; }
    
    /// <summary>
    /// Gets the command to stop emulator execution.
    /// </summary>
    /// <value>Command that halts continuous emulation.</value>
    /// <remarks>
    /// <para>
    /// <strong>Implementation Note:</strong> This command is currently a no-op placeholder.
    /// The actual stop logic is handled by the view which cancels the RunAsync() task.
    /// </para>
    /// <para>
    /// Future enhancement may move start/stop control into the view model for better testability.
    /// </para>
    /// </remarks>
    public ReactiveCommand<Unit, Unit> StopEmu { get; }
    
    /// <summary>
    /// Gets the command to reset the emulator (full system reset / power cycle).
    /// </summary>
    /// <value>Command that performs a cold boot reset.</value>
    /// <remarks>
    /// <para>
    /// <strong>Implementation Note:</strong> This command is currently a no-op placeholder.
    /// The actual reset logic is handled by the view which calls VA2M.Reset() directly.
    /// </para>
    /// <para>
    /// Future enhancement may move reset control into the view model for better testability.
    /// </para>
    /// </remarks>
    public ReactiveCommand<Unit, Unit> ResetEmu { get; }
    
    /// <summary>
    /// Gets the command to execute a single CPU instruction.
    /// </summary>
    /// <value>Command that steps one instruction while paused.</value>
    /// <remarks>
    /// <para>
    /// <strong>Implementation Note:</strong> This command is currently a no-op placeholder.
    /// The actual step logic is handled by <see cref="StepCommand"/> which calls
    /// IEmulatorState.RequestStep().
    /// </para>
    /// <para>
    /// This may be redundant with StepCommand and could be removed or consolidated.
    /// </para>
    /// </remarks>
    public ReactiveCommand<Unit, Unit> StepOnce { get; }

    /// <summary>
    /// Gets the command to toggle between pause and continue states.
    /// </summary>
    /// <value>Command that pauses if running, or continues if paused.</value>
    /// <remarks>
    /// <para>
    /// This is a Mac-style toggle command for the Debug menu. Instead of separate
    /// Pause and Continue menu items, a single item changes its text based on state
    /// and performs the appropriate action when invoked.
    /// </para>
    /// <para>
    /// Keyboard shortcut: F5
    /// </para>
    /// </remarks>
    public ReactiveCommand<Unit, Unit> TogglePauseOrContinue { get; }

    #endregion

    #region Emulator State Properties

    /// <summary>
    /// Backing field for IsRunning property.
    /// </summary>
    private bool _isRunning;

    /// <summary>
    /// Gets or sets whether the emulator is currently running.
    /// </summary>
    /// <value>True if emulator is executing continuously, false if paused/stopped.</value>
    /// <remarks>
    /// This property controls the availability of debug menu commands and the
    /// text shown on the Pause/Continue toggle menu item.
    /// </remarks>
    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            if (_isRunning != value)
            {
                this.RaiseAndSetIfChanged(ref _isRunning, value);
                // Notify dependent properties
                this.RaisePropertyChanged(nameof(CanPause));
                this.RaisePropertyChanged(nameof(CanContinue));
                this.RaisePropertyChanged(nameof(CanStep));
                this.RaisePropertyChanged(nameof(PauseOrContinueText));
                this.RaisePropertyChanged(nameof(CanToggleThrottle));
            }
        }
    }

    /// <summary>
    /// Gets the text for the Pause/Continue toggle menu item.
    /// </summary>
    /// <value>"_Pause" when running, "_Continue" when paused (includes mnemonic).</value>
    /// <remarks>
    /// Mac-style dynamic menu text that shows the available action based on current state.
    /// Uses underscore prefix for keyboard mnemonic (P or C).
    /// </remarks>
    public string PauseOrContinueText => IsRunning ? "_Pause" : "_Continue";

    /// <summary>
    /// Gets whether the Pause command can be executed.
    /// </summary>
    /// <value>True when emulator is running, false when paused.</value>
    public bool CanPause => IsRunning;

    /// <summary>
    /// Gets whether the Continue command can be executed.
    /// </summary>
    /// <value>True when emulator is paused, false when running.</value>
    public bool CanContinue => !IsRunning;

    /// <summary>
    /// Gets whether the Step command can be executed.
    /// </summary>
    /// <value>True when emulator is paused, false when running.</value>
    public bool CanStep => !IsRunning;

    /// <summary>
    /// Gets whether the throttle toggle command can be executed.
    /// </summary>
    /// <value>True when emulator is running, false when paused/stopped.</value>
    /// <remarks>
    /// Throttle toggling is disabled when the emulator is not running because:
    /// <list type="bullet">
    /// <item>Changing throttle while stopped has no effect</item>
    /// <item>Prevents race conditions in throttle state during start/stop transitions</item>
    /// <item>Single-step debug mode should always run unthrottled (instantaneous)</item>
    /// </list>
    /// </remarks>
    public bool CanToggleThrottle => IsRunning;

    #endregion

    #region Display Options Properties

    /// <summary>
    /// Backing field for ThrottleEnabled property.
    /// </summary>
    private bool _throttleEnabled = true;
    
    /// <summary>
    /// Gets or sets whether CPU throttling is enabled to maintain 1.023 MHz speed.
    /// </summary>
    /// <value>True to run at Apple IIe speed, false to run as fast as possible.</value>
    /// <remarks>
    /// <para>
    /// When true, the emulator runs at approximately 1.023 MHz (authentic Apple IIe speed).
    /// When false, runs unthrottled (useful for loading programs quickly or testing).
    /// </para>
    /// <para>
    /// This property is bound to a menu item checkbox. Changes are observed by the view
    /// which propagates them to the VA2M.ThrottleEnabled property.
    /// </para>
    /// </remarks>
    public bool ThrottleEnabled
    {
        get => _throttleEnabled;
        set => this.RaiseAndSetIfChanged(ref _throttleEnabled, value);
    }

    /// <summary>
    /// Backing field for CapsLockEnabled property.
    /// </summary>
    private bool _capsLockEnabled = true;
    
    /// <summary>
    /// Gets or sets whether Caps Lock emulation is enabled.
    /// </summary>
    /// <value>True to enable Caps Lock behavior, false to disable.</value>
    /// <remarks>
    /// <para>
    /// When enabled, keyboard input is converted to uppercase (matching Apple IIe behavior).
    /// When disabled, keyboard input preserves case.
    /// </para>
    /// <para>
    /// The Apple IIe keyboard was uppercase-only (no lowercase letters in ROM character set).
    /// This option allows modern users to type naturally while maintaining authentic behavior.
    /// </para>
    /// </remarks>
    public bool CapsLockEnabled
    {
        get => _capsLockEnabled;
        set => this.RaiseAndSetIfChanged(ref _capsLockEnabled, value);
    }

    /// <summary>
    /// Backing field for ShowScanLines property.
    /// </summary>
    private bool _showScanLines = true;
    
    /// <summary>
    /// Gets or sets whether CRT scanline effect is shown on the display.
    /// </summary>
    /// <value>True to show scanlines, false for smooth display.</value>
    /// <remarks>
    /// <para>
    /// When enabled, adds alternating dark lines to simulate the appearance of a CRT monitor.
    /// When disabled, shows a clean, modern display.
    /// </para>
    /// <para>
    /// This is a cosmetic option for nostalgia/authenticity. Does not affect emulation accuracy.
    /// </para>
    /// </remarks>
    public bool ShowScanLines
    {
        get => _showScanLines;
        set => this.RaiseAndSetIfChanged(ref _showScanLines, value);
    }

    /// <summary>
    /// Backing field for ForceMonochrome property.
    /// </summary>
    private bool _forceMonochrome;
    
    /// <summary>
    /// Gets or sets whether to force monochrome (green or amber) display mode.
    /// </summary>
    /// <value>True to force monochrome, false for color display.</value>
    /// <remarks>
    /// <para>
    /// When enabled, renders output in monochrome (simulating a green or amber phosphor monitor).
    /// When disabled, uses NTSC color artifact emulation.
    /// </para>
    /// <para>
    /// Many early Apple IIe users had monochrome monitors which were sharper and easier to read
    /// for text. This option replicates that experience.
    /// </para>
    /// </remarks>
    public bool ForceMonochrome
    {
        get => _forceMonochrome;
        set => this.RaiseAndSetIfChanged(ref _forceMonochrome, value);
    }

    /// <summary>
    /// Backing field for DecreaseFringing property.
    /// </summary>
    private bool _decreaseFringing;

    /// <summary>
    /// Gets or sets whether to decrease display fringing for a softer appearance.
    /// </summary>
    /// <value>True to decrease fringing, false for normal fringing.</value>
    public bool DecreaseFringing
    {
        get => _decreaseFringing;
        set => this.RaiseAndSetIfChanged(ref _decreaseFringing, value);
    }

    /// <summary>
    /// Backing field for MonoMixed property.
    /// </summary>
    private bool _monoMixed;
    
    /// <summary>
    /// Gets or sets whether to use monochrome rendering in mixed text/graphics mode.
    /// </summary>
    /// <value>True for monochrome mixed mode, false for color.</value>
    /// <remarks>
    /// <para>
    /// Mixed mode displays graphics with 4 lines of text at the bottom. This option
    /// controls whether the graphics portion is rendered in monochrome or color.
    /// </para>
    /// <para>
    /// Some programs look better in monochrome mixed mode, particularly those designed
    /// for monochrome monitors.
    /// </para>
    /// </remarks>
    public bool MonoMixed
    {
        get => _monoMixed;
        set => this.RaiseAndSetIfChanged(ref _monoMixed, value);
    }

    /// <summary>
    /// Backing field for ShowSoftSwitchStatus property.
    /// </summary>
    private bool _showSoftSwitchStatus = true;

    /// <summary>
    /// Gets or sets whether the soft switch status panel is visible.
    /// </summary>
    /// <value>True to show the status panel, false to hide it.</value>
    /// <remarks>
    /// <para>
    /// Controls the visibility of the right-side panel displaying Apple IIe soft switch
    /// states (memory mapping, video modes, ROM selection, annunciators, pushbuttons).
    /// </para>
    /// <para>
    /// Users can toggle this to maximize screen space for the display or show detailed
    /// system status for debugging and learning.
    /// </para>
    /// </remarks>
    public bool ShowSoftSwitchStatus
    {
        get => _showSoftSwitchStatus;
        set => this.RaiseAndSetIfChanged(ref _showSoftSwitchStatus, value);
    }

    /// <summary>
    /// Backing field for ShowDiskStatus property.
    /// </summary>
    private bool _showDiskStatus = false;

    /// <summary>
    /// Gets or sets whether the disk status panel is visible.
    /// </summary>
    /// <value>True to show the disk status panel, false to hide it.</value>
    /// <remarks>
    /// <para>
    /// Controls the visibility of the disk status panel displaying Disk II drive states
    /// (motor on/off, track position, disk images).
    /// </para>
    /// <para>
    /// Users can toggle this to maximize screen space for the display or show disk
    /// status for monitoring and debugging.
    /// </para>
    /// </remarks>
    public bool ShowDiskStatus
    {
        get => _showDiskStatus;
        set
        {
            if (_showDiskStatus != value)
            {
                this.RaiseAndSetIfChanged(ref _showDiskStatus, value);
                this.RaisePropertyChanged(nameof(EffectiveDiskPanelWidth));
            }
        }
    }

    /// <summary>
    /// Backing field for DiskPanelWidth property.
    /// </summary>
    private double _diskPanelWidth = 200.0;

    /// <summary>
    /// Gets or sets the width of the disk status panel.
    /// </summary>
    /// <value>Width in pixels, default 200.0.</value>
    /// <remarks>
    /// <para>
    /// Controls the width of the resizable disk status panel. This value is persisted
    /// to settings so the panel width is restored between application sessions.
    /// </para>
    /// <para>
    /// Minimum width is enforced by the GridSplitter to prevent the panel from becoming
    /// unusably narrow.
    /// </para>
    /// </remarks>
    public double DiskPanelWidth
    {
        get => _diskPanelWidth;
        set
        {
            // Clamp to valid range (150-400)
            var clampedValue = value < 150.0 ? 150.0 : (value > 400.0 ? 400.0 : value);
            if (_diskPanelWidth != clampedValue)
            {
                this.RaiseAndSetIfChanged(ref _diskPanelWidth, clampedValue);
                // Only update EffectiveDiskPanelWidth if panel is currently shown
                if (ShowDiskStatus)
                {
                    this.RaisePropertyChanged(nameof(EffectiveDiskPanelWidth));
                }
            }
        }
    }

    /// <summary>
    /// Gets or sets the effective width of the disk status panel column.
    /// </summary>
    /// <value>
    /// Returns 0 when ShowDiskStatus is false (column collapsed),
    /// or DiskPanelWidth when ShowDiskStatus is true.
    /// Setting this value updates DiskPanelWidth only when the panel is visible.
    /// </value>
    /// <remarks>
    /// This property provides two-way binding support for the GridSplitter while
    /// automatically collapsing the column when the panel is hidden. The GridSplitter
    /// can modify this property, which updates DiskPanelWidth (and persists the user's
    /// chosen width) only when the panel is visible.
    /// </remarks>
    public double EffectiveDiskPanelWidth
    {
        get => ShowDiskStatus ? DiskPanelWidth : 0.0;
        set
        {
            // Only update the actual width if the panel is shown
            // (Ignore GridSplitter attempts to resize when panel is hidden)
            if (ShowDiskStatus && value != DiskPanelWidth)
            {
                DiskPanelWidth = value;
            }
        }
    }

    #endregion

    #region Display Options Toggle Commands

    /// <summary>
    /// Gets the command to toggle CPU throttling on/off.
    /// </summary>
    /// <value>Command that inverts the ThrottleEnabled property.</value>
    /// <remarks>
    /// <para>
    /// Bound to menu item or keyboard shortcut. Updates <see cref="ThrottleEnabled"/>
    /// which the view observes to update VA2M.ThrottleEnabled.
    /// </para>
    /// <para>
    /// <strong>Execution Guard:</strong> This command can only execute when the emulator
    /// is running (<see cref="CanToggleThrottle"/>). This prevents changing throttle state
    /// during pause/stop when it would have no effect or could cause race conditions.
    /// </para>
    /// </remarks>
    public ReactiveCommand<Unit, Unit> ToggleThrottle { get; }
    
    /// <summary>
    /// Gets the command to toggle Caps Lock emulation on/off.
    /// </summary>
    /// <value>Command that inverts the CapsLockEnabled property.</value>
    /// <remarks>
    /// Bound to menu item or keyboard shortcut. Updates <see cref="CapsLockEnabled"/>
    /// which the view observes to control keyboard input transformation.
    /// </remarks>
    public ReactiveCommand<Unit, Unit> ToggleCapsLock { get; }
    
    /// <summary>
    /// Gets the command to toggle scanline display effect on/off.
    /// </summary>
    /// <value>Command that inverts the ShowScanLines property.</value>
    /// <remarks>
    /// Bound to menu item or keyboard shortcut. Updates <see cref="ShowScanLines"/>
    /// which the view observes to control display rendering.
    /// </remarks>
    public ReactiveCommand<Unit, Unit> ToggleScanLines { get; }
    
    /// <summary>
    /// Gets the command to toggle monochrome display mode on/off.
    /// </summary>
    /// <value>Command that inverts the ForceMonochrome property.</value>
    /// <remarks>
    /// Bound to menu item or keyboard shortcut. Updates <see cref="ForceMonochrome"/>
    /// which the view observes to control color vs monochrome rendering.
    /// </remarks>
    public ReactiveCommand<Unit, Unit> ToggleMonochrome { get; }
    
    /// <summary>
    /// Gets the command to toggle reduced fringing display on/off.
    /// </summary>
    /// <value>Command that inverts the DecreaseFringing property.</value>
    /// <remarks>
    /// Bound to menu item or keyboard shortcut. Updates <see cref="DecreaseFringing"/>
    /// which the view observes to control display intensity.
    /// </remarks>
    public ReactiveCommand<Unit, Unit> ToggleDecreaseFringing { get; }
    
    /// <summary>
    /// Gets the command to toggle monochrome mixed mode on/off.
    /// </summary>
    /// <value>Command that inverts the MonoMixed property.</value>
    /// <remarks>
    /// Bound to menu item or keyboard shortcut. Updates <see cref="MonoMixed"/>
    /// which the view observes to control mixed mode color rendering.
    /// </remarks>
    public ReactiveCommand<Unit, Unit> ToggleMonoMixed { get; }
    
    /// <summary>
    /// Gets the command to toggle soft switch status panel visibility on/off.
    /// </summary>
    /// <value>Command that inverts the ShowSoftSwitchStatus property.</value>
    /// <remarks>
    /// Bound to menu item or keyboard shortcut. Updates <see cref="ShowSoftSwitchStatus"/>
    /// which the view observes to control status panel visibility.
    /// </remarks>
    public ReactiveCommand<Unit, Unit> ToggleSoftSwitchStatus { get; }

    /// <summary>
    /// Gets the command to toggle disk status panel visibility on/off.
    /// </summary>
    /// <value>Command that inverts the ShowDiskStatus property.</value>
    /// <remarks>
    /// Bound to menu item or keyboard shortcut. Updates <see cref="ShowDiskStatus"/>
    /// which the view observes to control disk status panel visibility.
    /// </remarks>
    public ReactiveCommand<Unit, Unit> ToggleDiskStatus { get; }

    #endregion

    #region Private Fields

    /// <summary>
    /// Emulator state provider used for pause/continue/step commands.
    /// </summary>
    private readonly IEmulatorState _emuState;

    /// <summary>
    /// Drive state service for saving disk state on exit.
    /// </summary>
    private readonly IDriveStateService _driveStateService;

    /// <summary>
    /// Message box service for showing exit confirmation dialogs.
    /// </summary>
    private readonly IMessageBoxService _messageBoxService;

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindowViewModel"/> class.
    /// </summary>
    /// <param name="emulatorState">View model for displaying CPU state.</param>
    /// <param name="emuState">Emulator state provider for control commands.</param>
    /// <param name="systemStatus">View model for displaying system status.</param>
    /// <param name="diskStatus">View model for displaying disk drive status.</param>
    /// <param name="cpuStatus">View model for displaying CPU register and flag status.</param>
    /// <param name="statusBar">View model for displaying status bar content (aggregates CPU status and system status).</param>
    /// <param name="peripheralsMenu">View model for the Peripherals menu (dynamic card/drive discovery).</param>
    /// <param name="driveStateService">Drive state service for saving disk state on exit.</param>
    /// <param name="messageBoxService">Message box service for showing exit confirmation dialogs.</param>
    /// <remarks>
    /// <para>
    /// <strong>Dependency Injection:</strong> All dependencies are injected via constructor,
    /// enabling testability and loose coupling. Child view models are created and managed
    /// by the DI container.
    /// </para>
    /// <para>
    /// <strong>Command Initialization:</strong> All ReactiveCommands are initialized in the
    /// constructor. Commands that modify properties use inline lambdas for simplicity.
    /// </para>
    /// <para>
    /// <strong>Future View Models:</strong> ErrorLog and Disassembly view models are
    /// commented out pending implementation of those features.
    /// </para>
    /// <para>
    /// <strong>Placeholder Commands:</strong> StartEmu, StopEmu, ResetEmu, and StepOnce
    /// are currently empty commands. The view (MainWindow.axaml.cs) handles these actions
    /// directly. Future refactoring may move this logic into the view model for better
    /// separation of concerns and testability.
    /// </para>
    /// </remarks>
    public MainWindowViewModel(EmulatorStateViewModel emulatorState,
                               //ErrorLogViewModel errorLog,
                               //DisassemblyViewModel disassembly,
                               IEmulatorState emuState,
                               SystemStatusViewModel systemStatus,
                               DiskStatusPanelViewModel diskStatus,
                               CpuStatusPanelViewModel cpuStatus,
                               StatusBarViewModel statusBar,
                               PeripheralsMenuViewModel peripheralsMenu,
                               IDriveStateService driveStateService,
                               IMessageBoxService messageBoxService)
    {
        EmulatorState = emulatorState;
        //ErrorLog = errorLog;
        //Disassembly = disassembly;
        _emuState = emuState;
        SystemStatus = systemStatus;
        DiskStatus = diskStatus;
        CpuStatus = cpuStatus;
        StatusBar = statusBar;
        PeripheralsMenu = peripheralsMenu;
        _driveStateService = driveStateService;
        _messageBoxService = messageBoxService;

        // Initialize emulator control commands
        PauseCommand = ReactiveCommand.Create(() => _emuState.RequestPause());
        ContinueCommand = ReactiveCommand.Create(() => _emuState.RequestContinue());
        StepCommand = ReactiveCommand.Create(() => _emuState.RequestStep());

        // Initialize display option toggle commands
        // ToggleThrottle is guarded by CanToggleThrottle (only when running)
        var canToggleThrottle = this.WhenAnyValue(x => x.IsRunning);
        ToggleThrottle = ReactiveCommand.Create(() => { ThrottleEnabled = !ThrottleEnabled; }, canToggleThrottle);
        ToggleCapsLock = ReactiveCommand.Create(() => { CapsLockEnabled = !CapsLockEnabled; });
        ToggleScanLines = ReactiveCommand.Create(() => { ShowScanLines = !ShowScanLines; });
        ToggleMonochrome = ReactiveCommand.Create(() => { ForceMonochrome = !ForceMonochrome; });
        ToggleDecreaseFringing = ReactiveCommand.Create(() => { DecreaseFringing = !DecreaseFringing; });
        ToggleMonoMixed = ReactiveCommand.Create(() => { MonoMixed = !MonoMixed; });
        ToggleSoftSwitchStatus = ReactiveCommand.Create(() => { ShowSoftSwitchStatus = !ShowSoftSwitchStatus; });
        ToggleDiskStatus = ReactiveCommand.Create(() => { ShowDiskStatus = !ShowDiskStatus; });

        // Initialize placeholder emulator commands (view handles actual logic)
        StartEmu = ReactiveCommand.Create(() => { });
        StopEmu = ReactiveCommand.Create(() => { });
        ResetEmu = ReactiveCommand.Create(() => { });
        StepOnce = ReactiveCommand.Create(() => { });
        TogglePauseOrContinue = ReactiveCommand.Create(() => { });
    }

    #endregion

    #region Application Lifecycle

    /// <summary>
    /// Handles application exit, checking for dirty disks and saving drive state.
    /// </summary>
    /// <returns>True to allow exit, false to cancel.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Dirty Disk Confirmation:</strong> If any disk has unsaved changes,
    /// shows a confirmation dialog before exiting. User can cancel exit to save disks.
    /// </para>
    /// <para>
    /// <strong>Drive State Persistence:</strong> Always saves drive state before exit
    /// (which disks are inserted in which drives) so they can be restored on next launch.
    /// </para>
    /// </remarks>
    public async Task<bool> OnClosingAsync()
    {
        // Check for dirty disks
        var dirtyDisks = DiskStatus.Cards
            .SelectMany(card => card.Drives)
            .Where(drive => drive.IsDirty)
            .ToList();

        if (dirtyDisks.Any())
        {
            var diskList = string.Join("\n", dirtyDisks.Select(d => $"  • {d.DiskId}: {d.Filename}"));
            var message = $"The following disks have unsaved changes:\n\n{diskList}\n\nExit anyway?";

            var confirmed = await _messageBoxService.ShowConfirmationAsync(
                "Unsaved Changes",
                message);

            if (!confirmed)
            {
                return false; // Cancel exit
            }
        }

        // Drive state is now saved via GuiSettingsService in MainWindow.SaveWindowAndDisplaySettings()
        // No need to call _driveStateService.CaptureDriveStateAsync() here

        return true; // Allow exit
    }

    #endregion
}
