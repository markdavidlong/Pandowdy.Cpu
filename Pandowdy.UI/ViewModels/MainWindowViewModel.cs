// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using ReactiveUI;
using Pandowdy.EmuCore.DiskII.Messages;
using Pandowdy.EmuCore.Machine;
using Pandowdy.EmuCore.Slots;
using Pandowdy.UI.Interfaces;
using Pandowdy.Project.Interfaces;

using Pandowdy.EmuCore.DiskII;
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

    /// <summary>
    /// Gets the command to toggle emulator power on/off.
    /// </summary>
    /// <value>Command that toggles between powered-on and powered-off states.</value>
    /// <remarks>
    /// <para>
    /// Power On: calls <c>DoRestart()</c> on the emulator core (cold boot) and starts
    /// the emulator thread. Power Off: stops the emulator thread, freezing all state
    /// for debugger inspection.
    /// </para>
    /// <para>
    /// The emulator starts in the powered-off state. The view handles the actual
    /// start/stop logic; this command is a placeholder bridged to MainWindow.TogglePower().
    /// </para>
    /// <para>
    /// Keyboard shortcut: Ctrl+Alt+P
    /// </para>
    /// </remarks>
    public ReactiveCommand<Unit, Unit> TogglePower { get; }

    #endregion

    #region Emulator State Properties

    /// <summary>
    /// Backing field for IsPoweredOn property.
    /// </summary>
    private bool _isPoweredOn;

    /// <summary>
    /// Gets or sets whether the emulator is currently powered on.
    /// </summary>
    /// <value>True when the emulated Apple IIe is powered on, false when off.</value>
    /// <remarks>
    /// <para>
    /// The emulator starts powered off. Toggling power on performs a cold boot
    /// (<c>DoRestart()</c>) and starts the emulator thread. Toggling power off
    /// stops the emulator thread, freezing all subsystem state.
    /// </para>
    /// <para>
    /// The Apple2Display control can observe this to blank the screen when powered off.
    /// Menu items that depend on the machine running should check both
    /// <see cref="IsPoweredOn"/> and <see cref="IsRunning"/>.
    /// </para>
    /// </remarks>
    public bool IsPoweredOn
    {
        get => _isPoweredOn;
        set
        {
            if (_isPoweredOn != value)
            {
                this.RaiseAndSetIfChanged(ref _isPoweredOn, value);
                this.RaisePropertyChanged(nameof(PowerMenuText));
                this.RaisePropertyChanged(nameof(CanPause));
                this.RaisePropertyChanged(nameof(CanContinue));
                this.RaisePropertyChanged(nameof(CanStep));
                this.RaisePropertyChanged(nameof(CanReset));
                this.RaisePropertyChanged(nameof(PauseOrContinueText));

                // Propagate power state to child panels so indicators
                // are masked when the machine is off
                DiskStatus.SetPoweredOn(value);
                CpuStatus.SetPoweredOn(value);
            }
        }
    }

    /// <summary>
    /// Gets the text for the Power toggle menu item.
    /// </summary>
    /// <value>"Power _On" when off, "Power _Off" when on (includes mnemonic).</value>
    public string PowerMenuText => _isPoweredOn ? "Power _Off" : "Power _On";

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
                this.RaisePropertyChanged(nameof(CanReset));
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
    /// <value>True when emulator is powered on and running.</value>
    public bool CanPause => IsPoweredOn && IsRunning;

    /// <summary>
    /// Gets whether the Continue command can be executed.
    /// </summary>
    /// <value>True when emulator is powered on and paused.</value>
    public bool CanContinue => IsPoweredOn && !IsRunning;

    /// <summary>
    /// Gets whether the Step command can be executed.
    /// </summary>
    /// <value>True when emulator is powered on and paused.</value>
    public bool CanStep => IsPoweredOn && !IsRunning;

    /// <summary>
    /// Gets whether the Reset command can be executed.
    /// </summary>
    /// <value>True when emulator is powered on (warm reset requires a running machine).</value>
    public bool CanReset => IsPoweredOn;

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
    /// Bound to menu item or keyboard shortcut. Updates <see cref="ThrottleEnabled"/>
    /// which the view observes to update VA2M.ThrottleEnabled. Can be toggled at any time,
    /// including while the machine is off — the setting takes effect on next start.
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

    #region Project Lifecycle Commands

    /// <summary>
    /// Gets the command to create a new Skillet project.
    /// </summary>
    /// <value>Command that prompts for location and creates a new .skillet file.</value>
    /// <remarks>
    /// If a project is already open with unsaved changes, prompts to save first.
    /// </remarks>
    public ReactiveCommand<Unit, Unit> NewProjectCommand { get; }

    /// <summary>
    /// Gets the command to open an existing Skillet project.
    /// </summary>
    /// <value>Command that prompts for .skillet file and opens it.</value>
    /// <remarks>
    /// If a project is already open with unsaved changes, prompts to save first.
    /// </remarks>
    public ReactiveCommand<Unit, Unit> OpenProjectCommand { get; }

    /// <summary>
    /// Gets the command to save the current project.
    /// </summary>
    /// <value>Command that saves the current project to its file path.</value>
    /// <remarks>
    /// Enabled when a file-based project is open and is not pristine (has disk images
    /// or unsaved changes). Disabled for ad hoc projects and pristine file-based projects.
    /// </remarks>
    public ReactiveCommand<Unit, Unit> SaveProjectCommand { get; }

    /// <summary>
    /// Gets the command to save the current project to a new location.
    /// </summary>
    /// <value>Command that prompts for new location and saves project.</value>
    /// <remarks>
    /// Enabled when the current project is not pristine (has disk images or unsaved changes).
    /// Disabled for pristine projects that have no content worth persisting.
    /// </remarks>
    public ReactiveCommand<Unit, Unit> SaveProjectAsCommand { get; }

    /// <summary>
    /// Gets the command to close the current project.
    /// </summary>
    /// <value>Command that closes the current project after checking for unsaved changes.</value>
    /// <remarks>
    /// Enabled for file-based projects (always closable) or ad hoc projects that are not
    /// pristine (have imported disk images or unsaved changes). Disabled for pristine ad hoc projects.
    /// </remarks>
    public ReactiveCommand<Unit, Unit> CloseProjectCommand { get; }

    /// <summary>
    /// Gets the command to import a disk image into the current project.
    /// </summary>
    /// <value>Command that prompts for disk image file and imports it.</value>
    /// <remarks>
    /// Only enabled when a project is open. Supports .woz, .nib, .dsk, .do, .po formats.
    /// </remarks>
    public ReactiveCommand<Unit, Unit> ImportDiskImageCommand { get; }

    /// <summary>
    /// Gets the command to export a disk image from the current project.
    /// </summary>
    /// <value>Command that prompts for disk image and export location.</value>
    /// <remarks>
    /// Enabled when the project's disk image library contains at least one image.
    /// Disabled when no disk images have been imported.
    /// </remarks>
    public ReactiveCommand<Unit, Unit> ExportDiskImageCommand { get; }

    #endregion

    #region Project State Properties

    /// <summary>
    /// Backing field for HasProject property.
    /// </summary>
    private bool _hasProject;

    /// <summary>
    /// Gets whether a Skillet project is currently open.
    /// </summary>
    /// <value>True if a project is open, false otherwise.</value>
    public bool HasProject
    {
        get => _hasProject;
        private set => this.RaiseAndSetIfChanged(ref _hasProject, value);
    }

    /// <summary>
    /// Backing field for ProjectFilePath property.
    /// </summary>
    private string _projectFilePath = string.Empty;

    /// <summary>
    /// Gets the file path of the currently open project.
    /// </summary>
    /// <value>Full path to .skillet file, or empty string if no project open.</value>
    public string ProjectFilePath
    {
        get => _projectFilePath;
        private set => this.RaiseAndSetIfChanged(ref _projectFilePath, value);
    }

    /// <summary>
    /// Backing field for HasUnsavedChanges property.
    /// </summary>
    private bool _hasUnsavedChanges;

    /// <summary>
    /// Gets whether the current project has unsaved changes.
    /// </summary>
    /// <value>True if project needs saving, false otherwise.</value>
    public bool HasUnsavedChanges
    {
        get => _hasUnsavedChanges;
        private set => this.RaiseAndSetIfChanged(ref _hasUnsavedChanges, value);
    }

    /// <summary>
    /// Backing field for IsFileBasedProject property.
    /// </summary>
    private bool _isFileBasedProject;

    /// <summary>
    /// Gets whether the current project is file-based (not ad hoc).
    /// </summary>
    /// <value>True if project has a file on disk, false for ad hoc in-memory projects.</value>
    public bool IsFileBasedProject
    {
        get => _isFileBasedProject;
        private set => this.RaiseAndSetIfChanged(ref _isFileBasedProject, value);
    }

    /// <summary>
    /// Backing field for HasDiskImages property.
    /// </summary>
    private bool _hasDiskImages;

    /// <summary>
    /// Gets whether the current project's disk image library contains any images.
    /// </summary>
    /// <value>True if at least one disk image has been imported, false otherwise.</value>
    public bool HasDiskImages
    {
        get => _hasDiskImages;
        private set => this.RaiseAndSetIfChanged(ref _hasDiskImages, value);
    }

    /// <summary>
    /// Backing field for IsProjectNotPristine property.
    /// </summary>
    private bool _isProjectNotPristine;

    /// <summary>
    /// Gets whether the current project has been modified (is not pristine).
    /// </summary>
    /// <value>
    /// True if the project has imported disk images or unsaved changes (any mutation).
    /// False for a freshly created or opened project with no modifications.
    /// </value>
    /// <remarks>
    /// A pristine project has had no changes since creation or last save — no imported
    /// disks, no settings overrides, no mount config changes. Any mutation marks the
    /// project as dirty and makes it non-pristine. This applies to both ad hoc and
    /// file-based projects: a newly created file-based project with no changes is
    /// also pristine. Close is additionally enabled for file-based projects via
    /// the <see cref="IsFileBasedProject"/> property in the Close command guard.
    /// </remarks>
    public bool IsProjectNotPristine
    {
        get => _isProjectNotPristine;
        private set => this.RaiseAndSetIfChanged(ref _isProjectNotPristine, value);
    }

    /// <summary>
    /// Backing field for WindowTitle property.
    /// </summary>
    private string _windowTitle = "Pandowdy — untitled";

    /// <summary>
    /// Gets the window title showing the application name and current project.
    /// </summary>
    /// <value>
    /// Format: "Pandowdy — {ProjectName}" for clean projects,
    /// "Pandowdy — {ProjectName} *" for dirty projects,
    /// "Pandowdy — untitled" for ad hoc projects with no changes.
    /// </value>
    public string WindowTitle
    {
        get => _windowTitle;
        private set => this.RaiseAndSetIfChanged(ref _windowTitle, value);
    }

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

    /// <summary>
    /// File dialog service for showing disk image open/save dialogs.
    /// </summary>
    private readonly IDiskFileDialogService _diskFileDialogService;

    /// <summary>
    /// File dialog service for showing project file open/save dialogs.
    /// </summary>
    private readonly IProjectFileDialogService _projectFileDialogService;

    /// <summary>
    /// Emulator core interface for sending card messages (eject-all on project close).
    /// </summary>
    private readonly IEmulatorCoreInterface _emulatorCore;

    /// <summary>
    /// Project manager for accessing the current project and lifecycle operations.
    /// </summary>
    private readonly ISkilletProjectManager _projectManager;

    /// <summary>
    /// Skillet project instance for project lifecycle management (nullable - no project open initially).
    /// </summary>
    private ISkilletProject? _project;

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
    /// <param name="diskFileDialogService">File dialog service for showing disk image open/save dialogs.</param>
    /// <param name="projectFileDialogService">File dialog service for showing project file open/save dialogs.</param>
    /// <param name="emulatorCore">Emulator core interface for sending card messages (eject-all on project close).</param>
    /// <param name="projectManager">Project manager for accessing the current project.</param>
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
                               IEmulatorState emuState,
                               SystemStatusViewModel systemStatus,
                               DiskStatusPanelViewModel diskStatus,
                               CpuStatusPanelViewModel cpuStatus,
                               StatusBarViewModel statusBar,
                               PeripheralsMenuViewModel peripheralsMenu,
                               IDriveStateService driveStateService,
                               IMessageBoxService messageBoxService,
                               IDiskFileDialogService diskFileDialogService,
                               IProjectFileDialogService projectFileDialogService,
                               IEmulatorCoreInterface emulatorCore,
                               ISkilletProjectManager projectManager)
    {
        EmulatorState = emulatorState;
        _emuState = emuState;
        SystemStatus = systemStatus;
        DiskStatus = diskStatus;
        CpuStatus = cpuStatus;
        StatusBar = statusBar;
        PeripheralsMenu = peripheralsMenu;
        _driveStateService = driveStateService;
        _messageBoxService = messageBoxService;
        _diskFileDialogService = diskFileDialogService;
        _projectFileDialogService = projectFileDialogService;
        _emulatorCore = emulatorCore;
        _projectManager = projectManager;
        _project = projectManager.CurrentProject;

        // Update project state properties if project is provided
        if (_project != null)
        {
            RefreshProjectStateProperties();
        }

        // Initialize emulator control commands
        PauseCommand = ReactiveCommand.Create(() => _emuState.RequestPause());
        ContinueCommand = ReactiveCommand.Create(() => _emuState.RequestContinue());
        StepCommand = ReactiveCommand.Create(() => _emuState.RequestStep());

        // Initialize display option toggle commands
        ToggleThrottle = ReactiveCommand.Create(() => { ThrottleEnabled = !ThrottleEnabled; });
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
        TogglePower = ReactiveCommand.Create(() => { });

        // Initialize project lifecycle commands
        // Save Project: enabled for file-based projects that are not pristine
        // (importing a disk image makes the project saveable)
        var canSaveProject = this.WhenAnyValue(
            x => x.IsFileBasedProject, x => x.IsProjectNotPristine,
            (fileBased, notPristine) => fileBased && notPristine);

        // Save Project As: enabled only when project has content (not pristine)
        var canSaveProjectAs = this.WhenAnyValue(x => x.IsProjectNotPristine);

        // Close Project: enabled for file-based projects (always closable) or ad hoc with data
        var canCloseProject = this.WhenAnyValue(
            x => x.IsFileBasedProject, x => x.IsProjectNotPristine,
            (fileBased, notPristine) => fileBased || notPristine);

        // Export Disk Image: enabled only when project has disk images in the library
        var canExportDiskImage = this.WhenAnyValue(x => x.HasDiskImages);

        var hasProject = this.WhenAnyValue(x => x.HasProject);

        NewProjectCommand = ReactiveCommand.CreateFromTask(NewProjectAsync);
        OpenProjectCommand = ReactiveCommand.CreateFromTask(OpenProjectAsync);
        SaveProjectCommand = ReactiveCommand.CreateFromTask(SaveProjectAsync, canSaveProject);
        SaveProjectAsCommand = ReactiveCommand.CreateFromTask(SaveProjectAsAsync, canSaveProjectAs);
        CloseProjectCommand = ReactiveCommand.CreateFromTask(CloseProjectAsync, canCloseProject);
        ImportDiskImageCommand = ReactiveCommand.CreateFromTask(ImportDiskImageAsync, hasProject);
        ExportDiskImageCommand = ReactiveCommand.CreateFromTask(ExportDiskImageAsync, canExportDiskImage);
    }

    #endregion

    #region Project Lifecycle Methods

    /// <summary>
    /// Updates the window title based on the current project state.
    /// </summary>
    /// <remarks>
    /// Title formats per blueprint Appendix E.7:
    /// <list type="bullet">
    /// <item>Ad hoc project (clean): "Pandowdy — untitled"</item>
    /// <item>File-based project (clean): "Pandowdy — {ProjectName}"</item>
    /// <item>Dirty project: "Pandowdy — {ProjectName} *"</item>
    /// </list>
    /// </remarks>
    private void UpdateWindowTitle()
    {
        if (_project == null || _project.Metadata == null)
        {
            WindowTitle = "Pandowdy — untitled";
        }
        else
        {
            var dirtyIndicator = _project.HasUnsavedChanges ? " *" : string.Empty;
            WindowTitle = $"Pandowdy — {_project.Metadata.Name}{dirtyIndicator}";
        }
    }

    /// <summary>
    /// Refreshes all project state properties from the current <see cref="_project"/>.
    /// </summary>
    /// <remarks>
    /// Call this after any project lifecycle change (create, open, close, save as, import).
    /// Updates <see cref="HasProject"/>, <see cref="ProjectFilePath"/>,
    /// <see cref="HasUnsavedChanges"/>, <see cref="IsFileBasedProject"/>,
    /// <see cref="HasDiskImages"/>, <see cref="IsProjectNotPristine"/>,
    /// and the window title.
    /// </remarks>
    private void RefreshProjectStateProperties()
    {
        if (_project != null)
        {
            HasProject = true;
            ProjectFilePath = _project.FilePath ?? string.Empty;
            HasUnsavedChanges = _project.HasUnsavedChanges;
            IsFileBasedProject = !_project.IsAdHoc;
            HasDiskImages = _project.HasDiskImages;
            IsProjectNotPristine = _project.HasDiskImages || _project.HasUnsavedChanges;
        }
        else
        {
            HasProject = false;
            ProjectFilePath = string.Empty;
            HasUnsavedChanges = false;
            IsFileBasedProject = false;
            HasDiskImages = false;
            IsProjectNotPristine = false;
        }

        UpdateWindowTitle();
    }

    /// <summary>
    /// Creates a new Skillet project.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task NewProjectAsync()
    {
        // Check for unsaved changes in current project
        if (_project is { HasUnsavedChanges: true })
        {
            var save = await _messageBoxService.ShowConfirmationAsync(
                "Unsaved Changes",
                "You have unsaved changes. Save before creating a new project?");

            if (save)
            {
                await SaveProjectAsync();
            }
        }

        // Show new project dialog (Save As dialog with suggested name)
        var suggestedName = "untitled";
        var filePath = await _projectFileDialogService.ShowSaveProjectDialogAsync(suggestedName);

        if (filePath is null)
        {
            return;
        }

        // Extract project name from directory name, stripping the _skilletdir suffix.
        var projectName = StripSkilletDirSuffix(Path.GetFileName(filePath));

        try
        {
            // Close current project before creating new one
            if (_project != null)
            {
                await CloseProjectInternalAsync();
            }

            // Create new project via project manager
            var newProject = await _projectManager.CreateAsync(filePath, projectName);

            // Update UI state
            _project = newProject;
            RefreshProjectStateProperties();
        }
        catch (Exception ex)
        {
            await _messageBoxService.ShowErrorAsync(
                "Error Creating Project",
                $"Failed to create project: {ex.Message}");
        }
    }

    /// <summary>
    /// Opens an existing Skillet project.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task OpenProjectAsync()
    {
        // Check for unsaved changes in current project
        if (_project is { HasUnsavedChanges: true })
        {
            var save = await _messageBoxService.ShowConfirmationAsync(
                "Unsaved Changes",
                "You have unsaved changes. Save before opening another project?");

            if (save)
            {
                await SaveProjectAsync();
            }
        }

        // Show open project dialog
        var filePath = await _projectFileDialogService.ShowOpenProjectDialogAsync();

        if (filePath is null)
        {
            return;
        }

        try
        {
            // Close current project before opening new one
            if (_project != null)
            {
                await CloseProjectInternalAsync();
            }

            // Open project via project manager
            var openedProject = await _projectManager.OpenAsync(filePath);

            // Update UI state
            _project = openedProject;
            RefreshProjectStateProperties();

            // Auto-mount disk images from saved mount configuration
            await AutoMountFromConfigurationAsync();

            // Refresh library state on all drive widgets so "Mount from Library"
            // is enabled if the opened project has disk images in its library
            await RefreshAllDriveLibraryStateAsync();
        }
        catch (Exception ex)
        {
            await _messageBoxService.ShowErrorAsync(
                "Error Opening Project",
                $"Failed to open project: {ex.Message}");
        }
    }

    /// <summary>
    /// Saves the current project.
    /// </summary>
    /// <remarks>
    /// Only available for file-based projects (ad hoc projects must use Save As).
    /// Command is disabled via observable guard when project is ad hoc.
    /// </remarks>
    private async Task SaveProjectAsync()
    {
        if (_project == null || _project.IsAdHoc)
        {
            return;
        }

        try
        {
            // Persist which disks are currently mounted so they can be
            // auto-mounted when the project is reopened.
            await SaveMountConfigurationAsync();

            await _project.SaveAsync();
            RefreshProjectStateProperties();
        }
        catch (Exception ex)
        {
            await _messageBoxService.ShowErrorAsync(
                "Error Saving Project",
                $"Failed to save project: {ex.Message}");
        }
    }

    /// <summary>
    /// Saves the current project to a new location.
    /// </summary>
    /// <remarks>
    /// This command is used both for saving ad hoc projects to a file for the first time
    /// and for creating a copy of an existing project.
    /// </remarks>
    private async Task SaveProjectAsAsync()
    {
        if (_project == null)
        {
            return;
        }

        // Suggest current directory name (minus _skilletdir suffix) or "untitled" for ad hoc projects.
        // Guard against an empty result (e.g. trailing slash in FilePath) so the dialog pre-fills sensibly.
        var suggestedName = _project.IsAdHoc
            ? "untitled"
            : StripSkilletDirSuffix(Path.GetFileName(_project.FilePath!.TrimEnd(Path.DirectorySeparatorChar)));
        if (string.IsNullOrWhiteSpace(suggestedName))
        {
            suggestedName = "untitled";
        }

        var filePath = await _projectFileDialogService.ShowSaveProjectDialogAsync(suggestedName);

        if (filePath is null)
        {
            return;
        }

        try
        {
            // Persist which disks are currently mounted so they can be
            // auto-mounted when the project is reopened.
            await SaveMountConfigurationAsync();

            // Save project to new file via project manager
            await _projectManager.SaveAsAsync(filePath);

            // Update UI state (SaveAsAsync updates the project's FilePath and IsAdHoc internally)
            _project = _projectManager.CurrentProject;
            RefreshProjectStateProperties();
        }
        catch (Exception ex)
        {
            await _messageBoxService.ShowErrorAsync(
                "Error Saving Project",
                $"Failed to save project as: {ex.Message}");
        }
    }

    /// <summary>
    /// Closes the current project.
    /// </summary>
    /// <remarks>
    /// Prompts to save project-level changes (metadata, settings) and then prompts
    /// individually for each dirty disk image. After closing, a new ad hoc project
    /// is automatically created by the project manager.
    /// </remarks>
    private async Task CloseProjectAsync()
    {
        if (_project == null)
        {
            return;
        }

        // Check for project-level unsaved changes (metadata, settings, etc.)
        if (_project.HasUnsavedChanges)
        {
            var projectName = _project.IsAdHoc
                ? "untitled"
                : Path.GetFileName(_project.FilePath);

            var choice = await _messageBoxService.ShowSavePromptAsync(
                "Unsaved Project Changes",
                $"Project '{projectName}' has unsaved changes.",
                "Save Project",
                "Close Without Saving");

            switch (choice)
            {
                case SavePromptResult.Cancel:
                    return;
                case SavePromptResult.Save:
                    if (_project.IsAdHoc)
                    {
                        await SaveProjectAsAsync();
                    }
                    else
                    {
                        await SaveProjectAsync();
                    }
                    break;
                case SavePromptResult.DontSave:
                    break; // Proceed without saving project-level changes
            }
        }

        // Prompt per-disk for each dirty disk image
        var dirtyDrives = DiskStatus.Cards
            .SelectMany(card => card.Drives)
            .Where(drive => drive.IsDirty)
            .ToList();

        var drivesToDiscard = new List<DiskStatusWidgetViewModel>();
        bool saveDiskChanges = false;

        foreach (var drive in dirtyDrives)
        {
            var choice = await _messageBoxService.ShowSavePromptAsync(
                "Unsaved Disk Changes",
                $"Disk '{drive.Filename}' ({drive.DiskId}) has unsaved changes.",
                "Save Disk Data",
                "Close Without Saving");

            switch (choice)
            {
                case SavePromptResult.Cancel:
                    return; // Cancel close entirely

                case SavePromptResult.Save:
                    saveDiskChanges = true;
                    break; // Eject normally — ReturnAsync captures data

                case SavePromptResult.DontSave:
                    drivesToDiscard.Add(drive);
                    break; // Will eject with DiscardChanges
            }
        }

        // Eject drives the user chose to discard before the eject-all
        foreach (var drive in drivesToDiscard)
        {
            await _emulatorCore.SendCardMessageAsync(
                (SlotNumber)drive.SlotNumber,
                new EjectDiskMessage(drive.DriveNumber, DiscardChanges: true));
        }

        await CloseProjectInternalAsync(saveDiskChanges);
    }

    /// <summary>
    /// Internal helper to close project: ejects all disks, flushes to disk, then disposes.
    /// </summary>
    /// <param name="saveAfterEject">
    /// When true the project is saved after ejecting all disks so that dirty working
    /// blobs returned by <see cref="IDiskImageStore.ReturnAsync"/> are persisted.
    /// </param>
    /// <remarks>
    /// <list type="number">
    /// <item>Persists mount configuration.</item>
    /// <item>Ejects all mounted disks — each calls <see cref="IDiskImageStore.ReturnAsync"/>
    /// which stores dirty blobs in the in-memory project.</item>
    /// <item>Saves the project to flush blobs and manifest to disk (when requested).</item>
    /// <item>Delegates to <see cref="ISkilletProjectManager.CloseAsync"/> which disposes
    /// the project and creates a new ad hoc project.</item>
    /// </list>
    /// </remarks>
    private async Task CloseProjectInternalAsync(bool saveAfterEject = true)
    {
        if (_project == null)
        {
            return;
        }

        // Persist current mount state from the disk status panel before ejecting.
        // The status pipeline already carries DiskImageId for each drive.
        await SaveMountConfigurationAsync();

        // Eject all mounted disks — returns each InternalDiskImage to the store
        // via ReturnAsync before the project is closed.
        // Broadcast to all slots (null = all) so every controller card ejects its drives.
        await _emulatorCore.SendCardMessageAsync(null, new EjectAllDisksMessage());

        // Flush the project to disk so that working blobs captured by ReturnAsync
        // (and mount config changes) are not lost when the project is disposed.
        if (saveAfterEject && !_project.IsAdHoc)
        {
            try
            {
                await _project.SaveAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[CloseProjectInternalAsync] Error saving project after eject: {ex.Message}");
            }
        }

        // Close the current project and create a new ad hoc project
        await _projectManager.CloseAsync();
        _project = _projectManager.CurrentProject;

        RefreshProjectStateProperties();

        // Refresh library state on all drive widgets — the new ad hoc project
        // has an empty library, so "Mount from Library" should be disabled
        await RefreshAllDriveLibraryStateAsync();
    }

    /// <summary>
    /// Reads the project's mount_configuration table and sends <see cref="MountDiskMessage"/>
    /// for each entry with <c>auto_mount = true</c> and a non-null disk image ID.
    /// </summary>
    /// <remarks>
    /// Called after opening or creating a project to restore the drive states that were
    /// saved when the project was last closed. Errors mounting individual disks are logged
    /// but do not prevent other disks from mounting.
    /// </remarks>
    private async Task AutoMountFromConfigurationAsync()
    {
        if (_project == null)
        {
            return;
        }

        var mountConfigs = await _project.GetMountConfigurationAsync();

        foreach (var config in mountConfigs)
        {
            if (config is { AutoMount: true, DiskImageId: not null })
            {
                try
                {
                    await _emulatorCore.SendCardMessageAsync(
                        (SlotNumber)config.Slot,
                        new MountDiskMessage(config.DriveNumber, config.DiskImageId.Value));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[AutoMount] Failed to mount disk {config.DiskImageId} into slot {config.Slot} drive {config.DriveNumber}: {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// Persists the current drive mount state to the project's mount_configuration table.
    /// </summary>
    /// <remarks>
    /// Reads <see cref="DiskDriveStatusSnapshot.DiskImageId"/> from each drive in the
    /// disk status panel and writes slot/drive/diskImageId to mount_configuration.
    /// Called before eject-all during project close so the mount state survives
    /// the close/reopen cycle.
    /// </remarks>
    private async Task SaveMountConfigurationAsync()
    {
        if (_project == null)
        {
            return;
        }

        foreach (var card in DiskStatus.Cards)
        {
            foreach (var drive in card.Drives)
            {
                await _project.SetMountAsync(
                    drive.SlotNumber,
                    drive.DriveNumber,
                    drive.DiskImageId);
            }
        }
    }

    /// <summary>
    /// Refreshes the library state on all drive widgets to update "Mount from Library" enablement.
    /// </summary>
    /// <remarks>
    /// Called after any project lifecycle change that may affect the disk image library
    /// (open, close, import). Each widget re-queries the project's disk image list and
    /// enables or disables its mount command accordingly.
    /// </remarks>
    private async Task RefreshAllDriveLibraryStateAsync()
    {
        foreach (var card in DiskStatus.Cards)
        {
            foreach (var drive in card.Drives)
            {
                await drive.RefreshLibraryStateAsync();
            }
        }
    }

    /// <summary>
    /// Imports a disk image into the current project.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Shows a file picker dialog to select a disk image file (.woz, .nib, .dsk, .do, .po),
    /// then imports it into the current project's disk image library via
    /// <see cref="ISkilletProject.ImportDiskImageAsync"/>.
    /// </para>
    /// <para>
    /// After successful import, the disk image appears in the Mount from Library dialog
    /// and can be mounted into any drive.
    /// </para>
    /// </remarks>
    private async Task ImportDiskImageAsync()
    {
        if (_project == null)
        {
            return;
        }

        // Show file picker dialog with disk image filters
        var filePath = await _diskFileDialogService.ShowOpenFileDialogAsync();
        if (string.IsNullOrEmpty(filePath))
        {
            return; // User canceled
        }

        try
        {
            // Extract filename without extension as default name
            var defaultName = Path.GetFileNameWithoutExtension(filePath);

            // Import the disk image into the project
            var diskImageId = await _project.ImportDiskImageAsync(filePath, defaultName);

            // Refresh project state — importing a disk makes the project non-pristine,
            // enabling Save, Save As, Close, and Export commands as appropriate
            RefreshProjectStateProperties();

            // Refresh library state in all drive widgets so "Mount from Library" gets re-enabled
            await RefreshAllDriveLibraryStateAsync();

            // Show success message
            await _messageBoxService.ShowErrorAsync(
                "Import Successful",
                $"Disk image '{defaultName}' has been imported.\n\nDisk ID: {diskImageId}\n\nYou can now mount it from the Mount from Library dialog.");
        }
        catch (Exception ex)
        {
            await _messageBoxService.ShowErrorAsync(
                "Import Failed",
                $"Failed to import disk image:\n\n{ex.Message}");
        }
    }

    /// <summary>
    /// Exports a disk image from the current project.
    /// </summary>
    /// <remarks>
    /// TODO: Implement disk export workflow:
    /// 1. Show disk selection dialog (from project's disk images)
    /// 2. Show export format selection
    /// 3. Show file save dialog
    /// 4. Send ExportDiskMessage to controller
    /// This is complex UI work deferred until mount/eject workflow is fully validated.
    /// </remarks>
    private async Task ExportDiskImageAsync()
    {
        if (_project == null)
        {
            return;
        }

        // TODO: Full implementation requires disk selection UI and format picker
        await _messageBoxService.ShowErrorAsync(
            "Export Disk Image",
            "Export Disk Image feature requires additional UI components.\nThis will be implemented after mount/eject validation.");
    }

    #endregion

    #region Application Lifecycle

    /// <summary>
    /// Handles application exit, checking for dirty disks and saving drive state.
    /// </summary>
    /// <returns>True to allow exit, false to cancel.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Project Unsaved Changes:</strong> If the current project has unsaved changes,
    /// shows a confirmation dialog before exiting. User can cancel exit to save project.
    /// </para>
    /// <para>
    /// <strong>Dirty Disk Confirmation:</strong> If any disk has unsaved changes,
    /// shows a confirmation dialog before exiting. User can cancel exit to save disks.
    /// </para>
    /// <para>
    /// <strong>Project Shutdown:</strong> On confirmed exit, persists the current mount
    /// configuration to the .skillet file, saves the project (if file-based), and ejects
    /// all disks so <see cref="IDiskImageStore.ReturnAsync"/> flushes working copies back
    /// to the store before the process terminates. This mirrors the legacy
    /// <c>CaptureDriveStateSettings</c> path that saved drive state to JSON on exit.
    /// </para>
    /// </remarks>
    public async Task<bool> OnClosingAsync()
    {
        // Check for project unsaved changes (settings, metadata, etc.)
        if (_project is { HasUnsavedChanges: true })
        {
            var projectName = _project.IsAdHoc
                ? "untitled"
                : Path.GetFileName(_project.FilePath);

            var choice = await _messageBoxService.ShowSavePromptAsync(
                "Unsaved Project Changes",
                $"Project '{projectName}' has unsaved changes.",
                "Save Project",
                "Exit Without Saving");

            switch (choice)
            {
                case SavePromptResult.Cancel:
                    return false;
                case SavePromptResult.Save:
                    if (!_project.IsAdHoc)
                    {
                        await _project.SaveAsync();
                    }
                    break;
                case SavePromptResult.DontSave:
                    break; // Proceed without saving project
            }
        }

        // Prompt per-disk for each dirty disk
        var dirtyDrives = DiskStatus.Cards
            .SelectMany(card => card.Drives)
            .Where(drive => drive.IsDirty)
            .ToList();

        // Track which drives the user chose to save vs discard
        var drivesToDiscard = new List<DiskStatusWidgetViewModel>();
        bool saveDiskChanges = false;

        foreach (var drive in dirtyDrives)
        {
            var choice = await _messageBoxService.ShowSavePromptAsync(
                "Unsaved Disk Changes",
                $"Disk '{drive.Filename}' ({drive.DiskId}) has unsaved changes.",
                "Save Disk Data",
                "Exit Without Saving");

            switch (choice)
            {
                case SavePromptResult.Cancel:
                    return false; // Cancel exit entirely

                case SavePromptResult.Save:
                    saveDiskChanges = true;
                    break; // Eject normally — ReturnAsync captures data

                case SavePromptResult.DontSave:
                    drivesToDiscard.Add(drive);
                    break; // Will eject with DiscardChanges
            }
        }

        // Persist mount configuration and flush project before exit.
        if (_project != null)
        {
            try
            {
                await SaveMountConfigurationAsync();

                // Eject drives the user chose to discard (clear dirty before ReturnAsync)
                foreach (var drive in drivesToDiscard)
                {
                    await _emulatorCore.SendCardMessageAsync(
                        (SlotNumber)drive.SlotNumber,
                        new EjectDiskMessage(drive.DriveNumber, DiscardChanges: true));
                }

                // Eject all remaining disks (ReturnAsync captures dirty data)
                await _emulatorCore.SendCardMessageAsync(null, new EjectAllDisksMessage());

                // Save the project if the user chose to save any disk data, or if file-based
                if (saveDiskChanges || !_project.IsAdHoc)
                {
                    await _project.SaveAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[OnClosingAsync] Error during project shutdown: {ex.Message}");
            }
        }

        return true; // Allow exit
    }

    #endregion

    // ─── Private helpers ──────────────────────────────────────────────────

    /// <summary>
    /// Strips the <c>_skilletdir</c> suffix from a directory name to get the clean project name.
    /// Matches the derivation logic in <see cref="Pandowdy.Project.Services.SkilletProject"/>.
    /// When the single-file backend replaces the directory store, remove this helper.
    /// </summary>
    private static string StripSkilletDirSuffix(string dirName)
    {
        const string suffix = "_skilletdir";
        return dirName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
            ? dirName[..^suffix.Length]
            : dirName;
    }
}
