using Pandowdy.EmuCore.Interfaces;

namespace Pandowdy.EmuCore.Services;

/// <summary>
/// Immutable snapshot of the emulator's execution state at a specific point in time.
/// </summary>
/// <param name="PC">Program Counter (16-bit address of the next instruction to execute)</param>
/// <param name="SP">Stack Pointer (8-bit offset into the 6502 stack page at 0x0100-0x01FF)</param>
/// <param name="Cycles">Total CPU cycles executed since emulator start or reset</param>
/// <param name="LineNumber">
/// Optional line number in disassembly/source code corresponding to the current PC.
/// Null if no source mapping is available.
/// </param>
/// <param name="IsRunning">
/// True if the emulator is actively executing instructions (RunAsync is active).
/// False if stopped or not yet started.
/// </param>
/// <param name="IsPaused">
/// True if the emulator is paused (execution suspended but can be resumed).
/// False if running or stopped.
/// </param>
/// <remarks>
/// This record is immutable and thread-safe. Each state change produces a new instance,
/// which is published through <see cref="IEmulatorState.Stream"/> for reactive subscribers.
/// </remarks>
public record StateSnapshot(ushort PC, byte SP, ulong Cycles, int? LineNumber, bool IsRunning, bool IsPaused);


/// <summary>
/// Reactive provider for emulator execution state, using a BehaviorSubject to publish state changes.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Implementation:</strong> Uses <see cref="System.Reactive.Subjects.BehaviorSubject{T}"/> 
/// to maintain the current state and broadcast updates to all subscribers. The BehaviorSubject
/// guarantees that new subscribers immediately receive the most recent state.
/// </para>
/// <para>
/// <strong>Thread Safety:</strong> BehaviorSubject is thread-safe for publishing and subscribing.
/// Multiple threads can safely call <see cref="Update"/> and subscribe to <see cref="Stream"/>.
/// </para>
/// <para>
/// <strong>Initial State:</strong> Initialized with a default snapshot (PC=0, SP=0, Cycles=0, not running).
/// </para>
/// <para>
/// <strong>Control Methods:</strong> RequestPause(), RequestContinue(), and RequestStep() are currently
/// placeholders. These will be implemented when the execution control loop is integrated with this provider.
/// </para>
/// </remarks>
public sealed class EmulatorStateProvider : IEmulatorState
{
    private readonly System.Reactive.Subjects.BehaviorSubject<StateSnapshot> _subject = new(new StateSnapshot(0, 0, 0, null, false, false));
    
    /// <inheritdoc />
    /// <remarks>
    /// Uses a <see cref="System.Reactive.Subjects.BehaviorSubject{T}"/> which guarantees that
    /// new subscribers immediately receive the most recent state snapshot, then receive all
    /// subsequent updates.
    /// </remarks>
    public IObservable<StateSnapshot> Stream => _subject;
    
    /// <inheritdoc />
    /// <remarks>
    /// Returns the current value of the BehaviorSubject (most recently published snapshot).
    /// This is a synchronous operation that does not involve subscribing to the stream.
    /// </remarks>
    public StateSnapshot GetCurrent() => _subject.Value;
    
    /// <inheritdoc />
    /// <remarks>
    /// Publishes the new snapshot to all active subscribers via <see cref="Stream"/>.
    /// This method is typically called by the emulator's main execution loop after each
    /// instruction or at regular intervals.
    /// </remarks>
    public void Update(StateSnapshot snapshot) => _subject.OnNext(snapshot);
    
    /// <inheritdoc />
    /// <remarks>
    /// <strong>TODO:</strong> This is a placeholder. Implementation will signal the emulator's
    /// execution controller (likely via CancellationToken or state flag) to pause execution
    /// at the next safe point (typically after completing the current instruction).
    /// </remarks>
    public void RequestPause() { /* placeholder */ }
    
    /// <inheritdoc />
    /// <remarks>
    /// <strong>TODO:</strong> This is a placeholder. Implementation will signal the emulator's
    /// execution controller to resume execution from the paused state.
    /// </remarks>
    public void RequestContinue() { /* placeholder */ }
    
    /// <inheritdoc />
    /// <remarks>
    /// <strong>TODO:</strong> This is a placeholder. Implementation will signal the emulator to
    /// execute exactly one instruction, then pause again. Used for debugging and single-stepping
    /// through code.
    /// </remarks>
    public void RequestStep() { /* placeholder */ }
}
