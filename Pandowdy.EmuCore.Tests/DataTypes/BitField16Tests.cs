using Pandowdy.EmuCore.DataTypes;


namespace Pandowdy.EmuCore.Tests.DataTypes;

/// <summary>
/// Tests for BitField16 struct - a 16-bit bitfield used for pixel data storage.
/// Each pixel in BitmapDataArray uses a BitField16 to store up to 16 independent
/// bitplanes for color and rendering attributes.
/// </summary>
public class BitField16Tests
{
    [Fact]
    public void DefaultValue_IsZero()
    {
        // Arrange & Act
        var bitfield = new BitField16();

        // Assert
        Assert.Equal(0, bitfield.Value);
    }

    [Fact]
    public void SetValue_UpdatesValue()
    {
        // Arrange
        var bitfield = new BitField16
        {
            // Act
            Value = 0x1234
        };

        // Assert
        Assert.Equal(0x1234, bitfield.Value);
    }

    [Fact]
    public void GetBit_ReturnsCorrectBit()
    {
        // Arrange
        var bitfield = new BitField16 { Value = 0b1010_1010_1010_1010 };

        // Act & Assert
        Assert.True(bitfield.GetBit(1));   // Bit 1 is set
        Assert.False(bitfield.GetBit(0));  // Bit 0 is clear
        Assert.True(bitfield.GetBit(15));  // Bit 15 is set
        Assert.False(bitfield.GetBit(14)); // Bit 14 is clear
    }

    [Fact]
    public void SetBit_True_SetsBit()
    {
        // Arrange
        var bitfield = new BitField16 { Value = 0 };

        // Act
        bitfield.SetBit(0, true);
        bitfield.SetBit(5, true);
        bitfield.SetBit(15, true);

        // Assert
        Assert.True(bitfield.GetBit(0));
        Assert.True(bitfield.GetBit(5));
        Assert.True(bitfield.GetBit(15));
        Assert.Equal(0b1000_0000_0010_0001, bitfield.Value);
    }

    [Fact]
    public void SetBit_False_ClearsBit()
    {
        // Arrange
        var bitfield = new BitField16 { Value = 0xFFFF };

        // Act
        bitfield.SetBit(0, false);
        bitfield.SetBit(5, false);
        bitfield.SetBit(15, false);

        // Assert
        Assert.False(bitfield.GetBit(0));
        Assert.False(bitfield.GetBit(5));
        Assert.False(bitfield.GetBit(15));
        Assert.Equal(0b0111_1111_1101_1110, bitfield.Value);
    }

    [Fact]
    public void SetBit_TogglingBit_Works()
    {
        // Arrange
        var bitfield = new BitField16 { Value = 0 };

        // Act & Assert - Set bit
        bitfield.SetBit(7, true);
        Assert.True(bitfield.GetBit(7));

        // Toggle off
        bitfield.SetBit(7, false);
        Assert.False(bitfield.GetBit(7));

        // Toggle on again
        bitfield.SetBit(7, true);
        Assert.True(bitfield.GetBit(7));
    }

    [Fact]
    public void AllBitsIndependent()
    {
        // Arrange
        var bitfield = new BitField16();

        // Act - Set every other bit
        for (int i = 0; i < 16; i += 2)
        {
            bitfield.SetBit(i, true);
        }

        // Assert - Verify pattern
        for (int i = 0; i < 16; i++)
        {
            if (i % 2 == 0)
            {
                Assert.True(bitfield.GetBit(i), $"Bit {i} should be set");
            }
            else
            {
                Assert.False(bitfield.GetBit(i), $"Bit {i} should be clear");
            }
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(100)]
    public void GetBit_InvalidIndex_ThrowsException(int invalidIndex)
    {
        // Arrange
        var bitfield = new BitField16();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => bitfield.GetBit(invalidIndex));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(100)]
    public void SetBit_InvalidIndex_ThrowsException(int invalidIndex)
    {
        // Arrange
        var bitfield = new BitField16();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => bitfield.SetBit(invalidIndex, true));
    }

    [Fact]
    public void ValidIndices_AreZeroTo15()
    {
        // Arrange
        var bitfield = new BitField16();

        // Act & Assert - All valid indices should work
        for (int i = 0; i < 16; i++)
        {
            bitfield.SetBit(i, true);
            Assert.True(bitfield.GetBit(i));
        }
    }

    [Fact]
    public void HighBitOperations()
    {
        // Arrange
        var bitfield = new BitField16 { Value = 0x8000 };

        // Act & Assert
        Assert.True(bitfield.GetBit(15));
        Assert.False(bitfield.GetBit(14));

        bitfield.SetBit(15, false);
        Assert.False(bitfield.GetBit(15));
        Assert.Equal(0, bitfield.Value);
    }

    [Fact]
    public void MultipleOperations_MaintainCorrectState()
    {
        // Arrange
        var bitfield = new BitField16();

        // Act - Complex sequence
        bitfield.SetBit(0, true);   // 0x0001
        bitfield.SetBit(4, true);   // 0x0011
        bitfield.SetBit(8, true);   // 0x0111
        bitfield.SetBit(0, false);  // 0x0110
        bitfield.SetBit(12, true);  // 0x1110

        // Assert
        Assert.False(bitfield.GetBit(0));
        Assert.True(bitfield.GetBit(4));
        Assert.True(bitfield.GetBit(8));
        Assert.True(bitfield.GetBit(12));
        Assert.Equal(0x1110, bitfield.Value);
    }

    [Fact]
    public void IsValueType()
    {
        // Arrange
        var bf1 = new BitField16 { Value = 0x1234 };

        // Act - Copy by value
        var bf2 = bf1;
        bf2.Value = 0x5678;

        // Assert - Original unchanged (value type behavior)
        Assert.Equal(0x1234, bf1.Value);
        Assert.Equal(0x5678, bf2.Value);
    }
}
