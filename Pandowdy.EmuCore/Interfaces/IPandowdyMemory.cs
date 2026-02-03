
namespace Pandowdy.EmuCore.Interfaces;
public interface IPandowdyMemory
{
    int Size { get; }
    byte Read(ushort address);
    void Write(ushort address, byte data);

    byte this[ushort address] { get; set; }
}

