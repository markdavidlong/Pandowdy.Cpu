// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.EmuCore.Machine;

/// <summary>
/// Provides access to the emulator's execution state (PC, SP, cycle count, etc.).
/// Supports reactive subscriptions and control operations (pause, continue, step).
/// </summary>
/// <remarks>
/// <para>
/// <strong>Observable Pattern:</strong> The <see cref="Stream"/> property provides a reactive
/// observable that publishes <see cref="StateSnapshot"/> instances whenever the emulator state changes.
/// Subscribers receive real-time updates about CPU registers, cycle count, and execution status.
/// </para>
/// <para>
/// <strong>State Contents:</strong> Each <see cref="StateSnapshot"/> contains:
/// <list type="bullet">
/// <item><strong>PC:</strong> Program Counter (16-bit address of next instruction)</item>
/// <item><strong>SP:</strong> Stack Pointer (8-bit offset into stack page $0100-$01FF)</item>
/// <item><strong>Cycles:</strong> Total CPU cycles executed since reset</item>
/// <item><strong>LineNumber:</strong> Optional BASIC line number (if applicable)</item>
/// <item><strong>IsRunning:</strong> Whether emulator is actively executing</item>
/// <item><strong>IsPaused:</strong> Whether execution is suspended</item>
/// </list>
/// </para>
/// <para>
/// <strong>Thread Safety:</strong> Implementations use thread-safe reactive subjects
/// (BehaviorSubject) for publishing. <see cref="Update"/> can be called from the emulator
/// thread while UI threads subscribe to <see cref="Stream"/>.
/// </para>
/// </remarks>
public interface IEmulatorState
{
    /// <summary>
    /// Observable stream of emulator state snapshots.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Uses a BehaviorSubject internally, which guarantees that new subscribers immediately
    /// receive the most recent state snapshot, then receive all subsequent updates.
    /// </para>
    /// <para>
    /// <strong>Subscription Example:</strong>
    /// <code>
    /// emulatorState.Stream.Subscribe(snapshot =>
    /// {
    ///     Console.WriteLine($"PC: ${snapshot.PC:X4}, Cycles: {snapshot.Cycles}");
    /// });
    /// </code>
    /// </para>
    /// </remarks>
    IObservable<StateSnapshot> Stream { get; }

    /// <summary>
    /// Gets the current emulator state snapshot synchronously.
    /// </summary>
    /// <returns>The most recent <see cref="StateSnapshot"/> without subscribing to the stream.</returns>
    /// <remarks>
    /// Useful for one-time queries of current state without setting up a subscription.
    /// For continuous monitoring, prefer subscribing to <see cref="Stream"/>.
    /// </remarks>
    StateSnapshot GetCurrent();

    /// <summary>
    /// Updates the emulator state with a new snapshot.
    /// </summary>
    /// <param name="snapshot">The new state snapshot to publish.</param>
    /// <remarks>
    /// <para>
    /// Called by <see cref="VA2M"/> after each batch of cycles or significant state change.
    /// The snapshot is immediately published to all <see cref="Stream"/> subscribers.
    /// </para>
    /// <para>
    /// <strong>Thread Safety:</strong> This method is typically called from the emulator
    /// thread. The underlying BehaviorSubject handles thread-safe publishing to subscribers.
    /// </para>
    /// </remarks>
    void Update(StateSnapshot snapshot);

    /// <summary>
    /// Requests the emulator to pause execution.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Placeholder:</strong> This method is currently a placeholder for future
    /// debugger integration. When implemented, it will signal the emulator's execution
    /// controller to pause at the next safe point (after completing the current instruction).
    /// </para>
    /// </remarks>
    void RequestPause();

    /// <summary>
    /// Requests the emulator to continue execution from a paused state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Placeholder:</strong> This method is currently a placeholder for future
    /// debugger integration. When implemented, it will signal the emulator to resume
    /// normal execution from where it was paused.
    /// </para>
    /// </remarks>
    void RequestContinue();

    /// <summary>
    /// Requests the emulator to execute a single instruction step.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Placeholder:</strong> This method is currently a placeholder for future
    /// debugger integration. When implemented, it will execute exactly one 6502 instruction
    /// and then pause again, enabling single-step debugging.
    /// </para>
    /// </remarks>
    void RequestStep();
}
