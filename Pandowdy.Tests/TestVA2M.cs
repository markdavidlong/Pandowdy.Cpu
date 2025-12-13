using System.Reflection;
using Pandowdy.Core;
using Xunit;

namespace Pandowdy.Tests
{
    public sealed class TestVA2M
    {
        private static int InvokeGetAddressForXY(int x, int y, bool text, bool hires, bool mixed, bool page2, int cellRowOffset = 0)
        {
            MethodInfo? mi = typeof(VA2M).GetMethod("GetAddressForXY", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(mi);
            object? result = mi!.Invoke(null, new object[] { x, y, text, hires, mixed, page2, cellRowOffset });
            Assert.NotNull(result);
            return (int)result!;
        }

        [Fact]
        public void TestGetAddresForXY()
        {
            // Text Page1 basics
            int addr00 = InvokeGetAddressForXY(0, 0, text: true, hires: false, mixed: false, page2: false);
            Assert.Equal(0x0400, addr00);

            int addr10 = InvokeGetAddressForXY(1, 0, text: true, hires: false, mixed: false, page2: false);
            Assert.Equal(0x0401, addr10);

            int addr01 = InvokeGetAddressForXY(0, 1, text: true, hires: false, mixed: false, page2: false);
            Assert.Equal(0x0400 + 128, addr01);

            int addr08 = InvokeGetAddressForXY(0, 8, text: true, hires: false, mixed: false, page2: false);
            Assert.Equal(0x0400 + 40, addr08);

            int addr16 = InvokeGetAddressForXY(0, 16, text: true, hires: false, mixed: false, page2: false);
            Assert.Equal(0x0400 + 80, addr16);

            // Text Page2
            int addrPage2 = InvokeGetAddressForXY(0, 0, text: true, hires: false, mixed: false, page2: true);
            Assert.Equal(0x0800, addrPage2);

            // HiRes Page1
            int addrHires00 = InvokeGetAddressForXY(0, 0, text: false, hires: true, mixed: false, page2: false);
            Assert.Equal(0x2000, addrHires00);

            int addrHires08 = InvokeGetAddressForXY(0, 0, text: false, hires: true, mixed: false, page2: false, cellRowOffset: 1);
            Assert.Equal(0x2400, addrHires08);

            int addrHires72 = InvokeGetAddressForXY(0, 9, text: false, hires: true, mixed: false, page2: false, cellRowOffset: 0);
            Assert.Equal(0x20A8, addrHires72);

            int addrHires191_39 = InvokeGetAddressForXY(39, 23, text: false, hires: true, mixed: false, page2: false, cellRowOffset: 7);
            Assert.Equal(0x3ff7, addrHires191_39);

            // HiRes Page2
            int addr2Hires00 = InvokeGetAddressForXY(0, 0, text: false, hires: true, mixed: false, page2: true);
            Assert.Equal(0x4000, addr2Hires00);

            int addr2Hires08 = InvokeGetAddressForXY(0, 0, text: false, hires: true, mixed: false, page2: true, cellRowOffset: 1);
            Assert.Equal(0x4400, addr2Hires08);

            int addr2Hires72 = InvokeGetAddressForXY(0, 9, text: false, hires: true, mixed: false, page2: true, cellRowOffset: 0);
            Assert.Equal(0x40A8, addr2Hires72);

            int addr2Hires191_39 = InvokeGetAddressForXY(39, 23, text: false, hires: true, mixed: false, page2: true, cellRowOffset: 7);
            Assert.Equal(0x5ff7, addr2Hires191_39);

            int addr2Hires08_5 = InvokeGetAddressForXY(5, 0, text: false, hires: true, mixed: false, page2: true, cellRowOffset: 1);
            Assert.Equal(0x4405, addr2Hires08_5);

            // Mixed mode: y > 20 should map to text page1
            int addrMixedText = InvokeGetAddressForXY(0, 21, text: false, hires: true, mixed: true, page2: true);
            Assert.Equal(0x0800 + (21 % 8) * 128 + (21 / 8) * 40, addrMixedText);

            // Invalid coords
            int addrInvalid = InvokeGetAddressForXY(41, 0, text: true, hires: false, mixed: false, page2: true);
            Assert.Equal(-1, addrInvalid);
        }
    }
}
