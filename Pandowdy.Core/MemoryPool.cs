using System;   
using Emulator;

namespace Pandowdy.Core
{
    public sealed class MemoryPool : IMemory, IMappedMemory
    {
        //Methods from IMemory:
        public System.Int32 Size { get => 0x10000;  } // 64k addressable space

        public byte Read(ushort address) => ReadMapped(address);
        public void Write (ushort address, byte value) => WriteMapped(address, value);

        public byte[] ReadBlock(ushort address, int length)
        {
            byte[] buffer = new byte[length];
            for (int i = 0; i < length; i++)
            {
                buffer[i] = ReadMapped((ushort)(address + i));
            }
            return buffer;
        }

        public void WriteBlock(ushort offset, params byte[] data)
        {
            for (int i = 0; i < data.Length; i++)
            {
                WriteMapped((ushort)(offset + i), data[i]);
            }
            MemoryBlockWritten?.Invoke(this, new MemoryAccessEventArgs { Address = offset, Value = null, Length = data.Length });
        }

        public byte[] DataArray() { throw new Exception("Not implemented"); }

        public byte this[ushort address]
        {
            get => ReadMapped(address);
            set => WriteMapped(address, value);
        }

        //Methods from IMappedMemory:


        public event EventHandler<MemoryAccessEventArgs>? MemoryWritten;

        public event EventHandler<MemoryAccessEventArgs>? MemoryBlockWritten;

        
        
        // MemoryPool Internal implementation:


        private readonly byte[] _pool; // backing store
        public byte[] Pool => _pool;

        public enum Ranges
        {
            Region_0000_01FF = 0,
            Region_0200_03FF = 0x200,
            Region_0400_07FF = 0x400,
            Region_0800_1FFF = 0x800,
            Region_2000_3FFF = 0x2000,
            Region_4000_5FFF = 0x4000,
            Region_6000_BFFF = 0x6000,
            Region_C000_C0FF = 0xC000,
            Region_C100_C1FF = 0xC100,
            Region_C200_C2FF = 0xC200,
            Region_C300_C3FF = 0xC300,
            Region_C400_C4FF = 0xC400,
            Region_C500_C5FF = 0xC500,
            Region_C600_C6FF = 0xC600,
            Region_C700_C7FF = 0xC700,
            Region_C800_CFFF = 0xC800,
            Region_D000_DFFF = 0xD000,
            Region_E000_FFFF = 0xE000
        }


        private bool _ramRd = false;
        private bool _ramWrt = false;
        private bool _altZp = false;
        private bool _80Store = false;
        private bool _hires = false;
        private bool _page2 = false;

        // Nullable maps allow unmapped / write-protected regions; instance (was static)
        private readonly Dictionary<Ranges, Memory<byte>?> _readRanges = [];

        private readonly Dictionary<Ranges, Memory<byte>?> _writeRanges = [];

        // Region slices (instance readonly; previously static)
        private readonly Memory<byte> _m1;

        private readonly Memory<byte> _m2;
        private readonly Memory<byte> _m3;
        private readonly Memory<byte> _m4;
        private readonly Memory<byte> _m5;
        private readonly Memory<byte> _m6;
        private readonly Memory<byte> _m7;
        private readonly Memory<byte> _m8a;
        private readonly Memory<byte> _m8b;
        private readonly Memory<byte> _m9;

        private readonly Memory<byte> _a1;
        private readonly Memory<byte> _a2;
        private readonly Memory<byte> _a3;
        private readonly Memory<byte> _a4;
        private readonly Memory<byte> _a5;
        private readonly Memory<byte> _a6;
        private readonly Memory<byte> _a7;
        private readonly Memory<byte> _a8a;
        private readonly Memory<byte> _a8b;
        private readonly Memory<byte> _a9;

        private readonly Memory<byte> _io;

        private readonly Memory<byte> _int1;
        private readonly Memory<byte> _int2;
        private readonly Memory<byte> _int3;
        private readonly Memory<byte> _int4;
        private readonly Memory<byte> _int5;
        private readonly Memory<byte> _int6;
        private readonly Memory<byte> _int7;
        private readonly Memory<byte> _intext;
        private readonly Memory<byte> _rom1;
        private readonly Memory<byte> _rom2;

