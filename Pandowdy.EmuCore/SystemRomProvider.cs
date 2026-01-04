using System.Reflection;
using System.Runtime.CompilerServices;
using Pandowdy.EmuCore.Interfaces;

namespace Pandowdy.EmuCore
{
    /// <summary>
    /// Provides Apple IIe system ROM storage and access.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Manages the 16KB system ROM ($C000-$FFFF) including I/O firmware, peripheral ROMs,
    /// Monitor ROM, and Applesoft BASIC. The ROM can be loaded from either a file or an embedded resource.
    /// </para>
    /// <para>
    /// <strong>ROM Layout (16KB):</strong>
    /// <code>
    /// $C000-$C0FF (256 bytes)  - I/O space firmware
    /// $C100-$C7FF (1792 bytes) - Internal peripheral ROM (7 x 256 bytes)
    /// $C800-$CFFF (2KB)        - Extended internal ROM
    /// $D000-$DFFF (4KB)        - Monitor ROM / Language Card bank switching area
    /// $E000-$FFFF (8KB)        - Applesoft BASIC + reset vector / Language Card common area
    /// </code>
    /// </para>
    /// <para>
    /// <strong>Loading Sources:</strong>
    /// <list type="bullet">
    /// <item><strong>File System:</strong> Standard file path (e.g., "roms/AppleIIe.rom")</item>
    /// <item><strong>Embedded Resource:</strong> Use "res:" prefix (e.g., "res:Pandowdy.EmuCore.Resources.a2e_enh_c-f.rom")</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Address Mapping:</strong> The ROM provider's address space starts at offset 0,
    /// representing $C000. To access $D000, use offset 0x1000. To access $FFFF, use offset 0x3FFF.
    /// </para>
    /// </remarks>
    public sealed class SystemRomProvider : ISystemRomProvider
    {
        private const int RomSize = 0x4000; // 16KB ($C000-$FFFF)
        private const string ResourcePrefix = "res:";
        private readonly byte[] _romData = new byte[RomSize];

        /// <summary>
        /// Initializes a new instance of the SystemRomProvider class and loads ROM.
        /// </summary>
        /// <param name="filename">
        /// Path to the ROM file or resource identifier. Must be exactly 16KB (16,384 bytes).
        /// Use "res:" prefix for embedded resources (e.g., "res:Pandowdy.EmuCore.Resources.a2e_enh_c-f.rom").
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown if filename is null.</exception>
        /// <exception cref="FileNotFoundException">Thrown if the ROM file/resource cannot be found.</exception>
        /// <exception cref="InvalidDataException">
        /// Thrown if the ROM is not exactly 16KB or cannot be read.
        /// </exception>
        /// <remarks>
        /// <para>
        /// <strong>File System Example:</strong>
        /// <code>
        /// var rom = new SystemRomProvider("roms/AppleIIe.rom");
        /// </code>
        /// </para>
        /// <para>
        /// <strong>Embedded Resource Example:</strong>
        /// <code>
        /// var rom = new SystemRomProvider("res:Pandowdy.EmuCore.Resources.a2e_enh_c-f.rom");
        /// </code>
        /// </para>
        /// </remarks>
        public SystemRomProvider(string filename)
        {
            ArgumentNullException.ThrowIfNull(filename);

            LoadRomFile(filename);
        }

        /// <summary>
        /// Gets the size of the ROM in bytes (always 16KB).
        /// </summary>
        public int Size => RomSize;

        /// <summary>
        /// Reads a byte from the ROM at the specified address.
        /// </summary>
        /// <param name="address">Address within the ROM (0x0000-0x3FFF representing $C000-$FFFF).</param>
        /// <returns>Byte value at the specified address.</returns>
        /// <exception cref="IndexOutOfRangeException">
        /// Thrown if address is beyond ROM size.
        /// </exception>
        /// <remarks>
        /// <para>
        /// <strong>Address Mapping:</strong>
        /// <list type="bullet">
        /// <item>0x0000 = $C000 (I/O firmware start)</item>
        /// <item>0x1000 = $D000 (Monitor ROM start / Language Card bank area)</item>
        /// <item>0x2000 = $E000 (BASIC ROM start / Language Card common area)</item>
        /// <item>0x3FFF = $FFFF (End of ROM / Reset vector high byte)</item>
        /// </list>
        /// </para>
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte Read(ushort address)
        {
            return _romData[address];
        }

        /// <summary>
        /// Write operation - ROM is read-only, this is a no-op.
        /// </summary>
        /// <param name="address">Address (ignored).</param>
        /// <param name="data">Data (ignored).</param>
        /// <remarks>
        /// ROM is read-only. Writes are silently ignored to maintain IMemory compatibility.
        /// In real hardware, ROM writes have no effect.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(ushort address, byte data)
        {
            // ROM is read-only - no-op
        }

