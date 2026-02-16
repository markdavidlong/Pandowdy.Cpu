// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.Project.Constants;

static class SkilletConstants
{
    public const uint ApplicationId = 0x534B494C; // "SKIL"

    public const uint SchemaVersion = 1;

    // V1 table names
    public const string TableProjectMetadata = "project_metadata";
    public const string TableDiskImages = "disk_images";
    public const string TableMountConfiguration = "mount_configuration";
    public const string TableEmulatorOverrides = "emulator_overrides";
    public const string TableDisplayOverrides = "display_overrides";
    public const string TableProjectSettings = "project_settings";
}
