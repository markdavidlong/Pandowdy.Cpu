namespace Pandowdy.EmuCore.DiskII;

/// <summary>
/// Shared constants for Disk II emulation timing and geometry.
/// </summary>
/// <remarks>
/// <para>
/// These constants define the physical and electrical characteristics of the
/// Apple II Disk II drive system, including timing, track geometry, and sector layout.
/// </para>
/// <para>
/// <strong>Timing Basis:</strong> The Apple II CPU runs at 1.023 MHz while the Disk II
/// reads/writes at 250 kHz (4Î¼s per bit). This creates a ratio of exactly 45/11 CPU
/// cycles per bit, which must be maintained for accurate disk timing.
/// </para>
/// </remarks>
public static class DiskIIConstants
{
    /// <summary>
    /// Cycles per bit for accurate Apple II Disk II timing.
    /// </summary>
    /// <remarks>
    /// The disk reads at 250 kHz (4Î¼s per bit) while the CPU runs at 1.023 MHz.
    /// This gives exactly 45/11 cycles per bit â‰ˆ 4.090909 cycles/bit.
    /// </remarks>
    public const double CyclesPerBit = 45.0 / 11.0; // 4.090909...

    /// <summary>
    /// Number of tracks on a standard 5.25" disk.
    /// </summary>
    public const int TrackCount = 35;

    /// <summary>
    /// Bytes per track in NIB format (raw GCR nibbles).
    /// </summary>
    public const int BytesPerNibTrack = 6656;

    /// <summary>
    /// Bits per track (6656 bytes Ã— 8 bits = 53,248 bits).
    /// </summary>
    public const int BitsPerTrack = BytesPerNibTrack * 8;

    /// <summary>
    /// Maximum quarter-track position.
    /// </summary>
    /// <remarks>
    /// 35 tracks Ã— 4 quarter-steps per track = 140, plus position 0 = 141 total positions.
    /// Valid positions are 0-140, representing tracks 0.00 to 35.00.
    /// </remarks>
    public const int MaxQuarterTracks = 35 * 4; // 140

    /// <summary>
    /// Sectors per track for 16-sector format (DOS 3.3, ProDOS).
    /// </summary>
    public const int SectorsPerTrack16 = 16;

    /// <summary>
    /// Sectors per track for 13-sector format (DOS 3.2).
    /// </summary>
    public const int SectorsPerTrack13 = 13;

    /// <summary>
    /// Bytes per sector.
    /// </summary>
    public const int BytesPerSector = 256;

    /// <summary>
    /// Total bytes per disk in 16-sector format (35 tracks Ã— 16 sectors Ã— 256 bytes).
    /// </summary>
    public const int TotalBytes16Sector = TrackCount * SectorsPerTrack16 * BytesPerSector; // 143,360

    /// <summary>
    /// Total bytes per disk in 13-sector format (35 tracks Ã— 13 sectors Ã— 256 bytes).
    /// </summary>
    public const int TotalBytes13Sector = TrackCount * SectorsPerTrack13 * BytesPerSector; // 116,480

    /// <summary>
    /// Motor-off delay in VBlank frames (~1 second at 60 Hz).
    /// </summary>
    /// <remarks>
    /// The Disk II controller waits approximately 1 second after the last access
    /// before turning off the motor. At 60 VBlank frames per second, this is ~60 frames.
    /// </remarks>
    public const int MotorOffDelayFrames = 60;

    /// <summary>
    /// Telemetry category identifier for Disk II devices.
    /// </summary>
    public const string TelemetryCategory = "DiskII";
}
