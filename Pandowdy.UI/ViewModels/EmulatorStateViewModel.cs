// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System;
using ReactiveUI;
using Pandowdy.EmuCore.Machine;
using System.Reactive.Linq;
using Pandowdy.UI.Interfaces;

namespace Pandowdy.UI.ViewModels;

/// <summary>
/// View model for displaying real-time Apple IIe emulator state (PC, cycles).
/// </summary>
/// <remarks>
/// <para>
/// <strong>Purpose:</strong> Provides reactive properties bound to UI elements that display
/// the current execution state of the emulator. Uses a pull-based architecture where the
/// view model polls the emulator via IRefreshTicker.
/// </para>
/// <para>
/// <strong>Architecture:</strong> Implements ReactiveUI's IActivatableViewModel pattern
/// for lifecycle-aware subscriptions. The view model subscribes to the refresh ticker
/// when activated (view is visible) and polls emulator state on-demand.
/// </para>
/// <para>
/// <strong>Update Frequency:</strong> Samples the refresh ticker at the high-frequency interval
/// defined in <see cref="Constants.RefreshRates.Polling.HighFrequencyMs"/> to match the
/// display refresh rate. This provides smooth visual feedback synchronized with the display
/// without unnecessary overhead.
/// </para>
/// <para>
/// <strong>Thread Safety:</strong> All property updates are marshaled to the UI thread via
/// RxApp.MainThreadScheduler, ensuring safe binding to UI elements.
/// </para>
/// <para>
/// <strong>Typical Usage:</strong>
/// <code>
/// &lt;TextBlock Text="{Binding PC, StringFormat='PC: ${0:X4}'}" /&gt;
/// &lt;TextBlock Text="{Binding Cycles, StringFormat='Cycles: {0:N0}'}" /&gt;
/// </code>
/// </para>
/// </remarks>
public sealed class EmulatorStateViewModel : ReactiveObject, IActivatableViewModel
{
    /// <summary>
    /// Emulator core interface for polling CPU state.
    /// </summary>
    private readonly IEmulatorCoreInterface _emulator;

    /// <summary>
    /// 60 Hz refresh ticker for determining when to poll emulator state.
    /// </summary>
    private readonly IRefreshTicker _refreshTicker;

    /// <summary>
    /// Gets the view model activator for managing subscription lifecycle.
    /// </summary>
    /// <remarks>
    /// Used by ReactiveUI's WhenActivated mechanism to automatically subscribe when the
    /// view model is active (view visible) and unsubscribe when inactive (view hidden/disposed).
    /// This prevents memory leaks and unnecessary processing.
    /// </remarks>
    public ViewModelActivator Activator { get; } = new();

    /// <summary>
    /// Backing field for the PC property.
    /// </summary>
    private ushort _pc;

    /// <summary>
    /// Gets the current 6502 Program Counter (PC) register value.
    /// </summary>
    /// <value>16-bit address ($0000-$FFFF) indicating the next instruction to execute.</value>
    /// <remarks>
    /// <para>
    /// The PC points to the memory address of the next instruction the CPU will execute.
    /// In the Apple IIe, common PC values include:
    /// <list type="bullet">
    /// <item>$F666: Monitor prompt</item>
    /// <item>$E000-$FFFF: ROM (Monitor, BASIC interpreter)</item>
    /// <item>$0800-$BFFF: User program space</item>
    /// </list>
    /// </para>
    /// <para>
    /// Updates at the high-frequency polling rate (see <see cref="Constants.RefreshRates.Polling.HighFrequencyMs"/>)
    /// when the view model is active, synchronized with the display refresh rate.
    /// </para>
    /// </remarks>
    public ushort PC
    {
        get => _pc;
        private set => this.RaiseAndSetIfChanged(ref _pc, value);
    }

    /// <summary>
    /// Backing field for the Cycles property.
    /// </summary>
    private ulong _cycles;

