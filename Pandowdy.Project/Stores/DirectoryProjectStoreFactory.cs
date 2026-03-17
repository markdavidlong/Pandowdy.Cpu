// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.Project.Interfaces;

namespace Pandowdy.Project.Stores;

/// <summary>
/// Creates <see cref="DirectoryProjectStore"/> instances for directory-based project storage.
/// </summary>
public sealed class DirectoryProjectStoreFactory : IProjectStoreFactory
{
    /// <inheritdoc/>
    /// <exception cref="DirectoryNotFoundException">
    /// Thrown if <paramref name="path"/> does not exist as a directory.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown if no <c>manifest.json</c> is found inside the directory.
    /// </exception>
    public IProjectStore Open(string path)
    {
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException(
                $"Project directory not found: '{path}'.");
        }

        var manifestPath = System.IO.Path.Combine(path, "manifest.json");

        if (!File.Exists(manifestPath))
        {
            throw new InvalidOperationException(
                $"No manifest.json found in '{path}'. " +
                "The directory does not appear to be a valid Pandowdy project.");
        }

        return new DirectoryProjectStore(path);
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">
    /// Thrown if a directory already exists at <paramref name="path"/>.
    /// </exception>
    public IProjectStore Create(string path)
    {
        if (Directory.Exists(path))
        {
            throw new InvalidOperationException(
                $"Cannot create a new project at '{path}': directory already exists.");
        }

        Directory.CreateDirectory(System.IO.Path.Combine(path, "disks"));
        Directory.CreateDirectory(System.IO.Path.Combine(path, "attachments"));

        return new DirectoryProjectStore(path);
    }
}