        private readonly Memory<byte> _s1;
        private readonly Memory<byte> _s2;
        private readonly Memory<byte> _s3;
        private readonly Memory<byte> _s4;
        private readonly Memory<byte> _s5;
        private readonly Memory<byte> _s6;
        private readonly Memory<byte> _s7;

        private readonly Memory<byte> _s1ext;
        private readonly Memory<byte> _s2ext;
        private readonly Memory<byte> _s3ext;
        private readonly Memory<byte> _s4ext;
        private readonly Memory<byte> _s5ext;
        private readonly Memory<byte> _s6ext;
        private readonly Memory<byte> _s7ext;

        public MemoryPool(int poolSize = 0x27F00, bool randomInit = true)
        {
            _pool = new byte[poolSize];
            if (randomInit)
            {
                var rnd = new Random();
                rnd.NextBytes(_pool);
            }

            // Create slices (offset, length) exactly as before
            _m1 = _pool.AsMemory(0x0000, 0x0200); // Default
            _m2 = _pool.AsMemory(0x0200, 0x0200); // Default
            _m3 = _pool.AsMemory(0x0400, 0x0400); // Default
            _m4 = _pool.AsMemory(0x0800, 0x1800); // Default
            _m5 = _pool.AsMemory(0x2000, 0x2000); // Default
            _m6 = _pool.AsMemory(0x4000, 0x2000); // Default
            _m7 = _pool.AsMemory(0x6000, 0x6000); // Default
            _m8a = _pool.AsMemory(0xC000, 0x1000);
            _m8b = _pool.AsMemory(0xD000, 0x1000);
            _m9 = _pool.AsMemory(0xE000, 0x2000);

            _a1 = _pool.AsMemory(0x10000, 0x0200);
            _a2 = _pool.AsMemory(0x10200, 0x0200);
            _a3 = _pool.AsMemory(0x10400, 0x0400);
            _a4 = _pool.AsMemory(0x10800, 0x1800);
            _a5 = _pool.AsMemory(0x12000, 0x2000);
            _a6 = _pool.AsMemory(0x14000, 0x2000);
            _a7 = _pool.AsMemory(0x16000, 0x6000);
            _a8a = _pool.AsMemory(0x1C000, 0x1000);
            _a8b = _pool.AsMemory(0x1D000, 0x1000);
            _a9 = _pool.AsMemory(0x1E000, 0x2000);

            _io = _pool.AsMemory(0x20000, 0x0100);

            _int1 = _pool.AsMemory(0x20100, 0x0100);
            _int2 = _pool.AsMemory(0x20200, 0x0100);
            _int3 = _pool.AsMemory(0x20300, 0x0100);
            _int4 = _pool.AsMemory(0x20400, 0x0100);
            _int5 = _pool.AsMemory(0x20500, 0x0100);
            _int6 = _pool.AsMemory(0x20600, 0x0100);
            _int7 = _pool.AsMemory(0x20700, 0x0100);
            _intext = _pool.AsMemory(0x20800, 0x0800); // Default

            _rom1 = _pool.AsMemory(0x21000, 0x1000); // Default
            _rom2 = _pool.AsMemory(0x22000, 0x2000); // Default
            _s1 = _pool.AsMemory(0x24000, 0x0100); // Default
            _s2 = _pool.AsMemory(0x24100, 0x0100); // Default
            _s3 = _pool.AsMemory(0x24200, 0x0100); // Default
            _s4 = _pool.AsMemory(0x24300, 0x0100); // Default
            _s5 = _pool.AsMemory(0x24400, 0x0100); // Default
            _s6 = _pool.AsMemory(0x24500, 0x0100); // Default
            _s7 = _pool.AsMemory(0x24600, 0x0100); // Default

            _s1ext = _pool.AsMemory(0x24700, 0x0800);
            _s2ext = _pool.AsMemory(0x24F00, 0x0800);
            _s3ext = _pool.AsMemory(0x25700, 0x0800);
            _s4ext = _pool.AsMemory(0x25F00, 0x0800);
            _s5ext = _pool.AsMemory(0x26700, 0x0800);
            _s6ext = _pool.AsMemory(0x26F00, 0x0800);
            _s7ext = _pool.AsMemory(0x27700, 0x0800);

            SetDefaultReadRanges();
            SetDefaultWriteRanges();
        }

