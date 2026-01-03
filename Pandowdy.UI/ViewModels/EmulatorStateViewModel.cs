using System;
using ReactiveUI;
using Pandowdy.EmuCore.Interfaces;
using System.Reactive.Linq;

namespace Pandowdy.UI.ViewModels;

/// <summary>
/// View model for displaying real-time Apple IIe emulator state (PC, cycles, BASIC line number).
/// </summary>
/// <remarks>
/// <para>
/// <strong>Purpose:</strong> Provides reactive properties bound to UI elements that display
/// the current execution state of the emulator. Updates are automatically pushed from the
/// emulator core via the IEmulatorState stream.
/// </para>
/// <para>
/// <strong>Reactive Pattern:</strong> Implements ReactiveUI's IActivatableViewModel pattern
/// for lifecycle-aware subscriptions. The view model only subscribes to the state stream
/// when activated (view is visible), preventing unnecessary updates and resource usage.
/// </para>
/// <para>
/// <strong>Update Frequency:</strong> Samples the emulator state stream at 50ms intervals
/// (20 Hz) to balance UI responsiveness with performance. This prevents excessive UI updates
/// while still providing smooth feedback during emulation.
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
/// &lt;TextBlock Text="{Binding LineNumber, StringFormat='Line: {0}'}" /&gt;
/// </code>
/// </para>
/// </remarks>
public sealed class EmulatorStateViewModel : ReactiveObject, IActivatableViewModel
{
    /// <summary>
    /// Emulator state provider that supplies the reactive stream of state updates.
    /// </summary>
    private readonly IEmulatorState _state;
    
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
    /// Updates approximately 20 times per second (50ms sample interval) when the view model is active.
    /// </para>
    /// </remarks>
    public ushort PC
    {
        get => _pc;
        private set => this.RaiseAndSetIfChanged(ref _pc, value);
    }

    /// <summary>
    /// Backing field for the LineNumber property.
    /// </summary>
    private int? _lineNumber;
    
    /// <summary>
    /// Gets the current Applesoft BASIC line number being executed, or null if not in BASIC.
    /// </summary>
    /// <value>
    /// Line number (0-63999) if executing BASIC code, null if in Monitor or machine code.
    /// </value>
    /// <remarks>
    /// <para>
    /// <strong>Detection Method:</strong> The emulator reads zero page locations $75-$76
    /// which contain the BASIC line pointer. If the value is less than $FA00, it's interpreted
    /// as a valid BASIC line number.
    /// </para>
    /// <para>
    /// <strong>Use Cases:</strong>
    /// <list type="bullet">
    /// <item>Display current line during BASIC program execution</item>
    /// <item>Help users debug BASIC programs by showing execution progress</item>
    /// <item>Null when in Monitor, running machine code, or in immediate mode</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Binding Example:</strong>
    /// <code>
    /// &lt;TextBlock Text="{Binding LineNumber, StringFormat='Line: {0}', 
    ///                          TargetNullValue='(not in BASIC)'}" /&gt;
    /// </code>
    /// </para>
    /// </remarks>
    public int? LineNumber
    {
        get => _lineNumber;
        private set => this.RaiseAndSetIfChanged(ref _lineNumber, value);
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
    /// <param name="state">Emulator state provider supplying reactive state updates.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="state"/> is null.</exception>
    /// <remarks>
    /// <para>
    /// <strong>Initialization:</strong> The constructor sets up a reactive subscription using
    /// ReactiveUI's WhenActivated pattern. The actual subscription to the state stream is
    /// deferred until the view model is activated (typically when the view becomes visible).
    /// </para>
    /// <para>
    /// <strong>Subscription Lifecycle:</strong>
    /// <list type="number">
    /// <item><strong>Activation:</strong> When view becomes visible, WhenActivated callback fires</item>
    /// <item><strong>Subscription:</strong> Subscribe to state stream with 50ms sampling</item>
    /// <item><strong>Update Properties:</strong> PC, LineNumber, Cycles updated on UI thread</item>
    /// <item><strong>Deactivation:</strong> When view is hidden/disposed, subscription is automatically disposed</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Sampling Strategy:</strong> Uses Observable.Sample(50ms) to throttle updates
    /// to 20 Hz. This prevents excessive UI updates (emulator can generate updates at much
    /// higher frequency) while maintaining smooth visual feedback.
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
    public EmulatorStateViewModel(IEmulatorState state)
    {
        _state = state;
        this.WhenActivated(disposables =>
        {
            var sub = _state.Stream.Sample(TimeSpan.FromMilliseconds(50))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(s => { PC = s.PC; LineNumber = s.LineNumber; Cycles = s.Cycles; });
            disposables.Add(sub);
        });
    }
}
