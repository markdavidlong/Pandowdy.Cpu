// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

#pragma warning disable CS0618 // Type or member is obsolete - Avalonia API transition

using System.Linq;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Pandowdy.UI.Services;
using Pandowdy.UI.ViewModels;

namespace Pandowdy.UI.Controls;

/// <summary>
/// User control for displaying disk drive status and accepting disk image files via drag-and-drop.
/// </summary>
public partial class DiskStatusWidget : UserControl
{
    private IBrush? _originalBorderBrush;

    public DiskStatusWidget()
    {
        InitializeComponent();

        // Wire up drag-and-drop events
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);

        // Wire up double-click event to load disk
        DoubleTapped += OnDoubleTapped;
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        // Check if the dragged data contains files
        if (e.Data.Contains(DataFormats.Files))
        {
            var files = e.Data.GetFiles()?.ToArray();
            if (files != null && files.Length == 1)
            {
                var filePath = files[0].Path.LocalPath;

                // Check if it's a supported disk image
                if (DiskFileDialogService.IsSupportedDiskImage(filePath))
                {
                    e.DragEffects = DragDropEffects.Copy;

                    // Visual feedback: green glow on border
                    if (_originalBorderBrush == null)
                    {
                        _originalBorderBrush = RootBorder.BorderBrush;
                        RootBorder.BorderBrush = Brushes.LimeGreen;
                        RootBorder.BorderThickness = new Avalonia.Thickness(3);
                    }

                    e.Handled = true;
                    return;
                }
            }
        }

        // Not a valid disk image
        e.DragEffects = DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        // Restore original border
        RestoreBorder();

        // Get the file path
        if (e.Data.Contains(DataFormats.Files))
        {
            var files = e.Data.GetFiles()?.ToArray();
            if (files != null && files.Length == 1 && DataContext is DiskStatusWidgetViewModel vm)
            {
                var filePath = files[0].Path.LocalPath;

                if (DiskFileDialogService.IsSupportedDiskImage(filePath))
                {
                    // Send the insert disk command with the file path
                    await vm.InsertDiskAsync(filePath);
                    e.Handled = true;
                }
            }
        }
    }

    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        // Restore original border when drag leaves
        RestoreBorder();
    }

    private async void OnDoubleTapped(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        // Double-click triggers Insert Disk command
        if (DataContext is DiskStatusWidgetViewModel vm)
        {
            // Execute the command asynchronously
            try
            {
                await vm.InsertDiskCommand.Execute().GetAwaiter();
            }
            catch
            {
                // Errors are already handled within the command
            }
            e.Handled = true;
        }
    }

    private void RestoreBorder()
    {
        if (_originalBorderBrush != null)
        {
            RootBorder.BorderBrush = _originalBorderBrush;
            RootBorder.BorderThickness = new Avalonia.Thickness(2);
            _originalBorderBrush = null;
        }
    }
}
