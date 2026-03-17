// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System;
using Avalonia.Controls;
using Pandowdy.UI.ViewModels;

namespace Pandowdy.UI.Controls;

/// <summary>
/// Dialog window for mounting a disk image from the project library.
/// </summary>
public partial class MountFromLibraryDialog : Window
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MountFromLibraryDialog"/> class.
    /// </summary>
    public MountFromLibraryDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MountFromLibraryDialog"/> class
    /// with the specified ViewModel.
    /// </summary>
    /// <param name="viewModel">The ViewModel for the dialog.</param>
    public MountFromLibraryDialog(MountFromLibraryDialogViewModel viewModel) : this()
    {
        DataContext = viewModel;

        // Wire up command handlers to close the window
        viewModel.SelectDiskCommand.Subscribe(_ => Close(true));
        viewModel.CancelCommand.Subscribe(_ => Close(false));
    }

    /// <summary>
    /// Gets the dialog result (true if Mount was clicked, false if Cancel was clicked).
    /// </summary>
    public bool? DialogResult { get; private set; }

    /// <summary>
    /// Closes the dialog with the specified result.
    /// </summary>
    /// <param name="result">The dialog result.</param>
    private void Close(bool result)
    {
        DialogResult = result;
        Close();
    }
}
