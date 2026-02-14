// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

#pragma warning disable CS0618 // Type or member is obsolete - Avalonia API transition

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Pandowdy.UI.Services;
using Pandowdy.UI.ViewModels;
using System.Linq;

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

        // Set storage provider when attached to visual tree
        AttachedToVisualTree += OnAttachedToVisualTree;
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        // Find the parent window and set the storage provider on the ViewModel
        var window = this.FindLogicalAncestorOfType<Window>();
        if (window != null && DataContext is DiskStatusWidgetViewModel vm)
        {
            vm.SetStorageProvider(window.StorageProvider);
        }
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
