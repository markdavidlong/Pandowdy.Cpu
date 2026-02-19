// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.Project.Interfaces;

/// <summary>
/// Manages the lifecycle of .skillet project files (create, open, close).
/// </summary>
public interface ISkilletProjectManager
{
    /// <summary>
    /// Gets the currently open project, or null if no project is open.
    /// </summary>
    ISkilletProject? CurrentProject { get; }

    /// <summary>
    /// Creates a new .skillet project file at the specified path.
    /// </summary>
    Task<ISkilletProject> CreateAsync(string filePath, string projectName);

    /// <summary>
    /// Opens an existing .skillet project file.
    /// </summary>
    Task<ISkilletProject> OpenAsync(string filePath);

    /// <summary>
    /// Saves the current project to a new file path.
    /// </summary>
    /// <param name="filePath">The new file path for the project.</param>
    /// <remarks>
    /// This method is used both for persisting ad hoc projects to disk for the first time
    /// and for creating a copy of an existing project. Updates CurrentProject after completion.
    /// </remarks>
    Task SaveAsAsync(string filePath);

    /// <summary>
    /// Closes the currently open project.
    /// </summary>
    Task CloseAsync();
}
