// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.DataTypes;
using Pandowdy.EmuCore.Video;

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

    #region Boundary Conditions Tests (6 tests)

    [Fact]
    public void SetPixel_MaxBitplane_Works()
    {
        // Arrange
        var bitmap = new BitmapDataArray();

        // Act - Set on bitplane 15 (max)
        bitmap.SetPixel(100, 50, 15);

        // Assert
        Assert.True(bitmap.GetPixel(100, 50, 15));
        Assert.False(bitmap.GetPixel(100, 50, 14)); // Other bitplanes unaffected
    }

    [Fact]
    public void SetPixel_AllBitplanes_Independent()
    {
        // Arrange
        var bitmap = new BitmapDataArray();

        // Act - Set all 16 bitplanes at same pixel
        for (int plane = 0; plane < 16; plane++)
        {
            bitmap.SetPixel(250, 96, plane);
        }

        // Assert - All should be set
        for (int plane = 0; plane < 16; plane++)
        {
            Assert.True(bitmap.GetPixel(250, 96, plane));
        }
    }

    [Fact]
    public void ClearPixel_OnUnsetPixel_NoError()
    {
        // Arrange
        var bitmap = new BitmapDataArray();

        // Act - Clear pixel that was never set (idempotent)
        bitmap.ClearPixel(100, 50, 3);

        // Assert - Should remain false, no exception
        Assert.False(bitmap.GetPixel(100, 50, 3));
    }

    [Fact]
    public void SetPixel_MultipleTimesIdempotent()
    {
        // Arrange
        var bitmap = new BitmapDataArray();

        // Act - Set same pixel multiple times
        bitmap.SetPixel(100, 50, 5);
        bitmap.SetPixel(100, 50, 5);
        bitmap.SetPixel(100, 50, 5);

        // Assert - Should remain true
        Assert.True(bitmap.GetPixel(100, 50, 5));
    }

    [Fact]
    public void BoundaryPixels_TopEdge_AllAccessible()
    {
        // Arrange
        var bitmap = new BitmapDataArray();

        // Act & Assert - All pixels in top row (y=0)
        for (int x = 0; x < 560; x++)
        {
            bitmap.SetPixel(x, 0, 0);
            Assert.True(bitmap.GetPixel(x, 0, 0));
        }
    }

    [Fact]
    public void BoundaryPixels_BottomEdge_AllAccessible()
    {
        // Arrange
        var bitmap = new BitmapDataArray();

        // Act & Assert - All pixels in bottom row (y=191)
        for (int x = 0; x < 560; x++)
        {
            bitmap.SetPixel(x, 191, 0);
            Assert.True(bitmap.GetPixel(x, 191, 0));
        }
    }

    #endregion

    #region GetMutableRowDataSpan Tests (8 tests)

    [Fact]
    public void GetMutableRowDataSpan_ReturnsCorrectWidth()
    {
        // Arrange
        var bitmap = new BitmapDataArray();

        // Act
        var span = bitmap.GetMutableRowDataSpan(0);

        // Assert
        Assert.Equal(560, span.Length);
    }

    [Fact]
    public void GetMutableRowDataSpan_AllowsDirectModification()
    {
        // Arrange
        var bitmap = new BitmapDataArray();

        // Act - Get mutable span and modify directly
        var span = bitmap.GetMutableRowDataSpan(50);
        span[100].SetBit(0, true);
        span[200].SetBit(1, true);

        // Assert - Modifications persist
        Assert.True(bitmap.GetPixel(100, 50, 0));
        Assert.True(bitmap.GetPixel(200, 50, 1));
    }

    [Fact]
    public void GetMutableRowDataSpan_PerformancePath_DirectRendering()
    {
        // Arrange
        var bitmap = new BitmapDataArray();

        // Act - Render entire row using mutable span (high-performance path)
        var span = bitmap.GetMutableRowDataSpan(100);
        for (int x = 0; x < span.Length; x++)
        {
            if (x % 4 < 2) // Pattern: on-on-off-off
            {
                span[x].SetBit(0, true);
            }
        }

        // Assert - Verify pattern rendered correctly
        for (int x = 0; x < 560; x++)
        {
            bool expected = (x % 4 < 2);
            Assert.Equal(expected, bitmap.GetPixel(x, 100, 0));
        }
    }

    [Fact]
    public void GetMutableRowDataSpan_MultipleBitplanes_Simultaneously()
    {
        // Arrange
        var bitmap = new BitmapDataArray();

        // Act - Set multiple bitplanes via mutable span
        var span = bitmap.GetMutableRowDataSpan(75);
        span[150].SetBit(0, true);
        span[150].SetBit(1, true);
        span[150].SetBit(2, true);

        // Assert - All bitplanes should be set
        Assert.True(bitmap.GetPixel(150, 75, 0));
        Assert.True(bitmap.GetPixel(150, 75, 1));
        Assert.True(bitmap.GetPixel(150, 75, 2));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(192)]
    [InlineData(500)]
    public void GetMutableRowDataSpan_InvalidRow_ThrowsException(int row)
    {
        // Arrange
        var bitmap = new BitmapDataArray();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => bitmap.GetMutableRowDataSpan(row));
    }

    [Fact]
    public void GetMutableRowDataSpan_FirstRow_Works()
    {
        // Arrange
        var bitmap = new BitmapDataArray();

        // Act - Access first row (boundary test)
        var span = bitmap.GetMutableRowDataSpan(0);
        span[0].SetBit(0, true);
        span[559].SetBit(0, true);

        // Assert
        Assert.True(bitmap.GetPixel(0, 0, 0));
        Assert.True(bitmap.GetPixel(559, 0, 0));
    }

    [Fact]
    public void GetMutableRowDataSpan_LastRow_Works()
    {
        // Arrange
        var bitmap = new BitmapDataArray();

        // Act - Access last row (boundary test)
        var span = bitmap.GetMutableRowDataSpan(191);
        span[0].SetBit(0, true);
        span[559].SetBit(0, true);

        // Assert
        Assert.True(bitmap.GetPixel(0, 191, 0));
        Assert.True(bitmap.GetPixel(559, 191, 0));
    }

    [Fact]
    public void GetMutableRowDataSpan_ModifyDoesNotAffectAdjacentRows()
    {
        // Arrange
        var bitmap = new BitmapDataArray();

        // Act - Modify row 50 via mutable span
        var span = bitmap.GetMutableRowDataSpan(50);
        for (int x = 0; x < span.Length; x++)
        {
            span[x].SetBit(0, true);
        }

        // Assert - Adjacent rows should be unaffected
        for (int x = 0; x < 560; x++)
        {
            Assert.False(bitmap.GetPixel(x, 49, 0)); // Row above
            Assert.True(bitmap.GetPixel(x, 50, 0));  // Modified row
            Assert.False(bitmap.GetPixel(x, 51, 0)); // Row below
        }
    }

    #endregion

    #region Stress and Edge Cases Tests (6 tests)

    [Fact]
    public void EntireBitmap_SetAndClear_AllPixels()
    {
        // Arrange
        var bitmap = new BitmapDataArray();

        // Act - Set all pixels on bitplane 0
        for (int y = 0; y < 192; y++)
        {
            for (int x = 0; x < 560; x++)
            {
                bitmap.SetPixel(x, y, 0);
            }
        }

        // Assert - All should be set
        for (int y = 0; y < 192; y++)
        {
            for (int x = 0; x < 560; x++)
            {
                Assert.True(bitmap.GetPixel(x, y, 0));
            }
        }

        // Act - Clear entire bitmap
        bitmap.Clear();

        // Assert - All should be clear
        for (int y = 0; y < 192; y++)
        {
            for (int x = 0; x < 560; x++)
            {
                Assert.False(bitmap.GetPixel(x, y, 0));
            }
        }
    }

    [Fact]
    public void CheckerboardPattern_AcrossAllBitplanes()
    {
        // Arrange
        var bitmap = new BitmapDataArray();

        // Act - Create checkerboard on all bitplanes
        for (int plane = 0; plane < 16; plane++)
        {
            for (int y = 0; y < 192; y++)
            {
                for (int x = 0; x < 560; x++)
                {
                    if ((x + y) % 2 == 0)
                    {
                        bitmap.SetPixel(x, y, plane);
                    }
                }
            }
        }

        // Assert - Verify checkerboard on all planes
        for (int plane = 0; plane < 16; plane++)
        {
            for (int y = 0; y < 192; y++)
            {
                for (int x = 0; x < 560; x++)
                {
                    bool expected = ((x + y) % 2 == 0);
                    Assert.Equal(expected, bitmap.GetPixel(x, y, plane));
                }
            }
        }
    }

    [Fact]
    public void MixedOperations_SetClearQuery_MaintainConsistency()
    {
        // Arrange
        var bitmap = new BitmapDataArray();

        // Act - Mix of operations
        bitmap.SetPixel(100, 50, 0);
        Assert.True(bitmap.GetPixel(100, 50, 0));

        bitmap.ClearPixel(100, 50, 0);
        Assert.False(bitmap.GetPixel(100, 50, 0));

        bitmap.SetPixel(100, 50, 0);
        Assert.True(bitmap.GetPixel(100, 50, 0));

        bitmap.Clear();
        Assert.False(bitmap.GetPixel(100, 50, 0));
    }

    [Fact]
    public void HighFrequencyUpdates_SamePixel_MaintainsState()
    {
        // Arrange
        var bitmap = new BitmapDataArray();

        // Act - Rapidly toggle same pixel 1000 times
        for (int i = 0; i < 1000; i++)
        {
            if (i % 2 == 0)
            {
                bitmap.SetPixel(250, 96, 7);
            }
            else
            {
                bitmap.ClearPixel(250, 96, 7);
            }
        }

        // Assert - Should end up clear (even number of toggles)
        Assert.False(bitmap.GetPixel(250, 96, 7));
    }

    [Fact]
    public void DiagonalLine_AcrossBitmap()
    {
        // Arrange
        var bitmap = new BitmapDataArray();

        // Act - Draw diagonal line from (0,0) to (191,191)
        // Scale X to fit 560 width: x = y * (560/192) = y * 2.916...
        for (int y = 0; y < 192; y++)
        {
            int x = (y * 560) / 192; // Scale to width
            bitmap.SetPixel(x, y, 0);
        }

        // Assert - Verify diagonal pixels are set
        for (int y = 0; y < 192; y++)
        {
            int x = (y * 560) / 192;
            Assert.True(bitmap.GetPixel(x, y, 0));

            // Adjacent pixels should be clear
            if (x > 0)
            {
                Assert.False(bitmap.GetPixel(x - 1, y, 0));
            }
            if (x < 559)
            {
                Assert.False(bitmap.GetPixel(x + 1, y, 0));
            }
        }
    }

    [Fact]
    public void VerticalStripes_AllRows()
    {
        // Arrange
        var bitmap = new BitmapDataArray();

        // Act - Create vertical stripes (every 10th column)
        for (int x = 0; x < 560; x += 10)
        {
            for (int y = 0; y < 192; y++)
            {
                bitmap.SetPixel(x, y, 0);
            }
        }

        // Assert - Verify stripes
        for (int x = 0; x < 560; x++)
        {
            for (int y = 0; y < 192; y++)
            {
                bool expected = (x % 10 == 0);
                Assert.Equal(expected, bitmap.GetPixel(x, y, 0));
            }
        }
    }

    #endregion
}
