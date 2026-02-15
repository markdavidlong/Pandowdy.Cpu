// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System.Threading.Tasks;

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
}
