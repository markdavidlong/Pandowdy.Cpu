using Emulator;

namespace Pandowdy.EmuCore.Interfaces;

public interface ISystemRomProvider : IMemory
{
    public void LoadRomFile(string filename);
}
