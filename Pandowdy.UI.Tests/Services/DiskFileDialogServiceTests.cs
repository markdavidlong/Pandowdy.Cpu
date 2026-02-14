// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.UI.Services;
using Xunit;

namespace Pandowdy.UI.Tests.Services;

/// <summary>
/// Tests for the <see cref="DiskFileDialogService"/> class.
/// </summary>
public class DiskFileDialogServiceTests
{
    #region IsSupportedDiskImage Tests

    [Theory]
    [InlineData("E:\\test.woz", true)]
    [InlineData("E:\\test.nib", true)]
    [InlineData("E:\\test.dsk", true)]
    [InlineData("E:\\test.do", true)]
    [InlineData("E:\\test.po", true)]
    [InlineData("E:\\test.2mg", true)]
    [InlineData("E:\\test.WOZ", true)] // Case-insensitive
    [InlineData("E:\\test.NIB", true)]
    [InlineData("E:\\test.txt", false)]
    [InlineData("E:\\test.exe", false)]
    [InlineData("E:\\test", false)] // No extension
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsSupportedDiskImage_WithVariousExtensions_ReturnsExpectedResult(string? filePath, bool expected)
    {
        // Act
        var result = DiskFileDialogService.IsSupportedDiskImage(filePath!);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void IsSupportedDiskImage_WithEmptyString_ReturnsFalse()
    {
        // Arrange
        var filePath = string.Empty;

        // Act
        var result = DiskFileDialogService.IsSupportedDiskImage(filePath);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsSupportedDiskImage_WithWhitespace_ReturnsFalse()
    {
        // Arrange
        var filePath = "   ";

        // Act
        var result = DiskFileDialogService.IsSupportedDiskImage(filePath);

        // Assert
        Assert.False(result);
    }

    #endregion

    // Note: PickDiskImageForInsertAsync and PickDiskImageForSaveAsync tests require mocking
    // IStorageProvider, which is complex and not essential for Phase 3B basic functionality.
    // These methods are tested through integration tests where the UI is used interactively.
}
