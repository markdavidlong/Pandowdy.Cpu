using Emulator;

namespace Pandowdy.EmuCore.Interfaces;

public interface ISystemRamSelector : IMemory, IDirectMemoryPoolReader
{
    public void CopyMainMemoryIntoSpan(Span<byte> destination);
    public bool CopyAuxMemoryIntoSpan(Span<byte> destination);
}
