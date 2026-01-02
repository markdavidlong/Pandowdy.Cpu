using Pandowdy.EmuCore.Services;

namespace Pandowdy.EmuCore.Interfaces;

/// <summary>
/// Provides access to the emulator's execution state (PC, SP, cycle count, etc.).
/// Supports reactive subscriptions and control operations (pause, continue, step).
/// </summary>
public interface IEmulatorState
{
    /// <summary>
    /// Observable stream of emulator state snapshots.
    /// </summary>
    IObservable<StateSnapshot> Stream { get; }
    
    /// <summary>
    /// Gets the current emulator state snapshot.
    /// </summary>
    StateSnapshot GetCurrent();
    
    /// <summary>
    /// Updates the emulator state with a new snapshot.
    /// </summary>
    void Update(StateSnapshot snapshot);
    
    /// <summary>
    /// Requests the emulator to pause execution.
    /// </summary>
    void RequestPause();
    
    /// <summary>
    /// Requests the emulator to continue execution.
    /// </summary>
    void RequestContinue();
    
    /// <summary>
    /// Requests the emulator to execute a single instruction step.
    /// </summary>
    void RequestStep();
}
