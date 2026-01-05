namespace Pandowdy.EmuCore.Interfaces;

/// <summary>
/// Provides memory access notifications for debugging and monitoring purposes.
/// </summary>
/// <remarks>
/// This interface is designed for components that need to monitor memory access,
/// such as debuggers, memory viewers, and watchpoint systems. It provides events
/// that fire when memory is read from or written to, allowing external tools to
/// track program behavior, detect specific memory accesses, and implement breakpoint
/// and watchpoint functionality.
/// <para>
/// The interface provides two notification events:
/// <list type="bullet">
/// <item><see cref="MemoryRead"/>: Fired when a byte is read from memory</item>
/// <item><see cref="MemoryWritten"/>: Fired when a byte is written to memory</item>
/// </list>
/// Both events fire for individual byte operations, allowing precise tracking of
/// memory access patterns during program execution.
/// </para>
/// </remarks>
public interface IMemoryAccessNotifier
{
    /// <summary>
    /// Raised when a byte is written to memory.
    /// </summary>
    /// <remarks>
    /// This event fires whenever a byte of memory is modified through CPU
    /// operations or direct memory writes. The <see cref="MemoryAccessEventArgs"/>
    /// contains the address written to and the new value at that address.
    /// <para>
    /// Typical uses include:
    /// <list type="bullet">
    /// <item>Implementing memory write watchpoints in a debugger</item>
    /// <item>Tracking changes to specific memory locations</item>
    /// <item>Logging memory write patterns for analysis</item>
    /// <item>Triggering breakpoints when specific values are written to specific addresses</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Performance Note:</strong> This event can fire very frequently during
    /// program execution (potentially millions of times per second). Subscribers should
    /// minimize processing time to avoid impacting emulation performance.
    /// </para>
    /// </remarks>
    event EventHandler<MemoryAccessEventArgs> MemoryWritten;

    /// <summary>
    /// Raised when a byte is read from memory.
    /// </summary>
    /// <remarks>
    /// This event fires whenever a byte of memory is accessed through CPU
    /// operations or direct memory reads. The <see cref="MemoryAccessEventArgs"/>
    /// contains the address read from and the value at that address.
    /// <para>
    /// Typical uses include:
    /// <list type="bullet">
    /// <item>Implementing memory read watchpoints in a debugger</item>
    /// <item>Tracking which memory locations a program accesses</item>
    /// <item>Detecting access to memory-mapped I/O registers</item>
    /// <item>Profiling memory access patterns for optimization analysis</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Performance Note:</strong> This event fires even more frequently than
    /// <see cref="MemoryWritten"/> since programs typically read memory more often
    /// than they write it. Subscribers should be extremely efficient to avoid
    /// significantly impacting emulation performance.
    /// </para>
    /// </remarks>
    event EventHandler<MemoryAccessEventArgs> MemoryRead;
}