        private void SetDefaultReadRanges()
        {
            _readRanges[Ranges.Region_0000_01FF] = _m1;
            _readRanges[Ranges.Region_0200_03FF] = _m2;
            _readRanges[Ranges.Region_0400_07FF] = _m3;
            _readRanges[Ranges.Region_0800_1FFF] = _m4;
            _readRanges[Ranges.Region_2000_3FFF] = _m5;
            _readRanges[Ranges.Region_4000_5FFF] = _m6;
            _readRanges[Ranges.Region_6000_BFFF] = _m7;
            _readRanges[Ranges.Region_C000_C0FF] = _io;
            _readRanges[Ranges.Region_C100_C1FF] = _int1;
            _readRanges[Ranges.Region_C200_C2FF] = _int2;
            _readRanges[Ranges.Region_C300_C3FF] = _int3;
            _readRanges[Ranges.Region_C400_C4FF] = _int4;
            _readRanges[Ranges.Region_C500_C5FF] = _int5;
            _readRanges[Ranges.Region_C600_C6FF] = _int6;
            _readRanges[Ranges.Region_C700_C7FF] = _int7;
            _readRanges[Ranges.Region_C800_CFFF] = _intext;
            _readRanges[Ranges.Region_D000_DFFF] = _rom1;
            _readRanges[Ranges.Region_E000_FFFF] = _rom2;
        }

        private void SetDefaultWriteRanges()
        {
            _writeRanges[Ranges.Region_0000_01FF] = _m1;
            _writeRanges[Ranges.Region_0200_03FF] = _m2;
            _writeRanges[Ranges.Region_0400_07FF] = _m3;
            _writeRanges[Ranges.Region_0800_1FFF] = _m4;
            _writeRanges[Ranges.Region_2000_3FFF] = _m5;
            _writeRanges[Ranges.Region_4000_5FFF] = _m6;
            _writeRanges[Ranges.Region_6000_BFFF] = _m7;
            _writeRanges[Ranges.Region_C000_C0FF] = _io;
            _writeRanges[Ranges.Region_C100_C1FF] = null;
            _writeRanges[Ranges.Region_C200_C2FF] = null;
            _writeRanges[Ranges.Region_C300_C3FF] = null;
            _writeRanges[Ranges.Region_C400_C4FF] = null;
            _writeRanges[Ranges.Region_C500_C5FF] = null;
            _writeRanges[Ranges.Region_C600_C6FF] = null;
            _writeRanges[Ranges.Region_C700_C7FF] = null;
            _writeRanges[Ranges.Region_C800_CFFF] = null;
            _writeRanges[Ranges.Region_D000_DFFF] = null;
            _writeRanges[Ranges.Region_E000_FFFF] = null;
        }

        public byte ReadRawMain(int address) => _pool[address]; // $C000-$CFFF returns $D000-$DFFF Bank 1
        public byte ReadRawAux(int address) => _pool[address + 0x10000]; // $C000-$CFFF returns $D000-$DFFF Bank 1

        public byte ReadPool(int address) => _pool[address];

        public void WritePool(int address, byte value) => _pool[address] = value;

        private byte ReadFromRegion(Ranges region, int address)
        {
            _readRanges.TryGetValue(region, out var mem);
            if (!mem.HasValue)
            { return 0; }
            var m = mem.Value;
            int baseAddr = (int) region;
            int offset = address - baseAddr;
            if ((uint) offset >= m.Length)
            { return 0; }
            return m.Span[offset];
        }

        private void WriteToRegion(Ranges region, int address, byte value)
        {
            _writeRanges.TryGetValue(region, out var mem);
            if (!mem.HasValue)
            {
                return;
            }

            var m = mem.Value;
            int baseAddr = (int) region;
            int offset = address - baseAddr;
            if ((uint) offset >= m.Length)
            {
                return;
            }

            m.Span[offset] = value;
        }

