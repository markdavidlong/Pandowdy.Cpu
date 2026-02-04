// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using ReactiveUI;
using Pandowdy.EmuCore.Interfaces;
using Pandowdy.EmuCore.Services;
using System.Reactive;

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
    /// Backing field for DecreaseContrast property.
    /// </summary>
    private bool _decreaseContrast;
    
    /// <summary>
    /// Gets or sets whether to decrease display contrast for a softer appearance.
    /// </summary>
    /// <value>True to decrease contrast, false for normal contrast.</value>
    /// <remarks>
    /// Reduces the intensity difference between bright and dark pixels, simulating
    /// an aged CRT monitor or reducing eye strain.
    /// </remarks>
    public bool DecreaseContrast
    {
        get => _decreaseContrast;
        set => this.RaiseAndSetIfChanged(ref _decreaseContrast, value);
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

    #endregion

    #region Display Options Toggle Commands

    /// <summary>
    /// Gets the command to toggle CPU throttling on/off.
    /// </summary>
    /// <value>Command that inverts the ThrottleEnabled property.</value>
    /// <remarks>
    /// Bound to menu item or keyboard shortcut. Updates <see cref="ThrottleEnabled"/>
    /// which the view observes to update VA2M.ThrottleEnabled.
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
    /// Gets the command to toggle reduced contrast display on/off.
    /// </summary>
    /// <value>Command that inverts the DecreaseContrast property.</value>
    /// <remarks>
    /// Bound to menu item or keyboard shortcut. Updates <see cref="DecreaseContrast"/>
    /// which the view observes to control display intensity.
    /// </remarks>
    public ReactiveCommand<Unit, Unit> ToggleDecreaseContrast { get; }
    
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

    #endregion

    #region Private Fields

    /// <summary>
    /// Emulator state provider used for pause/continue/step commands.
    /// </summary>
    private readonly IEmulatorState _emuState;

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
                               CpuStatusPanelViewModel cpuStatus)
    {
        EmulatorState = emulatorState;
        //ErrorLog = errorLog;
        //Disassembly = disassembly;
        _emuState = emuState;
        SystemStatus = systemStatus;
        DiskStatus = diskStatus;
        CpuStatus = cpuStatus;

        // Initialize emulator control commands
        PauseCommand = ReactiveCommand.Create(() => _emuState.RequestPause());
        ContinueCommand = ReactiveCommand.Create(() => _emuState.RequestContinue());
        StepCommand = ReactiveCommand.Create(() => _emuState.RequestStep());

        // Initialize display option toggle commands
        ToggleThrottle = ReactiveCommand.Create(() => { ThrottleEnabled = !ThrottleEnabled; });
        ToggleCapsLock = ReactiveCommand.Create(() => { CapsLockEnabled = !CapsLockEnabled; });
        ToggleScanLines = ReactiveCommand.Create(() => { ShowScanLines = !ShowScanLines; });
        ToggleMonochrome = ReactiveCommand.Create(() => { ForceMonochrome = !ForceMonochrome; });
        ToggleDecreaseContrast = ReactiveCommand.Create(() => { DecreaseContrast = !DecreaseContrast; });
        ToggleMonoMixed = ReactiveCommand.Create(() => { MonoMixed = !MonoMixed; });
        ToggleSoftSwitchStatus = ReactiveCommand.Create(() => { ShowSoftSwitchStatus = !ShowSoftSwitchStatus; });

        // Initialize placeholder emulator commands (view handles actual logic)
        StartEmu = ReactiveCommand.Create(() => { });
        StopEmu = ReactiveCommand.Create(() => { });
        ResetEmu = ReactiveCommand.Create(() => { });
        StepOnce = ReactiveCommand.Create(() => { });
    }

    #endregion
}
