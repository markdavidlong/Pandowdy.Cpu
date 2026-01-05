using System.Runtime.CompilerServices;
using Emulator;
using Pandowdy.EmuCore.Interfaces;


namespace Pandowdy.EmuCore
{

    public class SystemRamSelector(
        ISystemRam mainRam,
        ISystemRam? auxRam,
        IFloatingBusProvider floatingBus,
        ISystemStatusProvider status) : ISystemRamSelector
    {
        private const int RequiredRamSize = 0xC000; // 48KB

 
        private readonly ISystemRam _mainRam = Utility.ValidateIMemorySize(mainRam, nameof(mainRam), RequiredRamSize);
        private readonly ISystemRam? _auxRam = auxRam != null ? Utility.ValidateIMemorySize(auxRam, nameof(auxRam), RequiredRamSize) : null;
        private readonly IFloatingBusProvider _floatingBus = floatingBus ?? throw new ArgumentNullException(nameof(floatingBus));
        private readonly ISystemStatusProvider _status = status ?? throw new ArgumentNullException(nameof(status));

 
        public int Size => RequiredRamSize; // 48kb addressable space


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ReadRawMain(int address)
        {
            return _mainRam[(ushort)(address & 0xffff)];
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ReadRawAux(int address)
        {
            return _auxRam == null ? _floatingBus.Read() : _auxRam[(ushort) (address & 0xffff)];
        }

               
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte Read(ushort address)
        {
            bool readAux;

            if (address < 0x200)
            {
                readAux = _status.StateAltZp;
            }
            else if (address < 0x400 || (address >= 0x800 && address < 0x2000) || address >= 0x4000)
            {
                readAux = _status.StateRamRd;
               
            }
            else if (!_status.State80Store)
            {
                readAux = _status.StateRamRd;
            }
            else
            {
                if (address < 0x800) // 0x400-7ff
                {
                    readAux = _status.StatePage2;
                }
                else // 0x2000-3fff)
                {
                    if (_status.StateHiRes)
                    {
                        readAux = _status.StatePage2;
                    }
                    else
                    {
                        readAux = _status.StateRamRd;
                    }
                }
            }

            IMemory? targetMemory = readAux ? _auxRam : _mainRam;
            return targetMemory != null ? targetMemory[address] : _floatingBus.Read();
        }

        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(ushort address, byte data)
        {
            bool writeAux;

            if (address < 0x200)
            {
                writeAux = _status.StateAltZp;
            }
            else if (address < 0x400 || (address >= 0x800 && address < 0x2000) || address >= 0x4000)
            {
                writeAux = _status.StateRamRd;

            }
            else if (!_status.State80Store)
            {
                writeAux = _status.StateRamRd;
            }
            else
            {
                if (address < 0x800) // 0x400-7ff
                {
                    writeAux = _status.StatePage2;
                }
                else // 0x2000-3fff)
                {
                    if (_status.StateHiRes)
                    {
                        writeAux = _status.StatePage2;
                    }
                    else
                    {
                        writeAux = _status.StateRamRd;
                    }
                }
            }

            IMemory? targetMemory = writeAux ? _auxRam : _mainRam;
            if (targetMemory != null)
            {
                targetMemory[address] = data;
            }
            else
            {
                // No Op. Possibly FloatingBus.Write() later on?
            }
        }

  
        public byte this[ushort address]
        {
            get => Read(address);
            set => Write(address, value);
        }
    }
}