        public byte ReadMapped(ushort address) => address switch
        {
            >= (ushort) Ranges.Region_E000_FFFF => ReadFromRegion(Ranges.Region_E000_FFFF, address),
            >= (ushort) Ranges.Region_D000_DFFF => ReadFromRegion(Ranges.Region_D000_DFFF, address),
            >= (ushort) Ranges.Region_C800_CFFF => ReadFromRegion(Ranges.Region_C800_CFFF, address),
            >= (ushort) Ranges.Region_C700_C7FF => ReadFromRegion(Ranges.Region_C700_C7FF, address),
            >= (ushort) Ranges.Region_C600_C6FF => ReadFromRegion(Ranges.Region_C600_C6FF, address),
            >= (ushort) Ranges.Region_C500_C5FF => ReadFromRegion(Ranges.Region_C500_C5FF, address),
            >= (ushort) Ranges.Region_C400_C4FF => ReadFromRegion(Ranges.Region_C400_C4FF, address),
            >= (ushort) Ranges.Region_C300_C3FF => ReadFromRegion(Ranges.Region_C300_C3FF, address),
            >= (ushort) Ranges.Region_C200_C2FF => ReadFromRegion(Ranges.Region_C200_C2FF, address),
            >= (ushort) Ranges.Region_C100_C1FF => ReadFromRegion(Ranges.Region_C100_C1FF, address),
            >= (ushort) Ranges.Region_C000_C0FF => ReadFromRegion(Ranges.Region_C000_C0FF, address),
            >= (ushort) Ranges.Region_6000_BFFF => ReadFromRegion(Ranges.Region_6000_BFFF, address),
            >= (ushort) Ranges.Region_4000_5FFF => ReadFromRegion(Ranges.Region_4000_5FFF, address),
            >= (ushort) Ranges.Region_2000_3FFF => ReadFromRegion(Ranges.Region_2000_3FFF, address),
            >= (ushort) Ranges.Region_0800_1FFF => ReadFromRegion(Ranges.Region_0800_1FFF, address),
            >= (ushort) Ranges.Region_0400_07FF => ReadFromRegion(Ranges.Region_0400_07FF, address),
            >= (ushort) Ranges.Region_0200_03FF => ReadFromRegion(Ranges.Region_0200_03FF, address),
            _ => ReadFromRegion(Ranges.Region_0000_01FF, address)
        };

        public void WriteMapped(ushort address, byte value)
        {
            var range = address switch
            {
                >= (ushort) Ranges.Region_E000_FFFF => Ranges.Region_E000_FFFF,
                >= (ushort) Ranges.Region_D000_DFFF => Ranges.Region_D000_DFFF,
                >= (ushort) Ranges.Region_C800_CFFF => Ranges.Region_C800_CFFF,
                >= (ushort) Ranges.Region_C700_C7FF => Ranges.Region_C700_C7FF,
                >= (ushort) Ranges.Region_C600_C6FF => Ranges.Region_C600_C6FF,
                >= (ushort) Ranges.Region_C500_C5FF => Ranges.Region_C500_C5FF,
                >= (ushort) Ranges.Region_C400_C4FF => Ranges.Region_C400_C4FF,
                >= (ushort) Ranges.Region_C300_C3FF => Ranges.Region_C300_C3FF,
                >= (ushort) Ranges.Region_C200_C2FF => Ranges.Region_C200_C2FF,
                >= (ushort) Ranges.Region_C100_C1FF => Ranges.Region_C100_C1FF,
                >= (ushort) Ranges.Region_C000_C0FF => Ranges.Region_C000_C0FF,
                >= (ushort) Ranges.Region_6000_BFFF => Ranges.Region_6000_BFFF,
                >= (ushort) Ranges.Region_4000_5FFF => Ranges.Region_4000_5FFF,
                >= (ushort) Ranges.Region_2000_3FFF => Ranges.Region_2000_3FFF,
                >= (ushort) Ranges.Region_0800_1FFF => Ranges.Region_0800_1FFF,
                >= (ushort) Ranges.Region_0400_07FF => Ranges.Region_0400_07FF,
                >= (ushort) Ranges.Region_0200_03FF => Ranges.Region_0200_03FF,
                _ => Ranges.Region_0000_01FF
            };
            WriteToRegion(range, address, value);
            MemoryWritten?.Invoke(this, new MemoryAccessEventArgs { Address = address, Value = value, Length = 1 });

        }



