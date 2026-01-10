using Emulator;

namespace Pandowdy.EmuCore.Interfaces;



public interface ISystemIoHandler : IMemory
{

    public void Reset(); // Resets any subsystems
}
