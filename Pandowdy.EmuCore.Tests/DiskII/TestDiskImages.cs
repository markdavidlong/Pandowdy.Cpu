// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.EmuCore.Tests.DiskII;

/// <summary>
/// Helper class for accessing test disk image files.
/// </summary>
public static class TestDiskImages
{
    /// <summary>
    /// Gets the path to the TestImages folder.
    /// </summary>
    public static string TestImagesFolder => Path.Combine(AppContext.BaseDirectory, "TestImages");

    /// <summary>
    /// Gets the path to test.nib disk image.
    /// </summary>
    public static string TestNib => Path.Combine(TestImagesFolder, "test.nib");

    /// <summary>
    /// Gets the path to test.woz disk image.
    /// </summary>
    public static string TestWoz => Path.Combine(TestImagesFolder, "test.woz");

    /// <summary>
    /// Gets the path to blank.nib disk image.
    /// </summary>
    public static string BlankNib => Path.Combine(TestImagesFolder, "blank.nib");

    /// <summary>
    /// Gets the path to dos.dsk disk image.
    /// </summary>
    public static string DosDsk => Path.Combine(TestImagesFolder, "dos.dsk");

    /// <summary>
    /// Gets the path to dos33mst.dsk disk image.
    /// </summary>
    public static string Dos33MasterDsk => Path.Combine(TestImagesFolder, "dos33mst.dsk");

    /// <summary>
    /// Gets the path to prodos.woz disk image.
    /// </summary>
    public static string ProdosWoz => Path.Combine(TestImagesFolder, "prodos.woz");

    /// <summary>
    /// Gets the path to prodos.nib disk image.
    /// </summary>
    public static string ProdosNib => Path.Combine(TestImagesFolder, "prodos.nib");

    /// <summary>
    /// Checks if test images are available.
    /// </summary>
    public static bool TestImagesAvailable => Directory.Exists(TestImagesFolder) && File.Exists(TestNib);
}
