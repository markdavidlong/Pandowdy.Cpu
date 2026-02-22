// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.EmuCore.Interfaces;

/// <summary>
/// Routes memory access between main and auxiliary RAM banks based on Apple IIe soft switch states.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Purpose:</strong> The Apple IIe has complex memory banking where reads and writes can
/// target either main RAM or auxiliary RAM depending on soft switch settings (RAMRD, RAMWRT, ALTZP,
/// 80STORE, PAGE2, HIRES). This interface manages that routing logic.
/// </para>
/// <para>
/// <strong>Address Space ($0000-$BFFF):</strong> The lower 48KB of address space where soft
/// switch-controlled banking applies:
/// <list type="bullet">
/// <item><strong>$0000-$01FF:</strong> Zero page/stack (ALTZP switch)</item>
/// <item><strong>$0200-$03FF:</strong> Low RAM (RAMRD/RAMWRT or 80STORE+PAGE2)</item>
/// <item><strong>$0400-$07FF:</strong> Text Page 1 (RAMRD/RAMWRT or 80STORE+PAGE2)</item>
/// <item><strong>$0800-$1FFF:</strong> General RAM (RAMRD/RAMWRT)</item>
/// <item><strong>$2000-$3FFF:</strong> Hi-Res Page 1 (RAMRD/RAMWRT or 80STORE+HIRES+PAGE2)</item>
/// <item><strong>$4000-$BFFF:</strong> General RAM (RAMRD/RAMWRT)</item>
/// </list>
/// </para>
/// <para>
/// <strong>Interfaces Implemented:</strong>
/// <list type="bullet">
/// <item><see cref="IPandowdyMemory"/>: Standard Read/Write for CPU access with soft switch routing</item>
/// <item><see cref="IDirectMemoryPoolReader"/>: Raw access bypassing soft switches for rendering</item>
/// </list>
/// </para>
/// <para>
/// <strong>Performance:</strong> Memory access is a hot path (~1 million accesses per second).
/// Implementations should use aggressive inlining and minimize branching.
/// </para>
/// </remarks>
/// <seealso cref="ISystemRam"/>
/// <seealso cref="IDirectMemoryPoolReader"/>
/// <seealso cref="IPandowdyMemory"/>
public interface ISystemRamSelector : IPandowdyMemory, IDirectMemoryPoolReader, IRestartable
{
    /// <summary>
    /// Copies the entire main RAM bank (48KB) into the provided destination span.
    /// </summary>
    /// <param name="destination">
    /// A span of at least 48KB (0xC000 bytes) to receive the main RAM contents.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown if the destination span is smaller than 48KB.
    /// </exception>
    /// <remarks>
    /// <para>
    /// <strong>Usage:</strong> Called by the video snapshot system to capture main RAM for
    /// threaded video rendering. The snapshot includes text pages, hi-res pages, and all
    /// other main memory regions.
    /// </para>
    /// <para>
    /// <strong>Direct Access:</strong> This method bypasses soft switch routing and always
    /// copies from main RAM, regardless of RAMRD/RAMWRT settings.
    /// </para>
    /// </remarks>
    public void CopyMainMemoryIntoSpan(Span<byte> destination);

    /// <summary>
    /// Copies the entire auxiliary RAM bank (48KB) into the provided destination span.
    /// </summary>
    /// <param name="destination">
    /// A span of at least 48KB (0xC000 bytes) to receive the auxiliary RAM contents.
    /// </param>
    /// <returns>
    /// <c>true</c> if auxiliary RAM was copied successfully; <c>false</c> if auxiliary RAM
    /// is not installed (base 64KB Apple IIe configuration without 80-column card).
    /// </returns>
    /// <remarks>
    /// <para>
    /// <strong>Usage:</strong> Called by the video snapshot system to capture auxiliary RAM for
    /// 80-column text rendering and double hi-res graphics. If auxiliary RAM is not present,
    /// the destination span is unchanged and the method returns false.
    /// </para>
    /// <para>
    /// <strong>Apple IIe Configurations:</strong>
    /// <list type="bullet">
    /// <item><strong>Base 64KB:</strong> No auxiliary RAM, method returns false</item>
    /// <item><strong>Extended 128KB:</strong> Auxiliary RAM present, method returns true</item>
    /// </list>
    /// </para>
    /// </remarks>
    public bool CopyAuxMemoryIntoSpan(Span<byte> destination);
}
