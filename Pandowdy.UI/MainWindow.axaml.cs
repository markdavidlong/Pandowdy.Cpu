using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using CommonUtil;
using DiskArc;
using System;
using System.IO;
using static DiskArc.Defs;

namespace pandowdy;

public partial class MainWindow : Window
{
    private readonly AppHook mAppHook = new AppHook(new SimpleMessageLog());
    private DiskReadTestTemp? mDiskReadTest;
    private string mLastDiskPath = "E:\\develop\\Pandowdy";

    public MainWindow()
    {
        InitializeComponent();
        mDiskReadTest = new DiskReadTestTemp(mAppHook, AppendText, this);
    }

    /// <summary>
    /// Appends text to the output window.
    /// </summary>
    /// <param name="text">The text to append.</param>
    public void AppendText(string text)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (OutputTextBox != null)
            {
                OutputTextBox.Text += text;
                // Auto-scroll to the end
                OutputTextBox.CaretIndex = OutputTextBox.Text?.Length ?? 0;
            }
        });
    }

    /// <summary>
    /// Sets the text in the output window, replacing any existing content.
    /// </summary>
    /// <param name="text">The text to set.</param>
    public void SetText(string text)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (OutputTextBox != null)
            {
                OutputTextBox.Text = text;
                // Auto-scroll to the end
                OutputTextBox.CaretIndex = OutputTextBox.Text?.Length ?? 0;
            }
        });
    }

    /// <summary>
    /// Clears all text from the output window.
    /// </summary>
    public void ClearText()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (OutputTextBox != null)
            {
                OutputTextBox.Text = string.Empty;
            }
        });
    }

    // Menu event handlers

    private void OnQuitClicked(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnSelectAllClicked(object? sender, RoutedEventArgs e)
    {
        if (OutputTextBox != null)
        {
            OutputTextBox.SelectAll();
            OutputTextBox.Focus();
        }
    }

    private async void OnTestDiskReadClicked(object? sender, RoutedEventArgs e)
    {
        ClearText();
        
        string? fileName = await mDiskReadTest!.GetDiskFileToProcess(mLastDiskPath);
        
        if (fileName == null)
        {
            // User cancelled
            return;
        }

        // Update the last used path to the directory of the selected file
        string? directory = Path.GetDirectoryName(fileName);
        if (!string.IsNullOrEmpty(directory))
        {
            mLastDiskPath = directory;
        }
        
        try
        {
            // Open the archive file.
            using FileStream arcFile = new(fileName, FileMode.Open,
                FileAccess.ReadWrite);

            // Analyze the file.  Disk images and file archives are handled differently.
            string? ext = Path.GetExtension(fileName);
            FileAnalyzer.AnalysisResult result = FileAnalyzer.Analyze(arcFile, ext,
                mAppHook, out FileKind kind, out SectorOrder orderHint);
            if (result != FileAnalyzer.AnalysisResult.Success)
            {
                AppendText("Archive or disk image not recognized\n");
                return;
            }

            if (IsDiskImageFile(kind))
            {
                if (!mDiskReadTest!.HandleDiskImage(arcFile, kind, orderHint))
                {
                    AppendText("Error: !HandleDiskImage\n");
                    return;
                }
            }
            else
            {
                if (!mDiskReadTest!.HandleFileArchive(arcFile, kind))
                {
                    AppendText("Error: !HandleFileArchive\n");
                    return;
                }
            }
        }
        catch (IOException ex)
        {
            // Probably a FileNotFoundException.
            AppendText("Error: " + ex.Message + "\n");
        }
    }

    private void OnCopyClicked(object? sender, RoutedEventArgs e)
    {
        if (OutputTextBox != null && !string.IsNullOrEmpty(OutputTextBox.SelectedText))
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            clipboard?.SetTextAsync(OutputTextBox.SelectedText);
        }
        else if (OutputTextBox != null && !string.IsNullOrEmpty(OutputTextBox.Text))
        {
            // If nothing is selected, copy all text
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            clipboard?.SetTextAsync(OutputTextBox.Text);
        }
    }

    private void OnClearTextClicked(object? sender, RoutedEventArgs e)
    {
        ClearText();
    }
}
