using Pandowdy.EmuCore.Interfaces;

namespace Pandowdy.EmuCore.DiskII.Providers;

/// <summary>
/// Factory for creating disk image providers based on file format detection.
/// </summary>
/// <remarks>
/// This factory examines the file extension to determine which provider implementation
/// to create. Supported formats:
/// <list type="bullet">
/// <item><strong>.nib</strong> - Raw GCR nibble format (232,960 bytes)</item>
/// <item><strong>.woz</strong> - WOZ format with timing information (most accurate)</item>
/// <item><strong>.dsk, .do, .po</strong> - Sector-based formats (143,360 bytes)</item>
/// <item><strong>.2mg, .2img</strong> - 2IMG wrapper format (with header)</item>
/// </list>
/// </remarks>
public class DiskImageFactory : IDiskImageFactory
{
    /// <summary>
    /// Creates an appropriate disk image provider for the given file.
    /// </summary>
    /// <param name="filePath">Path to disk image file.</param>
    /// <returns>A provider implementation for the detected format.</returns>
    /// <exception cref="ArgumentNullException">Thrown if filePath is null or empty.</exception>
    /// <exception cref="FileNotFoundException">Thrown if the file doesn't exist.</exception>
    /// <exception cref="NotSupportedException">Thrown if format is not supported.</exception>
    public IDiskImageProvider CreateProvider(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentNullException(nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Disk image file not found: {filePath}", filePath);
        }

        string extension = Path.GetExtension(filePath).ToLowerInvariant();

        return extension switch
        {
            ".nib" => new NibDiskImageProvider(filePath),
            ".woz" => new InternalWozDiskImageProvider(filePath), // Native WOZ parser (default)
            ".dsk" or ".do" or ".po" => new SectorDiskImageProvider(filePath),
            ".2mg" or ".2img" => new SectorDiskImageProvider(filePath),
            _ => throw new NotSupportedException(
                $"Unsupported disk image format: {extension}\n" +
                "Supported formats: .nib, .woz, .dsk, .do, .po, .2mg, .2img")
        };
    }

    /// <summary>
    /// Checks if a file format is supported based on file extension.
    /// </summary>
    /// <param name="filePath">Path to check.</param>
    /// <returns>True if the format is supported.</returns>
    public bool IsFormatSupported(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        string extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension is ".nib" or ".woz" or ".dsk" or ".do" or ".po" or ".2mg" or ".2img";
    }
}
