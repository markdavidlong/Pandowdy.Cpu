// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Pandowdy.UI.Interfaces;

namespace Pandowdy.UI.Services;

/// <summary>
/// Provides file dialog services for disk image selection and export operations.
/// </summary>
/// <remarks>
/// <para>
/// This service wraps Avalonia's StorageProvider API to provide consistent
/// file picker dialogs with appropriate filters for disk image formats.
/// </para>
/// <para>
/// This service resolves the active window's storage provider dynamically,
/// allowing it to be registered as a singleton in the DI container before
/// the main window is created.
/// </para>
/// </remarks>
public class DiskFileDialogService : IDiskFileDialogService
{
    /// <summary>
    /// Gets the current active window, or null if none is available.
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
    /// Shows an open file dialog for selecting a disk image to insert.
    /// </summary>
    /// <returns>The selected file path, or null if the user canceled.</returns>
    public async Task<string?> ShowOpenFileDialogAsync()
    {
        var window = GetMainWindow();
        if (window?.StorageProvider == null)
        {
            return null; // No window available to show dialog
        }

        var options = new FilePickerOpenOptions
        {
            Title = "Insert Disk Image",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Supported Disk Images")
                {
                    Patterns = ["*.dsk", "*.do", "*.po", "*.nib", "*.woz", "*.2mg"]
                },
                new FilePickerFileType("DSK Images")
                {
                    Patterns = ["*.dsk"]
                },
                new FilePickerFileType("DOS Order")
                {
                    Patterns = ["*.do"]
                },
                new FilePickerFileType("ProDOS Order")
                {
                    Patterns = ["*.po"]
                },
                new FilePickerFileType("NIB Images")
                {
                    Patterns = ["*.nib"]
                },
                new FilePickerFileType("WOZ Images")
                {
                    Patterns = ["*.woz"]
                },
                new FilePickerFileType("2IMG Images")
                {
                    Patterns = ["*.2mg"]
                },
                new FilePickerFileType("All Files")
                {
                    Patterns = ["*.*"]
                }
            ]
        };

        var result = await window.StorageProvider.OpenFilePickerAsync(options);
        return result.Count > 0 ? result[0].Path.LocalPath : null;
    }

    /// <summary>
    /// Shows a save file dialog for saving/exporting a disk image.
    /// </summary>
    /// <param name="suggestedFileName">Optional suggested filename (without path), or null for no suggestion.</param>
    /// <returns>The selected file path, or null if the user canceled.</returns>
    public async Task<string?> ShowSaveFileDialogAsync(string? suggestedFileName = null)
    {
        var window = GetMainWindow();
        if (window?.StorageProvider == null)
        {
            return null; // No window available to show dialog
        }

        var options = new FilePickerSaveOptions
        {
            Title = "Save Disk Image As",
            SuggestedFileName = suggestedFileName,
            FileTypeChoices =
            [
                new FilePickerFileType("NIB Images")
                {
                    Patterns = ["*.nib"]
                },
                new FilePickerFileType("WOZ Images")
                {
                    Patterns = ["*.woz"]
                },
                new FilePickerFileType("DSK Images")
                {
                    Patterns = ["*.dsk"]
                },
                new FilePickerFileType("DOS Order")
                {
                    Patterns = ["*.do"]
                },
                new FilePickerFileType("ProDOS Order")
                {
                    Patterns = ["*.po"]
                },
                new FilePickerFileType("All Files")
                {
                    Patterns = ["*.*"]
                }
            ]
        };

        var result = await window.StorageProvider.SaveFilePickerAsync(options);
        return result?.Path.LocalPath;
    }

    /// <summary>
    /// Checks if a file path has a supported disk image extension.
    /// </summary>
    /// <param name="filePath">The file path to check.</param>
    /// <returns>True if the file extension is supported, false otherwise.</returns>
    public static bool IsSupportedDiskImage(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        var extension = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
        return extension is ".dsk" or ".do" or ".po" or ".nib" or ".woz" or ".2mg";
    }
}
