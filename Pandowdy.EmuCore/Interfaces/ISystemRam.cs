using Emulator;

namespace Pandowdy.EmuCore.Interfaces;

public interface ISystemRam : IMemory
{
    public void CopyIntoSpan(Span<byte> destination);
}
