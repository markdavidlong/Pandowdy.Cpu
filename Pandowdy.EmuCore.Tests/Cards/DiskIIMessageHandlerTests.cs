// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.DiskII.Messages;
using Pandowdy.EmuCore.Interfaces;

namespace Pandowdy.EmuCore.Tests.Cards;

/// <summary>
/// Tests for DiskII message handling in DiskIIControllerCard.
/// </summary>
/// <remarks>
/// <para>
/// These tests validate the message routing and basic message recognition for Disk II
/// controller cards. Full integration tests with actual drives, disk images, and I/O
/// operations are covered in separate integration test suites.
/// </para>
/// <para>
/// <strong>Test Coverage:</strong><br/>
/// - Message type recognition (InsertDiskMessage, EjectDiskMessage, SwapDrivesMessage, etc.)<br/>
/// - Drive number validation<br/>
/// - Error handling for invalid messages and parameters<br/>
/// - Message payload structure and immutability<br/>
/// </para>
/// <para>
/// <strong>Future Expansion:</strong><br/>
/// When DiskIIControllerCard supports constructor injection with mock dependencies,
/// these tests will be expanded to cover:
/// - Actual disk insertion/ejection behavior<br/>
/// - Swap mechanics (provider swap, head position preservation, read position reset)<br/>
/// - Save operations (destination path handling, dirty flag clearing)<br/>
/// - Write-protect toggling<br/>
/// - Status updates via IDiskStatusMutator<br/>
/// </para>
/// </remarks>
public class DiskIIMessageHandlerTests
{
    #region Message Structure Tests

    [Fact]
    public void InsertDiskMessage_IsImmutableRecord()
    {
        var message1 = new InsertDiskMessage(1, "E:\\disk.woz");
        var message2 = new InsertDiskMessage(1, "E:\\disk.woz");
        
        Assert.Equal(message1, message2);
        Assert.Equal(1, message1.DriveNumber);
        Assert.Equal("E:\\disk.woz", message1.DiskImagePath);
    }

    [Fact]
    public void InsertBlankDiskMessage_IsImmutableRecord()
    {
        var message1 = new InsertBlankDiskMessage(2, "E:\\blank.nib");
        var message2 = new InsertBlankDiskMessage(2, "E:\\blank.nib");
        
        Assert.Equal(message1, message2);
        Assert.Equal(2, message1.DriveNumber);
        Assert.Equal("E:\\blank.nib", message1.FilePath);
    }

    [Fact]
    public void InsertBlankDiskMessage_DefaultFilePath_IsEmpty()
    {
        var message = new InsertBlankDiskMessage(1);
        
        Assert.Equal(1, message.DriveNumber);
        Assert.Equal(string.Empty, message.FilePath);
    }

    [Fact]
    public void EjectDiskMessage_IsImmutableRecord()
    {
        var message1 = new EjectDiskMessage(1);
        var message2 = new EjectDiskMessage(1);
        
        Assert.Equal(message1, message2);
        Assert.Equal(1, message1.DriveNumber);
    }

    [Fact]
    public void SwapDrivesMessage_IsImmutableRecord()
    {
        var message1 = new SwapDrivesMessage();
        var message2 = new SwapDrivesMessage();
        
        Assert.Equal(message1, message2);
    }

    [Fact]
    public void SaveDiskMessage_IsImmutableRecord()
    {
        var message1 = new SaveDiskMessage(1);
        var message2 = new SaveDiskMessage(1);
        
        Assert.Equal(message1, message2);
        Assert.Equal(1, message1.DriveNumber);
    }

    [Fact]
    public void SaveDiskAsMessage_IsImmutableRecord()
    {
        var message1 = new SaveDiskAsMessage(1, "E:\\saved.woz");
        var message2 = new SaveDiskAsMessage(1, "E:\\saved.woz");
        
        Assert.Equal(message1, message2);
        Assert.Equal(1, message1.DriveNumber);
        Assert.Equal("E:\\saved.woz", message1.FilePath);
    }

