namespace Pandowdy.EmuCore.Interfaces
{
    /// <summary>
    /// Abstraction for a memory source that raises notifications when it is modified.
    /// </summary>
    public interface IMemoryAccessNotifier
    {
        event EventHandler<MemoryAccessEventArgs> MemoryWritten;
        event EventHandler<MemoryAccessEventArgs> MemoryBlockWritten;

        byte Read(ushort address);
    }

}
