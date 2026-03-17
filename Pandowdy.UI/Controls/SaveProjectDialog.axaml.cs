// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Pandowdy.UI.ViewModels;

namespace Pandowdy.UI.Controls;

/// <summary>
/// Custom dialog for saving a Skillet project.  The user types a project name
/// and picks a parent directory; the resulting path is
/// <c>{parent}/{name}_skilletdir</c>.
/// </summary>
public partial class SaveProjectDialog : Window
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SaveProjectDialog"/> class.
    /// Required by the AXAML designer.
    /// </summary>
    public SaveProjectDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SaveProjectDialog"/> class
    /// with the specified ViewModel.
    /// </summary>
    /// <param name="viewModel">The ViewModel that drives the dialog.</param>
    public SaveProjectDialog(SaveProjectDialogViewModel viewModel) : this()
    {
        DataContext = viewModel;

        viewModel.SaveCommand.Subscribe(_ => Close(true));
        viewModel.CancelCommand.Subscribe(_ => Close(false));
    }

    /// <summary>Gets the dialog result (true = Save, false = Cancel / closed).</summary>
    public bool? DialogResult { get; private set; }

    /// <summary>Closes the dialog with the specified result.</summary>
    private void Close(bool result)
    {
        DialogResult = result;
        Close();
    }

    /// <summary>
    /// Handles the Browse button click — shows a folder picker for the parent directory.
    /// </summary>
    private async void OnBrowseClick(object? sender, RoutedEventArgs e)
    {
        var options = new FolderPickerOpenOptions
        {
            Title = "Choose Parent Folder for Project",
            AllowMultiple = false
        };

        var result = await StorageProvider.OpenFolderPickerAsync(options);
        if (result.Count > 0 && DataContext is SaveProjectDialogViewModel vm)
        {
            vm.ParentDirectory = result[0].Path.LocalPath;
        }
    }
}
