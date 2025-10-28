using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.Markup.Xaml;
using CommonUtil;
using DiskArc;
using Pandowdy.Core;
using Pandowdy.UI;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using static DiskArc.Defs;

namespace Pandowdy.UI;

public partial class MainWindow : Window
{
    private readonly AppHook mAppHook = new(new SimpleMessageLog());
    private DiskReadTestTemp? mDiskReadTest;
    private string mLastDiskPath = "E:\\develop\\Pandowdy";

    private VA2M _machine = new();
    private CancellationTokenSource? _emuCts;
    private TextBox? _outputTextBox;

    public MainWindow()
    {
        InitializeComponent();
        _outputTextBox = this.FindControl<TextBox>("OutputTextBox");
        mDiskReadTest = new DiskReadTestTemp(mAppHook, AppendText, this);

        // Wire emulator memory to the Apple2TextScreen via machine's mapped RAM
        var screen = this.FindControl<Apple2TextScreen>("ScreenDisplay");
        if (screen != null)
        {
            screen.MemorySource = _machine.RamMapped;
            screen.AttachMachine(_machine);
            screen.Focus();
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        StopEmulator();
        _machine.Dispose();
    }

    private async void OnEmuStartClicked(object? sender, RoutedEventArgs e)
    {
        if (_emuCts != null) // already running
        { return; }

        _machine.Reset();

        _emuCts = new CancellationTokenSource();
        try
        {
            // run at 1ms slices
            await _machine.RunAsync(_emuCts.Token, 1000);
        }
        catch (OperationCanceledException)
        {
            // ignore
        }
        finally
        {
            _emuCts.Dispose();
            _emuCts = null;
        }
    }

    private void OnEmuStopClicked(object? sender, RoutedEventArgs e)
    {
        StopEmulator();
    }

    private void OnEmuResetClicked(object? sender, RoutedEventArgs e)
    {
        _machine.Reset();
    }

    private void OnEmuStepOnceClicked(object? sender, RoutedEventArgs e)
    {
        _machine.Clock();
    }

    private void StopEmulator()
    {
        _emuCts?.Cancel();
    }

    /// <summary>
    /// Appends text to the output window.
    /// </summary>
    /// <param name="text">The text to append.</param>
    public void AppendText(string text)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_outputTextBox != null)
            {
                _outputTextBox.Text += text;
                // Auto-scroll to the end
                _outputTextBox.CaretIndex = _outputTextBox.Text?.Length ?? 0;
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
            if (_outputTextBox != null)
            {
                _outputTextBox.Text = text;
                // Auto-scroll to the end
                _outputTextBox.CaretIndex = _outputTextBox.Text?.Length ?? 0;
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
            if (_outputTextBox != null)
            {
                _outputTextBox.Text = string.Empty;
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
        if (_outputTextBox != null)
        {
            _outputTextBox.SelectAll();
            _outputTextBox.Focus();
        }
    }

    private async void OnTestDiskReadClicked(object? sender, RoutedEventArgs e)
    {
        ClearText();
        mLastDiskPath = await mDiskReadTest!.HandleDiskRead(mLastDiskPath);
    }

    private void OnCopyClicked(object? sender, RoutedEventArgs e)
    {
        if (_outputTextBox != null && !string.IsNullOrEmpty(_outputTextBox.SelectedText))
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            clipboard?.SetTextAsync(_outputTextBox.SelectedText);
        }
        else if (_outputTextBox != null && !string.IsNullOrEmpty(_outputTextBox.Text))
        {
            // If nothing is selected, copy all text
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            clipboard?.SetTextAsync(_outputTextBox.Text);
        }
    }

    private void OnClearTextClicked(object? sender, RoutedEventArgs e)
    {
        ClearText();
    }
}