        public void SetRamRd(bool ramRd)
        {
            _ramRd = ramRd;
            UpdateMemoryMappings();
        }

        public void SetRamWrt(bool ramWrt)
        {
            _ramWrt = ramWrt;
            UpdateMemoryMappings();
        }

        public void SetAltZp(bool altZp)
        {
            _altZp = altZp;
            UpdateMemoryMappings();
        }

        public void Set80Store(bool store80)
        {
            _80Store = store80;
            UpdateMemoryMappings();
        }

        public void SetHiRes(bool hires)
        {
            _hires = hires;
            UpdateMemoryMappings();
        }

        public void SetPage2(bool page2)
        {
            _page2 = page2;
            UpdateMemoryMappings();
        }

        public void SetCxRom(bool _ /*intCxRom*/)
        {
            // Temporarily Disabled
            //    if (intCxRom)
            //    {
            //       // TODO
            //    }
            //    else
            //    {
            //        //TODO
            //    }
            UpdateMemoryMappings();
        }

        public void SetSlotC3Rom(bool _ /* intCxRom */ )
        {
            // Temporarily Disabled
            //    if (slotC3Rom)
            //    {
            //        //TODO
            //    }
            //    else
            //    {
            //        //TODO
            //    }
            UpdateMemoryMappings();
        }

