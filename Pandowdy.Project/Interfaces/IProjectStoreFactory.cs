// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.Project.Interfaces;

/// <summary>
/// Creates path-bound <see cref="IProjectStore"/> instances.
/// Registered in DI; the concrete implementation determines the storage format.
/// </summary>
public interface IProjectStoreFactory
{
    /// <summary>
    /// Opens an existing store at <paramref name="path"/>.
    /// Throws if the store doesn't exist or is invalid.
    /// </summary>
    IProjectStore Open(string path);

    /// <summary>
    /// Creates a new, empty store at <paramref name="path"/>.
    /// Throws if a store already exists at that location.
    /// </summary>
    IProjectStore Create(string path);
}