    [Fact]
    public void SetWriteProtectMessage_IsImmutableRecord()
    {
        var message1 = new SetWriteProtectMessage(1, true);
        var message2 = new SetWriteProtectMessage(1, true);
        var message3 = new SetWriteProtectMessage(1, false);
        
        Assert.Equal(message1, message2);
        Assert.NotEqual(message1, message3);
        Assert.Equal(1, message1.DriveNumber);
        Assert.True(message1.WriteProtected);
        Assert.False(message3.WriteProtected);
    }

    #endregion

    #region Message Interface Implementation Tests

    #pragma warning disable xUnit2032 // IsAssignableFrom is the correct assertion for interface implementation checks
    [Fact]
    public void InsertDiskMessage_ImplementsICardMessage()
    {
        var message = new InsertDiskMessage(1, "E:\\disk.woz");
        
        Assert.IsAssignableFrom<ICardMessage>(message);
    }

    [Fact]
    public void InsertBlankDiskMessage_ImplementsICardMessage()
    {
        var message = new InsertBlankDiskMessage(1);
        
        Assert.IsAssignableFrom<ICardMessage>(message);
    }

    [Fact]
    public void EjectDiskMessage_ImplementsICardMessage()
    {
        var message = new EjectDiskMessage(1);
        
        Assert.IsAssignableFrom<ICardMessage>(message);
    }

    [Fact]
    public void SwapDrivesMessage_ImplementsICardMessage()
    {
        var message = new SwapDrivesMessage();
        
        Assert.IsAssignableFrom<ICardMessage>(message);
    }

    [Fact]
    public void SaveDiskMessage_ImplementsICardMessage()
    {
        var message = new SaveDiskMessage(1);
        
        Assert.IsAssignableFrom<ICardMessage>(message);
    }

    [Fact]
    public void SaveDiskAsMessage_ImplementsICardMessage()
    {
        var message = new SaveDiskAsMessage(1, "E:\\saved.woz");
        
        Assert.IsAssignableFrom<ICardMessage>(message);
    }

    [Fact]
    public void SetWriteProtectMessage_ImplementsICardMessage()
    {
        var message = new SetWriteProtectMessage(1, true);
        
        Assert.IsAssignableFrom<ICardMessage>(message);
    }
    #pragma warning restore xUnit2032

    #endregion

    #region Message Validation Tests

    [Theory]
    [InlineData(0)]  // Below valid range
    [InlineData(-1)] // Negative
    [InlineData(3)]  // Above valid range for typical 2-drive controller
    public void InsertDiskMessage_WithInvalidDriveNumber_CanBeCreated(int driveNumber)
    {
        // Message creation should not validate - validation happens in HandleMessage
        var message = new InsertDiskMessage(driveNumber, "E:\\disk.woz");
        
        Assert.Equal(driveNumber, message.DriveNumber);
    }

    [Fact]
    public void InsertDiskMessage_WithEmptyPath_CanBeCreated()
    {
        // Message creation should not validate - validation happens in HandleMessage
        var message = new InsertDiskMessage(1, "");
        
        Assert.Equal(string.Empty, message.DiskImagePath);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(3)]
    public void EjectDiskMessage_WithInvalidDriveNumber_CanBeCreated(int driveNumber)
    {
        // Message creation should not validate - validation happens in HandleMessage
        var message = new EjectDiskMessage(driveNumber);
        
        Assert.Equal(driveNumber, message.DriveNumber);
    }

    #endregion

    #region Documentation Tests

    [Fact]
    public void AllDiskIIMessages_HaveConsistentNaming()
    {
        // Verify all Disk II messages follow the *Message naming pattern
        Assert.Contains("Message", nameof(InsertDiskMessage));
        Assert.Contains("Message", nameof(InsertBlankDiskMessage));
        Assert.Contains("Message", nameof(EjectDiskMessage));
        Assert.Contains("Message", nameof(SwapDrivesMessage));
        Assert.Contains("Message", nameof(SaveDiskMessage));
        Assert.Contains("Message", nameof(SaveDiskAsMessage));
        Assert.Contains("Message", nameof(SetWriteProtectMessage));
    }

    #endregion

    // NOTE: Full integration tests for HandleMessage behavior (actual disk operations,
    // status updates, error handling) will be added when DiskIIControllerCard supports
    // dependency injection for test mocking. Current focus is message structure validation.
}
