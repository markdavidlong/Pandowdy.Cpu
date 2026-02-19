using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Pandowdy.UI.Interfaces;
using System.Threading.Tasks;

namespace Pandowdy.UI.Services;

/// <summary>
/// Service for displaying file dialogs to open or save Skillet project files.
/// </summary>
public class ProjectFileDialogService : IProjectFileDialogService
{
    /// <summary>
    /// Shows a file dialog to open an existing Skillet project file.
    /// </summary>
    /// <returns>The selected file path, or null if the user cancelled.</returns>
    public async Task<string?> ShowOpenProjectDialogAsync()
    {
        var window = GetMainWindow();
        if (window is null)
        {
            return null;
        }

        var options = new FilePickerOpenOptions
        {
            Title = "Open Skillet Project",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Skillet Projects")
                {
                    Patterns = new[] { "*.skillet" }
                },
                new FilePickerFileType("All Files")
                {
                    Patterns = new[] { "*.*" }
                }
            }
        };

        var result = await window.StorageProvider.OpenFilePickerAsync(options);

        if (result.Count > 0)
        {
            return result[0].Path.LocalPath;
        }

        return null;
    }

    /// <summary>
    /// Shows a file dialog to save a Skillet project file.
    /// </summary>
    /// <param name="suggestedFileName">Optional suggested filename (without extension).</param>
    /// <returns>The selected file path, or null if the user cancelled.</returns>
    public async Task<string?> ShowSaveProjectDialogAsync(string? suggestedFileName = null)
    {
        var window = GetMainWindow();
        if (window is null)
        {
            return null;
        }

        var options = new FilePickerSaveOptions
        {
            Title = "Create New Skillet Project",
            SuggestedFileName = suggestedFileName,
            DefaultExtension = "skillet",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Skillet Projects")
                {
                    Patterns = new[] { "*.skillet" }
                }
            }
        };

        var result = await window.StorageProvider.SaveFilePickerAsync(options);

        return result?.Path.LocalPath;
    }

    /// <summary>
    /// Gets the main application window for displaying dialogs.
    /// </summary>
    private static Window? GetMainWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow;
        }

        return null;
    }
}
