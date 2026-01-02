namespace Pandowdy.EmuCore
{
    public struct BitField16
    {
        private UInt16 _value;

        public UInt16 Value
        {
            readonly get => _value;
            set => _value = value;
        }

        private static int BitWidth => sizeof(UInt16) * 8;

        private static void CheckIndex(int index)
        {
            if ((uint)index >= BitWidth)
            {
                throw new ArgumentOutOfRangeException(nameof(index), $"Bit index {index} is outside the width of ({BitWidth} bits.");
            }
        }

        public readonly bool GetBit(int index)
        {
            CheckIndex(index);
            UInt16 mask = (UInt16) (1 << index);
            return (_value & mask) != 0;
        }

        public void SetBit(int index, bool state)
        {
            CheckIndex(index);
            UInt16 mask = (UInt16) (1 << index);

            if (state)
            {
                _value |= mask;
            }
            else
            {
                _value &= (UInt16) ~mask;
            }
        }
    }
}
