// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.EmuCore.Machine;

/// <summary>
/// Represents a component that can be restored to its initial power-on state during a cold boot.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Purpose:</strong> Implemented by components that need to participate in the cold boot
/// sequence triggered by <see cref="VA2M.DoRestart"/>. Each registered component is called in
/// priority order by <see cref="RestartCollection.RestartAll"/>.
/// </para>
/// <para>
/// <strong>Discovery:</strong> Components are tagged with
/// <c>[Capability(typeof(IRestartable))]</c> to be automatically discovered via DI and
/// registered in <see cref="RestartCollection"/>. Factory-generated objects
/// (e.g., cards installed into slots) are registered dynamically via
/// <see cref="RestartCollection.Register"/>.
/// </para>
/// <para>
/// <strong>Priority:</strong> The optional <see cref="CapabilityAttribute.Priority"/> value
/// controls execution order within the cold boot sequence. Higher-priority components restart
/// first. <see cref="VA2MBus"/> uses priority 100 to ensure the CPU is reset last.
/// </para>
/// <para>
/// <strong>Thread Context:</strong> <see cref="Restart"/> is always called on the emulator
/// thread, inside an enqueued action at an instruction boundary.
/// </para>
/// </remarks>
public interface IRestartable
{
    /// <summary>
    /// Restores this component to its initial power-on state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Implementations should reset all mutable state to match what would occur when the
    /// Apple IIe is powered on from cold — for example, clearing RAM to zero, resetting
    /// soft switches to defaults, stopping drive motors, and reinitializing registers.
    /// </para>
    /// <para>
    /// This method must be idempotent: calling it multiple times in succession should
    /// produce the same result as calling it once.
    /// </para>
    /// </remarks>
    void Restart();
}
