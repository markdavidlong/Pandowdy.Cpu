namespace Pandowdy.EmuCore.DataTypes;

/// <summary>
/// Simple debug message for DiskII telemetry during development.
/// </summary>
/// <remarks>
/// <para>
/// This is a simplified placeholder for DiskII telemetry during initial development.
/// It provides human-readable debug messages like "Disk Motor is now on" or 
/// "Seeking to Track 17, Sector 5".
/// </para>
/// <para>
/// <strong>Future Enhancement:</strong> When the GUI is ready to consume telemetry,
/// this will be replaced with typed message records (DiskIIMotorMessage, DiskIITrackMessage, etc.)
/// that allow pattern matching and structured data access.
/// </para>
/// </remarks>
/// <param name="Message">Human-readable description of the state change.</param>
public readonly record struct DiskIIMessage(string Message)
{
    /// <summary>
    /// Returns the message text.
    /// </summary>
    public override string ToString() => Message;
}