        public void UpdateMemoryMappings()
        {
            _readRanges[Ranges.Region_0000_01FF] = _altZp ? _a1 : _m1;
            _writeRanges[Ranges.Region_0000_01FF] = _altZp ? _a1 : _m1;

            int matrixval = 0;
            matrixval |= (_ramRd ? 0x08 : 0x00);
            matrixval |= (_80Store ? 0x04 : 0x00);
            matrixval |= (_hires ? 0x02 : 0x00);
            matrixval |= (_page2 ? 0x01 : 0x00);

            /*
             * +---------- RamRd/RamWrt
             * | +-------- 80Store
             * | | +------ Hires
             * | | | +---- Page2      0200-/0800-/4000-/6000-    0400-     2000-
             * | | | |                03ff /1fff /5fff /bfff     07ff      3fff
             * -------                -------------------------  --------  ------
             * 0 x x x   (0x00-0x07)  Main                       Main      Main
             * 1 0 x x   (0x08-0x0B)  Aux                        Aux       Aux
             * 1 1 0 0   (0x0C)       Aux                        Main      Aux
             * 1 1 0 1   (0x0D)       Aux                        Aux       Main
             * 1 1 1 0   (0x0E)       Aux                        Main      Main
             * 1 1 1 1   (0x0F)       Aux                        Aux       Aux
             */

            switch (matrixval)
            {
                case int n when (n < 8): // Main RAM RamRd is low, else don't care. 
                    _readRanges[Ranges.Region_0200_03FF] = _m2;
                    _readRanges[Ranges.Region_0400_07FF] = _m3;
                    _readRanges[Ranges.Region_0800_1FFF] = _m4;
                    _readRanges[Ranges.Region_2000_3FFF] = _m5;
                    _readRanges[Ranges.Region_4000_5FFF] = _m6;
                    _readRanges[Ranges.Region_6000_BFFF] = _m7;
                    break;
                case int n when (n>=8 && n<0x0c): // Auxiliary RAM RamRd is high and 80Store is low, else don't care.
                    _readRanges[Ranges.Region_0200_03FF] = _a2;
                    _readRanges[Ranges.Region_0400_07FF] = _a3;
                    _readRanges[Ranges.Region_0800_1FFF] = _a4;
                    _readRanges[Ranges.Region_2000_3FFF] = _a5;
                    _readRanges[Ranges.Region_4000_5FFF] = _a6;
                    _readRanges[Ranges.Region_6000_BFFF] = _a7;
                    break;
                case 0x0c:  
                    _readRanges[Ranges.Region_0200_03FF] = _a2;
                    _readRanges[Ranges.Region_0400_07FF] = _m3;
                    _readRanges[Ranges.Region_0800_1FFF] = _a4;
                    _readRanges[Ranges.Region_2000_3FFF] = _a5;
                    _readRanges[Ranges.Region_4000_5FFF] = _m6;
                    _readRanges[Ranges.Region_6000_BFFF] = _m7;
                    break;
                case 0x0d:
                    _readRanges[Ranges.Region_0200_03FF] = _a2;
                    _readRanges[Ranges.Region_0400_07FF] = _a3;
                    _readRanges[Ranges.Region_0800_1FFF] = _a4;
                    _readRanges[Ranges.Region_2000_3FFF] = _m5;
                    _readRanges[Ranges.Region_4000_5FFF] = _m6;
                    _readRanges[Ranges.Region_6000_BFFF] = _m7;
                    break;
                case 0x0e:
                    _readRanges[Ranges.Region_0200_03FF] = _a2;
                    _readRanges[Ranges.Region_0400_07FF] = _m3;
                    _readRanges[Ranges.Region_0800_1FFF] = _a4;
                    _readRanges[Ranges.Region_2000_3FFF] = _m5;
                    _readRanges[Ranges.Region_4000_5FFF] = _m6;
                    _readRanges[Ranges.Region_6000_BFFF] = _m7;
                    break;
                case 0x0f:
                    _readRanges[Ranges.Region_0200_03FF] = _a2;
                    _readRanges[Ranges.Region_0400_07FF] = _a3;
                    _readRanges[Ranges.Region_0800_1FFF] = _a4;
                    _readRanges[Ranges.Region_2000_3FFF] = _a5;
                    _readRanges[Ranges.Region_4000_5FFF] = _m6;
                    _readRanges[Ranges.Region_6000_BFFF] = _m7;
                    break;
                default:
                    throw new Exception("Execution should never get here.");
            }

            // Swap out RamRd for RamWrt in matrixval
            matrixval &= 0x07; // preserve lower 3 bits
            matrixval |= (_ramWrt ? 0x08 : 0x00);


            switch (matrixval)
            {
                case int n when (n < 8): // Main RAM RamWrt is low, else don't care. 
                    _writeRanges[Ranges.Region_0200_03FF] = _m2;
                    _writeRanges[Ranges.Region_0400_07FF] = _m3;
                    _writeRanges[Ranges.Region_0800_1FFF] = _m4;
                    _writeRanges[Ranges.Region_2000_3FFF] = _m5;
                    _writeRanges[Ranges.Region_4000_5FFF] = _m6;
                    _writeRanges[Ranges.Region_6000_BFFF] = _m7;
                    break;
                case int n when (n >= 8 && n < 0x0c): // Auxiliary RAM RamWrt is high and 80Store is low, else don't care.
                    _writeRanges[Ranges.Region_0200_03FF] = _a2;
                    _writeRanges[Ranges.Region_0400_07FF] = _a3;
                    _writeRanges[Ranges.Region_0800_1FFF] = _a4;
                    _writeRanges[Ranges.Region_2000_3FFF] = _a5;
                    _writeRanges[Ranges.Region_4000_5FFF] = _a6;
                    _writeRanges[Ranges.Region_6000_BFFF] = _a7;
                    break;
                case 0x0c:
                    _writeRanges[Ranges.Region_0200_03FF] = _a2;
                    _writeRanges[Ranges.Region_0400_07FF] = _m3;
                    _writeRanges[Ranges.Region_0800_1FFF] = _a4;
                    _writeRanges[Ranges.Region_2000_3FFF] = _a5;
                    _writeRanges[Ranges.Region_4000_5FFF] = _m6;
                    _writeRanges[Ranges.Region_6000_BFFF] = _m7;
                    break;
                case 0x0d:
                    _writeRanges[Ranges.Region_0200_03FF] = _a2;
                    _writeRanges[Ranges.Region_0400_07FF] = _a3;
                    _writeRanges[Ranges.Region_0800_1FFF] = _a4;
                    _writeRanges[Ranges.Region_2000_3FFF] = _m5;
                    _writeRanges[Ranges.Region_4000_5FFF] = _m6;
                    _writeRanges[Ranges.Region_6000_BFFF] = _m7;
                    break;
                case 0x0e:
                    _writeRanges[Ranges.Region_0200_03FF] = _a2;
                    _writeRanges[Ranges.Region_0400_07FF] = _m3;
                    _writeRanges[Ranges.Region_0800_1FFF] = _a4;
                    _writeRanges[Ranges.Region_2000_3FFF] = _m5;
                    _writeRanges[Ranges.Region_4000_5FFF] = _m6;
                    _writeRanges[Ranges.Region_6000_BFFF] = _m7;
                    break;
                case 0x0f:
                    _writeRanges[Ranges.Region_0200_03FF] = _a2;
                    _writeRanges[Ranges.Region_0400_07FF] = _a3;
                    _writeRanges[Ranges.Region_0800_1FFF] = _a4;
                    _writeRanges[Ranges.Region_2000_3FFF] = _a5;
                    _writeRanges[Ranges.Region_4000_5FFF] = _m6;
                    _writeRanges[Ranges.Region_6000_BFFF] = _m7;
                    break;
                default:
                    throw new Exception("Execution should never get here.");
            }



            /*
   

                // Region_D000_DFFF -> Slot ROM
                /// First determine ROM or RAM
                /// If RAM
                ///   Determine MAIN or AUX
                ///   Determine BANK1/BANK2 (and Reading/Writing permissions)

                // Region_E000_FFFF
                /// Determine if ROM or RAM (Look at READBSR1/READBSR2/WRITEBSR1/WRITEBSR2/OFFBSR1/OFFBSR2)
                /// If RAM
                ///   Look at RAMRD/RAMWRT to determine MAIN or AUX
                ///     Examine READBSR[1/2]/WRITEBSR[1/2]/OFFBSR[1/2] to determine BANK (and Reading/Writing permissions)

                // Region C000_CFFF -> I/O and Internal Slots
                /// At this point, this is scratch ram and isn't used from the user-facing side.

                // Each region from Region_C100_C1FF to Region_C200_C2FF and Region_C400_C4FF Region_C700_C7FF
                /// If INTCXROM is set, map to Internal Slot ROM (_int1-_int7)
                /// Else if there is a card in the slot, map to that card's I/O region

                // Region_C300_C3FF
                /// IF INTCXROM or SLOTC3ROM is set, map to Internal Slot ROM (_int3)
                /// Else if there is a card in Slot 3, map to that card's I/O region

            */
        }

