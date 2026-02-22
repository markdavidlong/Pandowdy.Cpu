// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.EmuCore.Interfaces;

/// <summary>
/// Provides read-only access to the Apple IIe system ROM ($C000-$FFFF).
/// </summary>
/// <remarks>
/// <para>
/// <strong>ROM Layout (16KB):</strong> The system ROM covers the top 16KB of the Apple IIe
/// address space, containing I/O firmware, internal peripheral ROMs, the Monitor, and
/// Applesoft BASIC.
/// </para>
/// <para>
/// <strong>Implements <see cref="IPandowdyMemory"/>:</strong> The <see cref="IPandowdyMemory.Read"/>
/// method provides indexed access with an offset of 0 representing $C000. To read $D000, use
/// offset 0x1000; to read $FFFF, use offset 0x3FFF.
/// </para>
/// <para>
/// <strong>Loading:</strong> ROM data is loaded via <see cref="LoadRomFile"/>, either from
/// the file system or from an embedded assembly resource (use the <c>res:</c> prefix).
/// </para>
/// </remarks>
/// <seealso cref="IPandowdyMemory"/>
public interface ISystemRomProvider : IPandowdyMemory
{
    /// <summary>
    /// Loads the 16KB system ROM from a file path or embedded resource identifier.
    /// </summary>
    /// <param name="filename">
    /// Path to the ROM file, or an embedded resource name prefixed with <c>res:</c>
    /// (e.g., <c>"res:Pandowdy.EmuCore.Resources.a2e_enh_c-f.rom"</c>).
    /// The file must be exactly 16KB (16,384 bytes).
    /// </param>
    /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="filename"/> is null.</exception>
    /// <exception cref="System.IO.FileNotFoundException">
    /// Thrown if the specified file or embedded resource cannot be found.
    /// </exception>
    /// <exception cref="System.IO.InvalidDataException">
    /// Thrown if the ROM data is not exactly 16KB or cannot be read.
    /// </exception>
    public void LoadRomFile(string filename);
}
