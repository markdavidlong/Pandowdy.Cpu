using Pandowdy.EmuCore.DataTypes;

namespace Pandowdy.EmuCore.Tests;

/// <summary>
/// Tests for BitmapDataArray - the 560Ã—192 pixel array that stores Apple II display data.
/// Each pixel uses a BitField16 to support multiple independent bitplanes for colors
/// and rendering attributes.
/// </summary>
public class BitmapDataArrayTests
{
    [Fact]
    public void Dimensions_Are560x192()
    {
        // Assert
        Assert.Equal(560, BitmapDataArray.Width);
        Assert.Equal(192, BitmapDataArray.Height);
    }

    [Fact]
    public void Constructor_InitializesEmpty()
    {
        // Arrange & Act
        var bitmap = new BitmapDataArray();

        // Assert - Spot check a few pixels
        Assert.False(bitmap.GetPixel(0, 0, 0));
        Assert.False(bitmap.GetPixel(100, 100, 0));
        Assert.False(bitmap.GetPixel(559, 191, 0));
    }

    [Fact]
    public void SetPixel_SetsCorrectBit()
    {
        // Arrange
        var bitmap = new BitmapDataArray();

        // Act
        bitmap.SetPixel(100, 50, 3);

        // Assert
        Assert.True(bitmap.GetPixel(100, 50, 3));
        Assert.False(bitmap.GetPixel(100, 50, 0)); // Other bitplanes unaffected
        Assert.False(bitmap.GetPixel(100, 50, 1));
        Assert.False(bitmap.GetPixel(100, 50, 2));
    }

    [Fact]
    public void ClearPixel_ClearsBit()
    {
        // Arrange
        var bitmap = new BitmapDataArray();
        bitmap.SetPixel(100, 50, 3);

        // Act
        bitmap.ClearPixel(100, 50, 3);

        // Assert
        Assert.False(bitmap.GetPixel(100, 50, 3));
    }

    [Fact]
    public void MultipleBitplanes_AreIndependent()
    {
        // Arrange
        var bitmap = new BitmapDataArray();

        // Act - Set different bitplanes at same location
        bitmap.SetPixel(200, 100, 0);
        bitmap.SetPixel(200, 100, 1);
        bitmap.SetPixel(200, 100, 3);

        // Assert
        Assert.True(bitmap.GetPixel(200, 100, 0));
        Assert.True(bitmap.GetPixel(200, 100, 1));
        Assert.False(bitmap.GetPixel(200, 100, 2)); // Not set
        Assert.True(bitmap.GetPixel(200, 100, 3));
    }

    [Fact]
    public void Clear_ResetsAllPixels()
    {
        // Arrange
        var bitmap = new BitmapDataArray();
        bitmap.SetPixel(10, 10, 0);
        bitmap.SetPixel(20, 20, 1);
        bitmap.SetPixel(30, 30, 2);

        // Act
        bitmap.Clear();

        // Assert
        Assert.False(bitmap.GetPixel(10, 10, 0));
        Assert.False(bitmap.GetPixel(20, 20, 1));
        Assert.False(bitmap.GetPixel(30, 30, 2));
    }

    [Fact]
    public void CornerPixels_AllAccessible()
    {
        // Arrange
        var bitmap = new BitmapDataArray();

        // Act & Assert - All corners
        bitmap.SetPixel(0, 0, 0);           // Top-left
        bitmap.SetPixel(559, 0, 0);         // Top-right
        bitmap.SetPixel(0, 191, 0);         // Bottom-left
        bitmap.SetPixel(559, 191, 0);       // Bottom-right

        Assert.True(bitmap.GetPixel(0, 0, 0));
        Assert.True(bitmap.GetPixel(559, 0, 0));
        Assert.True(bitmap.GetPixel(0, 191, 0));
        Assert.True(bitmap.GetPixel(559, 191, 0));
    }






    [Fact]
    public void GetRowDataSpan_ReturnsCorrectWidth()
    {
        // Arrange
        var bitmap = new BitmapDataArray();

        // Act
        var span = bitmap.GetRowDataSpan(0);

        // Assert
        Assert.Equal(560, span.Length);
    }

    [Fact]
    public void GetRowDataSpan_ReturnsCorrectData()
    {
        // Arrange
        var bitmap = new BitmapDataArray();
        bitmap.SetPixel(100, 50, 0);
        bitmap.SetPixel(200, 50, 1);

        // Act
        var span = bitmap.GetRowDataSpan(50);

        // Assert
        Assert.True(span[100].GetBit(0));
        Assert.True(span[200].GetBit(1));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(192)]
    [InlineData(200)]
    public void GetRowDataSpan_InvalidRow_ThrowsException(int row)
    {
        // Arrange
        var bitmap = new BitmapDataArray();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => bitmap.GetRowDataSpan(row));
    }

    [Fact]
    public void GetBitplaneSpanForRow_ReturnsCorrectWidth()
    {
        // Arrange
        var bitmap = new BitmapDataArray();

        // Act
        var span = bitmap.GetBitplaneSpanForRow(0, 0);

        // Assert
        Assert.Equal(560, span.Length);
    }

    [Fact]
    public void GetBitplaneSpanForRow_ReturnsCorrectData()
    {
        // Arrange
        var bitmap = new BitmapDataArray();
        bitmap.SetPixel(100, 50, 2);
        bitmap.SetPixel(200, 50, 2);
        bitmap.SetPixel(300, 50, 3); // Different bitplane

        // Act
        var span = bitmap.GetBitplaneSpanForRow(50, 2);

        // Assert
        Assert.True(span[100]);
        Assert.True(span[200]);
        Assert.False(span[300]); // Different bitplane
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(192, 0)]
    [InlineData(200, 0)]
    public void GetBitplaneSpanForRow_InvalidRow_ThrowsException(int row, int bitplane)
    {
        // Arrange
        var bitmap = new BitmapDataArray();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => bitmap.GetBitplaneSpanForRow(row, bitplane));
    }

    [Fact]
    public void PixelIsolation_BetweenRows()
    {
        // Arrange
        var bitmap = new BitmapDataArray();

        // Act - Set pixel in row 0
        bitmap.SetPixel(100, 0, 0);

        // Assert - Row 1 should be unaffected
        Assert.True(bitmap.GetPixel(100, 0, 0));
        Assert.False(bitmap.GetPixel(100, 1, 0));
    }

    [Fact]
    public void FullRowPattern()
    {
        // Arrange
        var bitmap = new BitmapDataArray();

        // Act - Set alternating pixels in row 100
        for (int x = 0; x < 560; x += 2)
        {
            bitmap.SetPixel(x, 100, 0);
        }

        // Assert - Verify pattern
        for (int x = 0; x < 560; x++)
        {
            bool expected = (x % 2 == 0);
            Assert.Equal(expected, bitmap.GetPixel(x, 100, 0));
        }
    }
}
