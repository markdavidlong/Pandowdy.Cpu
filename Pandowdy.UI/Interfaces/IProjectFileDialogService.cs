// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System.Threading.Tasks;

namespace Pandowdy.UI.Interfaces;

/// <summary>
/// Service for displaying project dialogs for Skillet project operations.
/// </summary>
/// <remarks>
/// <para>
/// Save / Save As shows a custom dialog where the user types a project name and
/// chooses a parent directory. The resulting path is <c>{parent}/{name}_skilletdir</c>.
/// Open shows a folder picker for selecting an existing <c>_skilletdir</c> directory.
/// </para>
/// <para>
/// When a future single-file backend replaces the directory store, this interface
/// should revert to file picker semantics with a <c>.skillet</c> extension filter.
/// </para>
/// </remarks>
public interface IProjectFileDialogService
{
    /// <summary>
    /// Shows a folder picker for selecting an existing Skillet project directory to open.
    /// </summary>
    /// <returns>The selected directory path, or null if the user canceled.</returns>
    Task<string?> ShowOpenProjectDialogAsync();

    /// <summary>
    /// Shows a custom Save Project dialog where the user types a project name and
    /// picks a parent directory.
    /// The returned path is <c>{parentFolder}/{typedName}_skilletdir</c>.
    /// If the user types a name that already ends with <c>_skilletdir</c> the suffix is not doubled.
    /// </summary>
    /// <param name="suggestedFileName">
    /// Pre-filled project name (without the <c>_skilletdir</c> suffix), or null / empty for "untitled".
    /// </param>
    /// <returns>The full directory path for the new project, or null if the user canceled.</returns>
    Task<string?> ShowSaveProjectDialogAsync(string? suggestedFileName = null);
}
