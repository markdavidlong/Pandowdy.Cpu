// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System;
using System.IO;
using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;

namespace Pandowdy.UI.ViewModels;

/// <summary>
/// ViewModel for the custom Save Project dialog.
/// </summary>
/// <remarks>
/// The dialog asks the user for a project name (plain text) and a parent directory.
/// The resulting directory path is <c>{ParentDirectory}/{ProjectName}_skilletdir</c>.
/// If the user types a name that already ends with <c>_skilletdir</c>, the suffix is
/// not doubled.
/// </remarks>
public sealed class SaveProjectDialogViewModel : ReactiveObject
{
    private string _projectName = string.Empty;
    private string _parentDirectory = string.Empty;
    private readonly ObservableAsPropertyHelper<string> _previewPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="SaveProjectDialogViewModel"/> class.
    /// </summary>
    /// <param name="suggestedName">
    /// The initial project name to pre-fill (without the <c>_skilletdir</c> suffix).
    /// Defaults to "untitled" when null or empty.
    /// </param>
    /// <param name="suggestedParentDirectory">
    /// An optional initial parent directory. When null the field starts empty.
    /// </param>
    public SaveProjectDialogViewModel(string? suggestedName = null, string? suggestedParentDirectory = null)
    {
        _projectName = string.IsNullOrWhiteSpace(suggestedName) ? "untitled" : suggestedName;
        _parentDirectory = suggestedParentDirectory ?? string.Empty;

        // Enable Save only when both fields are filled in.
        var canSave = this.WhenAnyValue(
                x => x.ProjectName,
                x => x.ParentDirectory,
                (name, dir) => !string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(dir));

        SaveCommand = ReactiveCommand.Create(
            () => { DialogResult = true; },
            canSave);

        CancelCommand = ReactiveCommand.Create(
            () => { DialogResult = false; });

        // Build a live preview of the result path whenever either field changes.
        _previewPath = this.WhenAnyValue(x => x.ProjectName, x => x.ParentDirectory)
            .Select(t => BuildPreviewPath(t.Item1, t.Item2))
            .ToProperty(this, x => x.PreviewPath, string.Empty);
    }

    /// <summary>Gets or sets the project name typed by the user.</summary>
    public string ProjectName
    {
        get => _projectName;
        set => this.RaiseAndSetIfChanged(ref _projectName, value);
    }

    /// <summary>Gets or sets the parent directory chosen via the Browse button.</summary>
    public string ParentDirectory
    {
        get => _parentDirectory;
        set => this.RaiseAndSetIfChanged(ref _parentDirectory, value);
    }

    /// <summary>
    /// Gets a live preview of the resulting project directory path.
    /// </summary>
    public string PreviewPath => _previewPath.Value;

    /// <summary>Gets the command to accept and close the dialog.</summary>
    public ReactiveCommand<Unit, Unit> SaveCommand { get; }

    /// <summary>Gets the command to cancel and close the dialog.</summary>
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    /// <summary>Gets a value indicating whether Save was clicked (true) or Cancel / closed (false).</summary>
    public bool DialogResult { get; private set; }

    /// <summary>
    /// Computes the full directory path that will be created.
    /// Applies the <c>_skilletdir</c> suffix rule.
    /// </summary>
    /// <returns>The full path, or null when either input is empty.</returns>
    public string? GetResultPath()
    {
        if (string.IsNullOrWhiteSpace(ProjectName) || string.IsNullOrWhiteSpace(ParentDirectory))
        {
            return null;
        }

        return Path.Combine(ParentDirectory, ApplySkilletDirSuffix(ProjectName.Trim()));
    }

    /// <summary>
    /// Appends <c>_skilletdir</c> unless the name already ends with it.
    /// </summary>
    internal static string ApplySkilletDirSuffix(string name)
    {
        const string suffix = "_skilletdir";
        return name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
            ? name
            : name + suffix;
    }

    /// <summary>Builds the preview string shown in the dialog.</summary>
    private static string BuildPreviewPath(string name, string dir)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(dir))
        {
            return string.Empty;
        }

        return Path.Combine(dir, ApplySkilletDirSuffix(name.Trim()));
    }
}