        /// <summary>
        /// Gets or sets a byte at the specified address. Setter is a no-op (ROM is read-only).
        /// </summary>
        /// <param name="address">Address within the ROM (0x0000-0x3FFF).</param>
        /// <returns>Byte value at the specified address.</returns>
        public byte this[ushort address]
        {
            get => Read(address);
            set { /* ROM is read-only - no-op */ }
        }

        /// <summary>
        /// Determines whether the filename specifies an embedded resource.
        /// </summary>
        /// <param name="filename">Filename or resource identifier to check.</param>
        /// <returns>True if the filename starts with "res:", false otherwise.</returns>
        private static bool IsResource(string filename)
        {
            return filename.StartsWith(ResourcePrefix, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Extracts the resource name from a resource identifier.
        /// </summary>
        /// <param name="filename">Resource identifier (e.g., "res:Pandowdy.EmuCore.Resources.rom").</param>
        /// <returns>Resource name without the "res:" prefix.</returns>
        private static string GetResourceName(string filename)
        {
            return filename[ResourcePrefix.Length..];
        }

        /// <summary>
        /// Loads ROM data from an embedded resource.
        /// </summary>
        /// <param name="resourceName">Fully-qualified resource name.</param>
        /// <returns>ROM data bytes.</returns>
        /// <exception cref="FileNotFoundException">Thrown if the resource cannot be found.</exception>
        /// <exception cref="InvalidDataException">Thrown if the resource size is incorrect.</exception>
        private static byte[] LoadFromResource(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream(resourceName) ?? throw new FileNotFoundException(
                    $"Embedded resource not found: {resourceName}. " +
                    "Ensure the resource is embedded with Build Action = Embedded Resource.",
                    resourceName);

            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }

        /// <summary>
        /// Loads ROM data from a file.
        /// </summary>
        /// <param name="filename">Path to the ROM file.</param>
        /// <returns>ROM data bytes.</returns>
        /// <exception cref="FileNotFoundException">Thrown if the file does not exist.</exception>
        private static byte[] LoadFromFile(string filename)
        {
            if (!File.Exists(filename))
            {
                throw new FileNotFoundException(
                    $"System ROM file not found: {filename}",
                    filename);
            }

            return File.ReadAllBytes(filename);
        }

        /// <summary>
        /// Loads ROM data from the specified file or resource into the internal ROM array.
        /// </summary>
        /// <param name="filename">
        /// Path to the ROM file or resource identifier.
        /// Use "res:" prefix for embedded resources (e.g., "res:Pandowdy.EmuCore.Resources.a2e_enh_c-f.rom").
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown if filename is null.</exception>
        /// <exception cref="FileNotFoundException">Thrown if file/resource does not exist.</exception>
        /// <exception cref="InvalidDataException">Thrown if ROM size is incorrect or read fails.</exception>
        /// <remarks>
        /// <para>
        /// <strong>Loading Decision:</strong>
        /// <list type="bullet">
        /// <item>If filename starts with "res:", load from embedded resource</item>
        /// <item>Otherwise, load from file system</item>
        /// </list>
        /// </para>
        /// </remarks>
        private void LoadRomFile(string filename)
        {
            ArgumentNullException.ThrowIfNull(filename);

            try
            {
                byte[] data;

                if (IsResource(filename))
                {
                    string resourceName = GetResourceName(filename);
                    data = LoadFromResource(resourceName);
                }
                else
                {
                    data = LoadFromFile(filename);
                }

                if (data.Length != RomSize)
                {
                    throw new InvalidDataException(
                        $"Invalid ROM size. Expected {RomSize} bytes (0x{RomSize:X}), " +
                        $"but '{filename}' is {data.Length} bytes (0x{data.Length:X}). " +
                        "The ROM must be exactly 16KB.");
                }

                Array.Copy(data, _romData, RomSize);
            }
            catch (Exception ex) when (ex is not InvalidDataException and not FileNotFoundException and not ArgumentNullException)
            {
                throw new InvalidDataException(
                    $"Failed to load ROM '{filename}': {ex.Message}",
                    ex);
            }
        }

        /// <summary>
        /// Loads or reloads ROM from the specified file or resource.
        /// </summary>
        /// <param name="filename">
        /// Path to the ROM file or resource identifier. Must be exactly 16KB.
        /// Use "res:" prefix for embedded resources.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown if filename is null.</exception>
        /// <exception cref="FileNotFoundException">Thrown if the ROM file/resource does not exist.</exception>
        /// <exception cref="InvalidDataException">
        /// Thrown if the ROM is not exactly 16KB or cannot be read.
        /// </exception>
        /// <remarks>
        /// This method allows runtime reloading of ROM, though this is rarely needed in practice.
        /// The ROM data is replaced atomically. Supports both file system and embedded resource loading.
        /// </remarks>
        void ISystemRomProvider.LoadRomFile(string filename)
        {
            LoadRomFile(filename);
        }
    }
}
