// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.Project.Interfaces;

/// <summary>
/// Resolves settings using four-layer resolution: hardcoded → JSON → skillet → runtime.
/// </summary>
public interface ISettingsResolver
{
    /// <summary>
    /// Resolves a setting value using the four-layer resolution order.
    /// </summary>
    T Resolve<T>(string key, T hardcodedDefault);

    /// <summary>
    /// Determines which layer a setting should persist to.
    /// </summary>
    SettingsLayer GetPersistLayer(string key);
}

/// <summary>
/// Identifies which settings layer a value belongs to.
/// </summary>
public enum SettingsLayer
{
    Hardcoded,   // Never persisted
    Json,        // Global workstation preference
    Skillet      // Project-specific override
}
