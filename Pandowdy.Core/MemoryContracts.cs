using System;

namespace Pandowdy.Core;

/// <summary>
/// Event arguments for mapped memory notifications used by non-UI consumers.
/// </summary>
public sealed class MemoryAccessEventArgs : EventArgs
{
    public ushort Address { get; init; }
    public byte? Value { get; init; }
    public int Length { get; init; }
}

/// <summary>
/// Abstraction for a memory source that raises notifications when it is modified.
/// </summary>
public interface IMappedMemory
{
    event EventHandler<MemoryAccessEventArgs> MemoryWritten;
    event EventHandler<MemoryAccessEventArgs> MemoryBlockWritten;

    byte Read(ushort address);
}
