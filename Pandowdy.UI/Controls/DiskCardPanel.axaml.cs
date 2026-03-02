// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace Pandowdy.UI.Controls;

/// <summary>
/// Code-behind for DiskCardPanel control.
/// </summary>
/// <remarks>
/// Displays a disk controller card with its header (slot + card name) and
/// contains 1-2 DiskStatusWidget children representing individual drives.
/// The settings button (⚙) opens a context menu for card-level operations.
/// </remarks>
public partial class DiskCardPanel : UserControl
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DiskCardPanel"/> class.
    /// </summary>
    public DiskCardPanel()
    {
        InitializeComponent();  
        AvaloniaXamlLoader.Load(this);

        // Find the menu button and wire up click handler
        var menuButton = this.FindControl<Button>("CardMenuButton");
        if (menuButton != null)
        {
            menuButton.Click += OnCardMenuButtonClick;
        }
    }

    private void OnCardMenuButtonClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { ContextMenu: not null } button)
        {
            button.ContextMenu.Open(button);
        }
    }
}
