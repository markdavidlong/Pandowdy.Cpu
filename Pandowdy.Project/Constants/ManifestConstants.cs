// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.Project.Constants;

public static class ManifestConstants
{
    /// <summary>
    /// Current schema version. LoadManifest must validate
    /// Metadata.SchemaVersion against this and throw if unsupported.
    /// </summary>
    public const int CurrentSchemaVersion = 1;

    public const string Disclaimer =
        "This project file may contain third-party data, which is protected by the "
        + "copyrights of the original rights holder. The author(s) of Pandowdy and all "
        + "associated parties do not assert any claim of ownership or assume any license "
        + "or rights to third-party data contained within this project file.";
}
