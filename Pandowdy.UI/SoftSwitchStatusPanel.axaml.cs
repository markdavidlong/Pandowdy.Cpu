// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Pandowdy.EmuCore.IO;
using Pandowdy.UI.ViewModels;

namespace Pandowdy.UI;

/// <summary>
/// User control that displays Apple IIe soft switch status information in real-time.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Purpose:</strong> Provides a visual panel showing the current state of all Apple IIe
/// soft switches, including memory mapping configuration, video modes, pushbuttons, and annunciators.
/// This is invaluable for debugging, education, and understanding what the emulated Apple IIe is doing.
/// </para>
/// <para>
/// <strong>Display Categories:</strong>
/// <list type="bullet">
/// <item><strong>Memory Mapping:</strong> 80STORE, RAMRD, RAMWRT, INTCXROM, ALTZP, SLOTC3ROM</item>
/// <item><strong>Video Modes:</strong> PAGE2, HIRES, MIXED, TEXT, 80COL, ALTCHAR, FLASHON</item>
/// <item><strong>Pushbuttons:</strong> PB0, PB1, PB2 (game controller buttons)</item>
/// <item><strong>Annunciators:</strong> AN0, AN1, AN2, AN3/DGR</item>
/// <item><strong>Language Card:</strong> BANK1, PREWRITE, HIGHREAD, HIGHWRITE</item>
/// </list>
/// </para>
/// <para>
/// <strong>Two-Phase Initialization:</strong> Similar to MainWindow, this control uses a two-phase
/// initialization pattern:
/// <list type="number">
/// <item><strong>Constructor:</strong> Parameterless (Avalonia requirement), loads XAML</item>
/// <item><strong>Initialize():</strong> Called by parent window, sets DataContext to view model</item>
/// </list>
/// </para>
/// <para>
/// <strong>Data Binding:</strong> All status displays are bound to properties in
/// <see cref="SystemStatusViewModel"/>, which receives updates from the emulator core via
/// <see cref="Pandowdy.EmuCore.IO.ISystemStatusProvider"/>.
/// </para>
/// <para>
/// <strong>Update Frequency:</strong> Updates occur whenever soft switch states change in the
/// emulator. Unlike EmulatorStateViewModel (which samples at 50ms), these updates are event-driven
/// and happen immediately when switches change.
/// </para>
/// <para>
/// <strong>Visibility Control:</strong> The panel can be shown/hidden via MainWindow.ShowSoftSwitchStatus
/// property, allowing users to toggle it on/off (Alt+W shortcut).
/// </para>
/// </remarks>
public partial class SoftSwitchStatusPanel : UserControl
{
    /// <summary>
    /// Guard flag ensuring Initialize() is called exactly once.
    /// </summary>
    private bool _isInitialized;

    /// <summary>
    /// Initializes a new instance of the <see cref="SoftSwitchStatusPanel"/> class.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>XAML Requirement:</strong> This parameterless constructor is required by Avalonia's
    /// XAML loader. The DataContext is not set here; it must be set via <see cref="Initialize"/>
    /// </para>
    /// <para>
    /// <strong>Initialization:</strong> Only loads the XAML markup. The actual view model binding
    /// is deferred to the Initialize() method which is called by the parent MainWindow.
    /// </para>
    /// <para>
    /// <strong>Call Sequence:</strong> This constructor is called first (by XAML loader or parent),
    /// immediately followed by Initialize() (called by MainWindow.Initialize).
    /// </para>
    /// </remarks>
    public SoftSwitchStatusPanel()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Initializes the panel with its view model and establishes data binding.
    /// Must be called by parent window after construction.
    /// </summary>
    /// <param name="viewModel">
    /// The SystemStatusViewModel containing reactive properties for all 24 soft switch states.
    /// Must not be null.
    /// </param>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown if Initialize is called more than once.
    /// </exception>
    /// <remarks>
    /// <para>
    /// <strong>Two-Phase Pattern:</strong> This method completes the initialization started by
    /// the parameterless constructor, setting the DataContext to enable XAML data binding.
    /// </para>
    /// <para>
    /// <strong>Data Binding Setup:</strong> Once DataContext is set to the SystemStatusViewModel,
    /// all XAML bindings become active and the panel starts displaying real-time soft switch states.
    /// </para>
    /// <para>
    /// <strong>Single Initialization:</strong> The _isInitialized guard prevents multiple
    /// Initialize() calls, which would be an error since DataContext should only be set once.
    /// </para>
    /// <para>
    /// <strong>Called By:</strong> MainWindow.Initialize() during the window's two-phase
    /// initialization process. This happens automatically after window construction.
    /// </para>
    /// <para>
    /// <strong>View Model Properties:</strong> The SystemStatusViewModel contains 24 reactive
    /// boolean properties representing all Apple IIe soft switches. When any switch changes in
    /// the emulator, the view model property updates, triggering UI refresh via data binding.
    /// </para>
    /// </remarks>
    public void Initialize(SystemStatusViewModel viewModel)
    {
        if (_isInitialized)
        {
            throw new System.InvalidOperationException("Panel already initialized.");
        }
        
        _isInitialized = true;
        DataContext = viewModel;
    }

    /// <summary>
    /// Loads the XAML markup for this control.
    /// </summary>
    /// <remarks>
    /// Called by the constructor to load the SoftSwitchStatusPanel.axaml file and create
    /// the visual tree containing all soft switch indicators.
    /// </remarks>
    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
