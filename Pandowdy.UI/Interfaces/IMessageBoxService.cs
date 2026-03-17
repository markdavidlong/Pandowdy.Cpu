// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System.Threading.Tasks;
using Pandowdy.Project.Models;

namespace Pandowdy.UI.Interfaces;

/// <summary>
/// Service for displaying message boxes and dialogs to the user.
/// </summary>
public interface IMessageBoxService
{
    /// <summary>
    /// Displays an error message dialog.
    /// </summary>
    /// <param name="title">The title of the error dialog.</param>
    /// <param name="message">The error message to display.</param>
    /// <returns>A task that completes when the user dismisses the dialog.</returns>
    Task ShowErrorAsync(string title, string message);

    /// <summary>
    /// Displays a confirmation dialog with Yes/No buttons.
    /// </summary>
    /// <param name="title">The title of the confirmation dialog.</param>
    /// <param name="message">The confirmation message to display.</param>
    /// <returns>True if user clicked Yes, false if No.</returns>
    Task<bool> ShowConfirmationAsync(string title, string message);

    /// <summary>
    /// Shows the Mount from Library dialog and returns the selected disk image.
    /// </summary>
    /// <returns>
    /// The selected <see cref="DiskImageRecord"/> if the user clicked Mount,
    /// or null if the user clicked Cancel.
    /// </returns>
    Task<DiskImageRecord?> ShowMountFromLibraryDialogAsync();

    /// <summary>
    /// Displays a three-choice save prompt for unsaved disk data.
    /// </summary>
    /// <param name="title">The title of the dialog.</param>
    /// <param name="message">Descriptive message about the unsaved changes.</param>
    /// <param name="saveLabel">Label for the save button (e.g. "Save Disk Data").</param>
    /// <param name="dontSaveLabel">Label for the don't-save button (e.g. "Eject Without Saving").</param>
    /// <returns>
    /// <see cref="SavePromptResult.Save"/> if the user chose to save,
    /// <see cref="SavePromptResult.DontSave"/> if proceeded without saving,
    /// <see cref="SavePromptResult.Cancel"/> if the operation was cancelled.
    /// </returns>
    Task<SavePromptResult> ShowSavePromptAsync(
        string title,
        string message,
        string saveLabel = "Save",
        string dontSaveLabel = "Don't Save");
}
