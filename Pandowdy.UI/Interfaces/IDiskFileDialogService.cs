// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System.Threading.Tasks;

namespace Pandowdy.UI.Interfaces;

/// <summary>
/// Service for displaying file dialogs for disk image operations.
/// </summary>
/// <remarks>
/// <para>
/// This service abstracts file picker dialogs for disk image selection and export,
/// providing a testable interface for file dialog operations.
/// </para>
/// </remarks>
public interface IDiskFileDialogService
{
    /// <summary>
    /// Shows an open file dialog for selecting a disk image to insert.
    /// </summary>
    /// <returns>The selected file path, or null if the user canceled.</returns>
    Task<string?> ShowOpenFileDialogAsync();

    /// <summary>
    /// Shows a save file dialog for saving/exporting a disk image.
    /// </summary>
    /// <param name="suggestedFileName">Suggested filename for the save dialog, or null for no suggestion.</param>
    /// <returns>The selected file path, or null if the user canceled.</returns>
    Task<string?> ShowSaveFileDialogAsync(string? suggestedFileName = null);
}