        public void InstallApple2ROM(byte[] rom)
        {
            // check rom size and throw if not 16k
            if (rom.Length != 0x4000)
            {
                throw new Exception("Apple IIe ROM must be exactly 16KB in size.");
            }

            // rom should be 16k with the rom data filling _io, _int1-_int7, _intext, _rom1, _rom2
            rom.AsSpan(0x0000, 0x0100).CopyTo(_io.Span);
            rom.AsSpan(0x0100, 0x0100).CopyTo(_int1.Span);
            rom.AsSpan(0x0200, 0x0100).CopyTo(_int2.Span);
            rom.AsSpan(0x0300, 0x0100).CopyTo(_int3.Span);
            rom.AsSpan(0x0400, 0x0100).CopyTo(_int4.Span);
            rom.AsSpan(0x0500, 0x0100).CopyTo(_int5.Span);
            rom.AsSpan(0x0600, 0x0100).CopyTo(_int6.Span);
            rom.AsSpan(0x0700, 0x0100).CopyTo(_int7.Span);
            rom.AsSpan(0x0800, 0x0800).CopyTo(_intext.Span);
            rom.AsSpan(0x1000, 0x1000).CopyTo(_rom1.Span);

            rom.AsSpan(0x2000, 0x2000).CopyTo(_rom2.Span);


        }
    }
}