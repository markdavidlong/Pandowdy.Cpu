using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Pandowdy.UI.Controls;
using Pandowdy.UI.Interfaces;
using Pandowdy.UI.ViewModels;
using System.IO;
using System.Threading.Tasks;

namespace Pandowdy.UI.Services;

/// <summary>
/// Service for displaying project dialogs to open or save Skillet project directories.
/// </summary>
/// <remarks>
/// <para>
/// Save / Save As uses a custom <see cref="SaveProjectDialog"/> where the user types a
/// project name and picks a parent directory. The directory created is
/// <c>{parent}/{name}_skilletdir</c>.
/// </para>
/// <para>
/// Open uses a system folder picker so the user selects the <c>_skilletdir</c> directory.
/// </para>
/// <para>
/// When a future single-file backend (<c>.skillet</c>) replaces the directory store,
/// revert both methods to standard <c>SaveFilePickerAsync</c> / <c>OpenFilePickerAsync</c>
/// with a <c>*.skillet</c> extension filter.
/// </para>
/// </remarks>
public class ProjectFileDialogService : IProjectFileDialogService
{
    /// <summary>
    /// Shows a folder picker so the user can select an existing Skillet project directory.
    /// </summary>
    /// <returns>The selected directory path, or null if the user cancelled.</returns>
    public async Task<string?> ShowOpenProjectDialogAsync()
    {
        var window = GetMainWindow();
        if (window is null)
        {
            return null;
        }

        var options = new FolderPickerOpenOptions
        {
            Title = "Open Skillet Project",
            AllowMultiple = false
        };

        var result = await window.StorageProvider.OpenFolderPickerAsync(options);
        return result.Count > 0 ? result[0].Path.LocalPath : null;
    }

    /// <summary>
    /// Shows a custom Save Project dialog where the user types a project name and
    /// picks the parent directory via a Browse button.
    /// </summary>
    /// <param name="suggestedFileName">
    /// The initial project name to pre-fill (without the <c>_skilletdir</c> suffix).
    /// Defaults to "untitled" when null or empty.
    /// </param>
    /// <returns>
    /// The full directory path <c>{parentDir}/{name}_skilletdir</c>,
    /// or null if the user cancelled.
    /// </returns>
    public async Task<string?> ShowSaveProjectDialogAsync(string? suggestedFileName = null)
    {
        var window = GetMainWindow();
        if (window is null)
        {
            return null;
        }

        var viewModel = new SaveProjectDialogViewModel(suggestedFileName);
        var dialog = new SaveProjectDialog(viewModel);

        await dialog.ShowDialog(window);

        return dialog.DialogResult == true ? viewModel.GetResultPath() : null;
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
