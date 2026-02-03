namespace Pandowdy.EmuCore.Interfaces;

public interface ISystemRomProvider : IPandowdyMemory
{
    public void LoadRomFile(string filename);
}
