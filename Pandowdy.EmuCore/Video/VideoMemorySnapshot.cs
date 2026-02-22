// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.IO;

namespace Pandowdy.EmuCore.Video;

/// <summary>
/// Immutable snapshot of Apple IIe system RAM for threaded rendering.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Purpose:</strong> Captures a point-in-time copy of the full 48KB main and auxiliary
/// RAM banks. This allows rendering to occur on a separate thread without blocking the emulator
/// thread or risking memory corruption from concurrent access.
/// </para>
/// <para>
/// <strong>Simplified Architecture:</strong> Instead of pre-slicing video memory regions into
/// separate arrays, this captures the entire 48KB main and 48KB auxiliary RAM banks. The renderer
/// then indexes directly into these arrays for video memory regions:
/// <list type="bullet">
/// <item>Text Page 1: $0400-$07FF (1KB)</item>
/// <item>Text Page 2: $0800-$0BFF (1KB)</item>
/// <item>Hi-Res Page 1: $2000-$3FFF (8KB)</item>
/// <item>Hi-Res Page 2: $4000-$5FFF (8KB)</item>
/// </list>
/// </para>
/// <para>
/// <strong>Performance Benefits:</strong>
/// <list type="bullet">
/// <item>Single bulk copy per bank (48KB each) instead of 8 separate slice copies</item>
/// <item>Simpler memory layout - renderer uses direct array indexing</item>
/// <item>Faster snapshot capture (~50% fewer operations)</item>
/// <item>Total size: 96KB (negligible overhead on modern systems)</item>
/// </list>
/// </para>
/// <para>
/// <strong>Pooling:</strong> Instances should be pooled and reused to avoid GC pressure.
/// See <see cref="VideoMemorySnapshotPool"/> for pooling implementation.
/// </para>
/// </remarks>
public sealed class VideoMemorySnapshot
{
    /// <summary>
    /// Full 48KB main RAM bank ($0000-$BFFF).
    /// </summary>
    /// <remarks>
    /// Video memory regions within this array:
    /// <list type="bullet">
    /// <item>Text Page 1: [0x0400..0x07FF]</item>
    /// <item>Text Page 2: [0x0800..0x0BFF]</item>
    /// <item>Hi-Res Page 1: [0x2000..0x3FFF]</item>
    /// <item>Hi-Res Page 2: [0x4000..0x5FFF]</item>
    /// </list>
    /// </remarks>
    public readonly byte[] MainRam = new byte[0xC000];  // 48KB
    
    /// <summary>
    /// Full 48KB auxiliary RAM bank ($0000-$BFFF).
    /// </summary>
    /// <remarks>
    /// Video memory regions within this array (for 80-column and double hi-res modes):
    /// <list type="bullet">
    /// <item>Aux Text Page 1: [0x0400..0x07FF]</item>
    /// <item>Aux Text Page 2: [0x0800..0x0BFF]</item>
    /// <item>Aux Hi-Res Page 1: [0x2000..0x3FFF]</item>
    /// <item>Aux Hi-Res Page 2: [0x4000..0x5FFF]</item>
    /// </list>
    /// </remarks>
    public readonly byte[] AuxRam = new byte[0xC000];   // 48KB
    
    /// <summary>
    /// Soft switch states at the time of snapshot.
    /// </summary>
    /// <remarks>
    /// Determines rendering mode (text/graphics, 40/80 column, page selection, etc.).
    /// </remarks>
    public SystemStatusSnapshot? SoftSwitches { get; set; }
    
    /// <summary>
    /// Frame sequence number for debugging and sync verification.
    /// </summary>
    public ulong FrameNumber { get; set; }
    
    /// <summary>
    /// Resets both RAM banks to zero (for pooling reuse).
    /// </summary>
    /// <remarks>
    /// Clears 96KB total (48KB main + 48KB aux). Takes ~1-2ms on modern CPUs.
    /// </remarks>
    public void Clear()
    {
        Array.Clear(MainRam);
        Array.Clear(AuxRam);
        SoftSwitches = null;
        FrameNumber = 0;
    }
}
