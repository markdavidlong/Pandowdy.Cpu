using Pandowdy.EmuCore.Interfaces;
using Pandowdy.EmuCore.Services;

namespace Pandowdy.EmuCore.DiskII;

/// <summary>
/// Factory for creating Disk II drives with status integration.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Decorator Chain:</strong> Creates drives wrapped in decorators:
/// <code>
/// DiskIIDebugDecorator â†’ DiskIIStatusDecorator â†’ DiskIIDrive
/// </code>
/// - <see cref="DiskIIDrive"/>: Core drive implementation
/// - <see cref="DiskIIStatusDecorator"/>: Synchronizes state with <see cref="IDiskStatusMutator"/>
/// - <see cref="DiskIIDebugDecorator"/>: Adds diagnostic logging (outermost layer)
/// </para>
/// <para>
/// <strong>Status Integration:</strong> The <see cref="DiskIIStatusDecorator"/> automatically
/// registers the drive with the status provider and publishes state changes for motor,
/// track position, and disk operations.
/// </para>
/// <para>
/// <strong>Slot/Drive Parsing:</strong> Parses drive names in the format "SlotX-DY"
/// (e.g., "Slot6-D1" â†’ Slot 6, Drive 1) to assign proper slot and drive numbers
/// for status tracking.
/// </para>
/// </remarks>
/// <remarks>
/// Initializes a new instance of the <see cref="DiskIIFactory"/> class.
/// </remarks>
/// <param name="imageFactory">Factory for creating disk image providers.</param>
/// <param name="statusMutator">Status mutator for registering drives and publishing state.</param>
public class DiskIIFactory(IDiskImageFactory imageFactory, IDiskStatusMutator statusMutator) : IDiskIIFactory
{
    private readonly IDiskImageFactory _imageFactory = imageFactory ?? throw new ArgumentNullException(nameof(imageFactory));
    private readonly IDiskStatusMutator _statusMutator = statusMutator ?? throw new ArgumentNullException(nameof(statusMutator));

    /// <summary>
    /// Creates a Disk II drive with no disk inserted.
    /// </summary>
    /// <param name="driveName">Name for the drive (e.g., "Slot6-D1").</param>
    /// <returns>A new drive instance with no disk, wrapped in decorators.</returns>
    public IDiskIIDrive CreateDrive(string driveName)
    {
        // Parse slot and drive numbers from name
        var (slotNumber, driveNumber) = ParseDriveName(driveName);

        // Create core drive (no disk inserted)
        var coreDrive = new DiskIIDrive(
            driveName,
            imageProvider: null,
            diskImageFactory: _imageFactory);

        // Wrap in status decorator for UI integration
        var statusDrive = new DiskIIStatusDecorator(coreDrive, _statusMutator, slotNumber, driveNumber);

        // Wrap in debug decorator for diagnostic logging (outermost layer)
        return new DiskIIDebugDecorator(statusDrive);
    }

    /// <summary>
    /// Creates a Disk II drive with a disk image loaded.
    /// </summary>
    /// <param name="driveName">Name for the drive (e.g., "Slot6-D1").</param>
    /// <param name="diskImagePath">Path to disk image file (.nib, .woz, .dsk, etc.).</param>
    /// <returns>A new drive instance with the specified disk loaded.</returns>
    public IDiskIIDrive CreateDriveWithDisk(string driveName, string diskImagePath)
    {
        // Parse slot and drive numbers from name
        var (slotNumber, driveNumber) = ParseDriveName(driveName);

        // Create image provider
        IDiskImageProvider provider = _imageFactory.CreateProvider(diskImagePath);

        // Create core drive with disk
        var coreDrive = new DiskIIDrive(
            driveName,
            imageProvider: provider,
            diskImageFactory: _imageFactory);

        // Wrap in status decorator for UI integration
        var statusDrive = new DiskIIStatusDecorator(coreDrive, _statusMutator, slotNumber, driveNumber);

        // Wrap in debug decorator (outermost layer)
        return new DiskIIDebugDecorator(statusDrive);
    }

    /// <summary>
    /// Parses drive name in the format "SlotX-DY" to extract slot and drive numbers.
    /// </summary>
    /// <param name="driveName">Drive name (e.g., "Slot6-D1").</param>
    /// <returns>Tuple of (slotNumber, driveNumber), or (6, 1) as default if parsing fails.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Format:</strong> Expects names like "Slot6-D1", "Slot2-D2", etc.
    /// </para>
    /// <para>
    /// <strong>Fallback:</strong> Returns (6, 1) if parsing fails, which corresponds to
    /// the typical boot slot configuration (Slot 6, Drive 1).
    /// </para>
    /// </remarks>
    internal static (int slotNumber, int driveNumber) ParseDriveName(string driveName)
    {
        try
        {
            // Expected format: "Slot6-D1"
            // Split on '-' to get ["Slot6", "D1"]
            var parts = driveName.Split('-');
            if (parts.Length != 2)
            {
                return (6, 1); // Default fallback
            }

            // Extract slot number from "Slot6"
            string slotPart = parts[0];
            if (!slotPart.StartsWith("Slot", StringComparison.OrdinalIgnoreCase))
            {
                return (6, 1);
            }

            if (!int.TryParse(slotPart.AsSpan(4), out int slotNumber))
            {
                return (6, 1);
            }

            // Extract drive number from "D1"
            string drivePart = parts[1];
            if (!drivePart.StartsWith("D", StringComparison.OrdinalIgnoreCase))
            {
                return (6, 1);
            }

            if (!int.TryParse(drivePart.AsSpan(1), out int driveNumber))
            {
                return (6, 1);
            }

            return (slotNumber, driveNumber);
        }
        catch
        {
            // Parsing failed - return default
            return (6, 1);
        }
    }
}
