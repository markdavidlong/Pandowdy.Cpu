namespace Pandowdy.EmuCore.Interfaces;

/// <summary>
/// Provides low-level disk image data to emulate Disk II hardware behavior.
/// </summary>
/// <remarks>
/// <para>
/// This interface abstracts the disk image format from the controller card logic.
/// The controller card operates at the hardware level (stepper motors, shift registers),
/// while implementations handle format-specific details (NIB, WOZ, DSK, etc.).
/// </para>
/// <para>
/// <strong>Implementations:</strong>
/// <list type="bullet">
/// <item><see cref="DiskII.Providers.NibDiskImageProvider"/> - Raw GCR nibble format (.nib)</item>
/// <item><see cref="DiskII.Providers.SectorDiskImageProvider"/> - Sector format (.dsk/.do/.po)</item>
/// <item><see cref="DiskII.Providers.InternalWozDiskImageProvider"/> - WOZ flux timing (.woz)</item>
/// </list>
/// </para>
/// </remarks>
public interface IDiskImageProvider : IDisposable
{
    /// <summary>
    /// Gets the file path of the disk image.
    /// </summary>
    string FilePath { get; }

    /// <summary>
    /// Gets whether this image supports write operations.
    /// </summary>
    bool IsWritable { get; }

    /// <summary>
    /// Gets or sets whether the disk is write-protected (simulates physical write-protect tab).
    /// </summary>
    bool IsWriteProtected { get; set; }

    /// <summary>
    /// Sets the current quarter-track position (0-139 for 35 tracks).
    /// </summary>
    /// <param name="qTrack">Quarter-track position (0-3 = track 0, 4-7 = track 1, etc.).</param>
    void SetQuarterTrack(int qTrack);

    /// <summary>
    /// Gets the current quarter-track position.
    /// </summary>
    int CurrentQuarterTrack { get; }

    /// <summary>
    /// Reads the next bit from the current track at the current rotational position.
    /// </summary>
    /// <param name="cycleCount">Current CPU cycle count (for timing-sensitive formats like WOZ).</param>
    /// <returns>The next bit value, or null if no disk or invalid position.</returns>
    bool? GetBit(ulong cycleCount);

    /// <summary>
    /// Writes a bit to the current track (if supported).
    /// </summary>
    /// <param name="bit">The bit value to write.</param>
    /// <param name="cycleCount">Current CPU cycle count.</param>
    /// <returns>True if write succeeded, false if write-protected or unsupported.</returns>
    bool WriteBit(bool bit, ulong cycleCount);

    /// <summary>
    /// Flushes any pending writes to disk.
    /// </summary>
    void Flush();
}
