// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Pandowdy.Project.Interfaces;
using Pandowdy.Project.Models;
using Pandowdy.UI.Controls;
using Pandowdy.UI.Interfaces;
using Pandowdy.UI.ViewModels;

namespace Pandowdy.UI.Services;

/// <summary>
/// Avalonia implementation of message box service for displaying error and confirmation dialogs.
/// </summary>
/// <remarks>
/// <para>
/// This service resolves the active window dynamically when showing dialogs,
/// allowing it to be registered as a singleton in the DI container before
/// the main window is created.
/// </para>
/// </remarks>
public class MessageBoxService(ISkilletProjectManager projectManager) : IMessageBoxService
{
    private readonly ISkilletProjectManager _projectManager = projectManager;

    /// <summary>
    /// Gets the current active main window, or null if none is available.
    /// </summary> 
    private static Window? GetMainWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow ?? (desktop.Windows.Count > 0 ? desktop.Windows[0] : null);
        }
        return null;
    }

    /// <summary>
    /// Displays an error message dialog.
    /// </summary>
    /// <param name="title">The title of the error dialog.</param>
    /// <param name="message">The error message to display.</param>
    /// <returns>A task that completes when the user dismisses the dialog.</returns>
    public async Task ShowErrorAsync(string title, string message)
    {
        var ownerWindow = GetMainWindow();
        if (ownerWindow == null)
        {
            return; // No window available to show dialog
        }

        var dialog = new Window
        {
            Title = title,
            Width = 400,
            Height = 200,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(20),
                Spacing = 20,
                Children =
                {
                    new TextBlock
                    {
                        Text = message,
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap
                    },
                    new Button
                    {
                        Content = "OK",
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                        Width = 80,
                        Command = ReactiveUI.ReactiveCommand.Create(() => { })
                    }
                }
            }
        };

        // Wire up the button to close the dialog
        if (dialog.Content is StackPanel panel && panel.Children[1] is Button okButton)
        {
            okButton.Click += (_, _) => dialog.Close();
        }

        await dialog.ShowDialog(ownerWindow);
    }

    /// <summary>
    /// Displays a confirmation dialog with Yes/No buttons.
    /// </summary>
    /// <param name="title">The title of the confirmation dialog.</param>
    /// <param name="message">The confirmation message to display.</param>
    /// <returns>True if user clicked Yes, false if No.</returns>
    public async Task<bool> ShowConfirmationAsync(string title, string message)
    {
        var ownerWindow = GetMainWindow();
        if (ownerWindow == null)
        {
            return false; // No window available, default to No
        }

        bool result = false;

        var dialog = new Window
        {
            Title = title,
            Width = 400,
            Height = 200,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(20),
                Spacing = 20,
                Children =
                {
                    new TextBlock
                    {
                        Text = message,
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap
                    },
                    new StackPanel
                    {
                        Orientation = Avalonia.Layout.Orientation.Horizontal,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                        Spacing = 10,
                        Children =
                        {
                            new Button
                            {
                                Content = "Yes",
                                Width = 80
                            },
                            new Button
                            {
                                Content = "No",
                                Width = 80
                            }
                        }
                    }
                }
            }
        };

        // Wire up button events
        if (dialog.Content is StackPanel panel && 
            panel.Children[1] is StackPanel buttonPanel)
        {
            if (buttonPanel.Children[0] is Button yesButton)
            {
                yesButton.Click += (_, _) =>
                {
                    result = true;
                    dialog.Close();
                };
            }

            if (buttonPanel.Children[1] is Button noButton)
            {
                noButton.Click += (_, _) =>
                {
                    result = false;
                    dialog.Close();
                };
            }
        }

        await dialog.ShowDialog(ownerWindow);
        return result;
    }

    /// <summary>
    /// Shows the Mount from Library dialog and returns the selected disk image.
    /// </summary>
    /// <returns>
    /// The selected <see cref="DiskImageRecord"/> if the user clicked Mount,
    /// or null if the user clicked Cancel.
    /// </returns>
    public async Task<DiskImageRecord?> ShowMountFromLibraryDialogAsync()
    {
        var ownerWindow = GetMainWindow();
        if (ownerWindow == null)
        {
            return null; // No window available
        }

        var viewModel = new MountFromLibraryDialogViewModel(_projectManager);
        var dialog = new MountFromLibraryDialog(viewModel);

        await dialog.ShowDialog(ownerWindow);

        // Return the selected disk image if user clicked Mount
        return dialog.DialogResult == true ? viewModel.SelectedDiskImage : null;
    }
}
