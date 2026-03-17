// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.Project.Models;

/// <summary>
/// Identifies which settings dictionary to read from or write to.
/// Replaces the string-based table names formerly in <c>SkilletConstants</c>.
/// </summary>
public enum SettingsScope
{
    /// <summary>Machine-level emulator overrides (CPU speed, memory model, etc.).</summary>
    EmulatorOverrides,

    /// <summary>Display-level overrides (color mode, scan-line effects, etc.).</summary>
    DisplayOverrides,

    /// <summary>Project-wide settings (DefaultSavePolicy, etc.).</summary>
    ProjectSettings
}