    /// <summary>
    /// Gets the total number of CPU cycles executed since the last reset.
    /// </summary>
    /// <value>Cycle count (0 to ulong.MaxValue), increments at 1.023 MHz rate.</value>
    /// <remarks>
    /// <para>
    /// <strong>Cycle Timing:</strong> The Apple IIe runs at approximately 1,023,000 Hz
    /// (1.023 MHz). This property shows the total number of cycles executed, which can be
    /// used to:
    /// <list type="bullet">
    /// <item>Calculate elapsed time: cycles / 1,023,000 = seconds</item>
    /// <item>Measure performance of BASIC or machine code programs</item>
    /// <item>Verify emulator timing accuracy</item>
    /// <item>Display uptime or execution duration</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Example Values:</strong>
    /// <list type="bullet">
    /// <item>1,023,000 cycles ≈ 1 second of emulation</item>
    /// <item>17,063 cycles ≈ 1 video frame (~60 Hz)</item>
    /// <item>61,380,000 cycles ≈ 1 minute of emulation</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Reset Behavior:</strong> Reset to 0 on full system reset (power cycle).
    /// Continues counting during warm reset (Ctrl+Reset).
    /// </para>
    /// </remarks>
    public ulong Cycles
    {
        get => _cycles;
        private set => this.RaiseAndSetIfChanged(ref _cycles, value);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EmulatorStateViewModel"/> class.
    /// </summary>
    /// <param name="emulator">Emulator core interface for polling CPU state.</param>
    /// <param name="refreshTicker">Refresh ticker for polling timing (see <see cref="Constants.RefreshRates.BaseTickerHz"/>).</param>
    /// <exception cref="ArgumentNullException">Thrown if any parameter is null.</exception>
    /// <remarks>
    /// <para>
    /// <strong>Initialization:</strong> The constructor sets up a reactive subscription using
    /// ReactiveUI's WhenActivated pattern. The actual subscription to the refresh ticker is
    /// deferred until the view model is activated (typically when the view becomes visible).
    /// </para>
    /// <para>
    /// <strong>Subscription Lifecycle:</strong>
    /// <list type="number">
    /// <item><strong>Activation:</strong> When view becomes visible, WhenActivated callback fires</item>
    /// <item><strong>Subscription:</strong> Subscribe to refresh ticker with high-frequency sampling
    /// (see <see cref="Constants.RefreshRates.Polling.HighFrequencyMs"/>)</item>
    /// <item><strong>Poll State:</strong> On each tick, read PC and Cycles from emulator</item>
    /// <item><strong>Update Properties:</strong> PC, Cycles updated on UI thread</item>
    /// <item><strong>Deactivation:</strong> When view is hidden/disposed, subscription is automatically disposed</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Pull Architecture:</strong> Uses Observable.Sample() to match the display refresh rate.
    /// This prevents polling faster than the display can update while maintaining smooth visual feedback.
    /// The emulator is polled on-demand, not pushed via reactive streams. The polling interval is
    /// configured via <see cref="Constants.RefreshRates.Polling.HighFrequencyMs"/>.
    /// </para>
    /// <para>
    /// <strong>Thread Marshaling:</strong> ObserveOn(RxApp.MainThreadScheduler) ensures all
    /// property updates occur on the UI thread, which is required for binding to UI elements.
    /// </para>
    /// <para>
    /// <strong>Memory Management:</strong> The WhenActivated pattern automatically handles
    /// subscription disposal, preventing memory leaks when the view model is no longer active.
    /// </para>
    /// </remarks>
    public EmulatorStateViewModel(IEmulatorCoreInterface emulator, IRefreshTicker refreshTicker)
    {
        _emulator = emulator ?? throw new ArgumentNullException(nameof(emulator));
        _refreshTicker = refreshTicker ?? throw new ArgumentNullException(nameof(refreshTicker));

        this.WhenActivated(disposables =>
        {
            // Poll emulator state at high frequency (matches display refresh rate)
            var sub = _refreshTicker.Stream
                .Sample(TimeSpan.FromMilliseconds(Constants.RefreshRates.Polling.HighFrequencyMs))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ => UpdateFromEmulator());
            disposables.Add(sub);
        });
    }

    /// <summary>
    /// Polls the emulator and updates all properties from the current state.
    /// </summary>
    private void UpdateFromEmulator()
    {
        var cpu = _emulator.CpuState;
        PC = cpu.PC;
        Cycles = _emulator.TotalCycles;
    }
}
