// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.EmuCore.Video;

/// <summary>
/// Defines a renderer capable of converting Apple IIe video memory into a displayable bitmap.
/// </summary>
/// <remarks>
/// This interface abstracts the process of rendering Apple IIe video modes (text mode,
/// lo-res graphics, hi-res graphics, and mixed modes) into a bitmap format suitable for
/// display. Implementations read directly from the video memory pages, interpret the
/// video mode soft switches, and produce a pixel-accurate representation of what would
/// appear on an Apple IIe monitor.
/// <para>
/// The Apple IIe supports several video modes:
/// <list type="bullet">
/// <item>Text Mode: 40-column or 80-column text with 24 rows</item>
/// <item>Lo-Res Graphics: 40x48 color blocks (16 colors)</item>
/// <item>Hi-Res Graphics: 280x192 pixels (6 colors plus black and white)</item>
/// <item>Mixed Mode: Graphics on top 20 rows, text on bottom 4 rows</item>
/// </list>
/// Each mode may use page 1 ($0400/$2000) or page 2 ($0800/$4000) video memory.
/// </para>
/// </remarks>
public interface IDisplayBitmapRenderer
{
    /// <summary>
    /// Renders the current Apple IIe video state into the provided frame buffer.
    /// </summary>
    /// <param name="context">A <see cref="RenderContext"/> containing the frame buffer
    /// to render into, direct memory access for reading video RAM, and system status
    /// information including video mode soft switches.</param>
    /// <remarks>
    /// This method examines the video mode flags in the context (text/graphics, mixed mode,
    /// hi-res/lo-res, page selection) and renders the appropriate video content:
    /// <list type="bullet">
    /// <item>Reads from the correct video memory page (page 1 or page 2) based on soft switches</item>
    /// <item>Interprets memory contents according to the active video mode</item>
    /// <item>Handles character ROM lookups for text mode, including flashing characters and MouseText</item>
    /// <item>Applies Apple IIe color generation rules for graphics modes</item>
    /// <item>Renders mixed mode by combining graphics and text regions</item>
    /// <item>Outputs pixels to the frame buffer in a format ready for display</item>
    /// </list>
    /// <para>
    /// This method should be called once per video frame (typically 60 times per second)
    /// to keep the display synchronized with video memory changes. The rendering process
    /// directly reads memory through <see cref="IDirectMemoryPoolReader"/> to access both
    /// main and auxiliary video pages without going through the CPU bus.
    /// </para>
    /// </remarks>
    void Render(RenderContext context);
}
