// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System.Threading.Tasks;

namespace Pandowdy.UI.Interfaces;

/// <summary>
/// Service for displaying file dialogs for Skillet project operations.
/// </summary>
/// <remarks>
/// <para>
/// This service abstracts file picker dialogs for .skillet project file operations,
/// providing a testable interface for project file dialog operations.
/// </para>
/// </remarks>
public interface IProjectFileDialogService
{
    /// <summary>
    /// Shows an open file dialog for selecting a .skillet project file to open.
    /// </summary>
    /// <returns>The selected file path, or null if the user canceled.</returns>
    Task<string?> ShowOpenProjectDialogAsync();

    /// <summary>
    /// Shows a save file dialog for creating a new .skillet project file.
    /// </summary>
    /// <param name="suggestedFileName">Suggested filename for the save dialog, or null for no suggestion.</param>
    /// <returns>The selected file path, or null if the user canceled.</returns>
    Task<string?> ShowSaveProjectDialogAsync(string? suggestedFileName = null);
}
