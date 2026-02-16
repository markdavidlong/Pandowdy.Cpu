// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.Project.Interfaces;

namespace Pandowdy.Project.Services;

/// <summary>
/// Stub implementation of ISettingsResolver for Phase 1.
/// Full implementation deferred to Phase 3.
/// </summary>
public sealed class SettingsResolver : ISettingsResolver
{
    public T Resolve<T>(string key, T hardcodedDefault)
    {
        // Phase 1: Always return hardcoded default
        return hardcodedDefault;
    }

    public SettingsLayer GetPersistLayer(string key)
    {
        // Phase 1: Always return Json layer
        return SettingsLayer.Json;
    }
}
