// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.EmuCore.Tests;

/// <summary>
/// Test constants and placeholder values used across test projects.
/// </summary>
/// <remarks>
/// <para>
/// <strong>IMPORTANT:</strong> All file paths defined in this class are
/// <strong>placeholder strings only</strong> - they are NOT references to actual files
/// that need to exist on disk. These paths are used purely as test data to verify
/// that metadata (file paths, names, formats) are handled correctly by the emulator.
/// </para>
/// <para>
/// <strong>No File I/O is performed</strong> - Tests using these constants create
/// disk images entirely in memory and verify that path/metadata properties are
/// preserved correctly during operations like swapping, saving, or exporting.
/// </para>
/// </remarks>
public static class TestConstants
{
    /// <summary>
    /// Placeholder disk image paths for testing metadata handling.
    /// </summary>
    /// <remarks>
    /// These are test data strings - NOT actual files that need to exist.
    /// </remarks>
    public static class DiskImagePaths
    {
        // WOZ format test paths
        public const string TestDisk1Woz = "TestData/disk1.woz";
        public const string TestDisk1WozNew = "TestData/disk1_new.woz";
        public const string GameWoz = "TestData/game.woz";
        public const string GameWozNew = "TestData/game_new.woz";
        public const string OriginalWoz = "TestData/original.woz";
        public const string ModifiedWoz = "TestData/modified.woz";

        // DSK format test paths
        public const string TestDisk2Dsk = "TestData/disk2.dsk";
        public const string TestDisk2DskNew = "TestData/disk2_new.dsk";

        // Filenames only (for UI display tests)
        public const string Disk1WozFilename = "disk1.woz";
        public const string Disk2DskFilename = "disk2.dsk";
        public const string GameWozFilename = "game.woz";
    }

    /// <summary>
    /// Standard disk image parameters.
    /// </summary>
    public static class DiskParameters
    {
        /// <summary>Standard track count for Apple II 5.25" disks.</summary>
        public const int StandardTrackCount = 35;

        /// <summary>Standard bit count per track for NIB/synthesized formats.</summary>
        public const int StandardTrackBitCount = 51200;

        /// <summary>Maximum quarter-track position (Track 34.75 = quarter-track 139).</summary>
        public const int MaxQuarterTracks = 139;
    }

    /// <summary>
    /// Test drive names.
    /// </summary>
    public static class DriveNames
    {
        public const string Drive1 = "Drive1";
        public const string Drive2 = "Drive2";
        public const string Slot6Drive1 = "Slot6-D1";
        public const string Slot6Drive2 = "Slot6-D2";
    }
}
