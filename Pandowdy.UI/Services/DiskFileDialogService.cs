// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;

namespace Pandowdy.UI.Services;

/// <summary>
/// Provides file dialog services for disk image selection and export operations.
/// </summary>
/// <remarks>
/// This service wraps Avalonia's StorageProvider API to provide consistent
/// file picker dialogs with appropriate filters for disk image formats.
/// </remarks>
public class DiskFileDialogService
{
    /// <summary>
    /// Opens a file picker dialog for selecting a disk image to insert.
    /// </summary>
    /// <param name="storageProvider">The storage provider from the active window.</param>
    /// <returns>The selected file path, or null if the user canceled.</returns>
    public static async Task<string?> PickDiskImageForInsertAsync(IStorageProvider storageProvider)
    {
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

        var result = await storageProvider.OpenFilePickerAsync(options);
        return result.Count > 0 ? result[0].Path.LocalPath : null;
    }

    /// <summary>
    /// Opens a file picker dialog for saving/exporting a disk image.
    /// </summary>
    /// <param name="storageProvider">The storage provider from the active window.</param>
    /// <param name="suggestedFileName">Optional suggested filename (without path).</param>
    /// <returns>The selected file path, or null if the user canceled.</returns>
    public static async Task<string?> PickDiskImageForSaveAsync(IStorageProvider storageProvider, string? suggestedFileName = null)
    {
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

        var result = await storageProvider.SaveFilePickerAsync(options);
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
