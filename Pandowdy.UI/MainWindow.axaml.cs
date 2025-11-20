using Avalonia;
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

    private VA2M _machine; // acquired from DI
    private CancellationTokenSource? _emuCts;
    private Task? _emuTask;
    private TextBox? _outputTextBox;
    private MenuItem? _throttleMenuItem;
    private MenuItem? _capsLockMenuItem;
    private Menu? _mainMenu;
    private Apple2TextScreen? _screen;
    private bool _menuPointerActive; // true while pointer is over the menu bar

    private bool _capsLockEnabled = true; // default ON
    public bool IsCapsLockEnabledForInput => _capsLockEnabled; // expose to Apple2TextScreen

    public MainWindow()
    {
        InitializeComponent();
        // Retrieve DI-provided machine
        var app = (App?)Application.Current;
        _machine = (VA2M)app!.Services.GetService(typeof(VA2M))!;

        _outputTextBox = this.FindControl<TextBox>("OutputTextBox");
        _throttleMenuItem = this.FindControl<MenuItem>("ThrottleMenuItem");
        _capsLockMenuItem = this.FindControl<MenuItem>("CapsLockMenuItem");
        _mainMenu = this.FindControl<Menu>("MainMenu");
        mDiskReadTest = new DiskReadTestTemp(mAppHook, AppendText, this);

        if (_mainMenu != null)
        {
            _mainMenu.PointerEntered += (_, __) => _menuPointerActive = true;
            _mainMenu.PointerExited += (_, __) => _menuPointerActive = false;
        }

        _screen = this.FindControl<Apple2TextScreen>("ScreenDisplay");
        if (_screen != null)
        {
            _screen.MemorySource = _machine.RamMapped;
            _screen.AttachMachine(_machine);
            if (_machine.Bus is VA2MBus bus)
            {
                _screen.SubscribeToVBlank(bus);
            }
            _screen.Focus();
        }

        if (_throttleMenuItem != null) _throttleMenuItem.IsChecked = _machine.ThrottleEnabled;
        if (_capsLockMenuItem != null) _capsLockMenuItem.IsChecked = _capsLockEnabled;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        // Ensure the screen has focus when window opens
        _screen?.Focus();
        // subscribe to vblank
        if (_screen != null && _machine?.Bus is VA2MBus bus)
        {
            _screen.SubscribeToVBlank(bus);
        }
        else
        {
            throw new System.Exception("Failed to subscribe to VBlank: screen or bus is null.");
        }
            // Auto-start the emulator after the window is shown
            Dispatcher.UIThread.Post(() => OnEmuStartClicked(this, new RoutedEventArgs()));
    }

    protected override void OnClosed(EventArgs e)
    {
        // unsubscribe
        if (_screen != null && _machine?.Bus is VA2MBus bus)
        {
            _screen.UnsubscribeFromVBlank(bus);
        }
        StopEmulator();
        base.OnClosed(e);
    }

    protected override void OnGotFocus(GotFocusEventArgs e)
    {
        base.OnGotFocus(e);
        // Keep focus on screen unless actively interacting with menu via pointer
        if (!_menuPointerActive)
        {
            _screen?.Focus();
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        // Clicking anywhere should give focus back to the screen (unless clicking on menu)
        if (!_menuPointerActive)
        {
            _screen?.Focus();
        }
    }

    private bool IsAnyMenuOpen()
    {
        if (_mainMenu == null)
            return false;
        foreach (var item in _mainMenu.Items)
        {
            if (item is MenuItem mi && mi.IsSubMenuOpen)
                return true;
        }
        return false;
    }

    private void CloseAllMenus()
    {
        if (_mainMenu == null)
            return;
        foreach (var item in _mainMenu.Items)
        {
            if (item is MenuItem mi)
            {
                mi.IsSubMenuOpen = false;
            }
        }
        _menuPointerActive = false;
        _screen?.Focus();
    }

    private async void OnEmuStartClicked(object? sender, RoutedEventArgs e)
    {
        if (_emuCts != null) return; // already running

        _machine.Reset();

        _emuCts = new CancellationTokenSource();
        var token = _emuCts.Token;
        _emuTask = Task.Run(async () =>
        {
            try
            {
                await _machine.RunAsync(token, 60).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
        });

        // Optionally observe completion (fire-and-forget)
        _ = _emuTask.ContinueWith(t =>
        {
            // Cleanup after exit
            Dispatcher.UIThread.Post(() =>
            {
                _emuCts?.Dispose();
                _emuCts = null;
                _emuTask = null;
            });
        });
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
        // Prevent stepping while background thread running
        if (_emuCts != null) return;
        _machine.Clock();
    }

    private void OnEmuThrottleClicked(object? sender, RoutedEventArgs e)
    {
        _machine.ThrottleEnabled = !_machine.ThrottleEnabled;
        if (_throttleMenuItem != null)
        {
            _throttleMenuItem.IsChecked = _machine.ThrottleEnabled;
        }
    }

    private void OnEmuCapsLockClicked(object? sender, RoutedEventArgs e)
    {
        _capsLockEnabled = !_capsLockEnabled;
        if (_capsLockMenuItem != null)
        {
            _capsLockMenuItem.IsChecked = _capsLockEnabled;
        }
    }

    private void StopEmulator()
    {
        if (_emuCts == null) return;
        _emuCts.Cancel();
    }

    // Window-level key handling for accelerators when child controls have focus.
    // If menu is not active or the key isn't handled by menu, forward to the screen.
    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        // When pointer is over menu, let accelerators fire; otherwise, keep screen focused
        if (_menuPointerActive || IsAnyMenuOpen())
        {
            // Let menu handle accelerators first
            if (HandleAccelerator(e))
            {
                e.Handled = true;
                return;
            }
            // Not handled by menu: close menu and process in main control
            if (IsAnyMenuOpen())
            {
                CloseAllMenus();
            }
            if (!TryInjectSpecialKey(e))
            {
                // Printable characters will come via TextInput
            }
            return;
        }

        // Menu not active: handle accelerators, else keep focus on screen
        if (HandleAccelerator(e))
        {
            e.Handled = true;
            return;
        }
        _screen?.Focus();
    }

    // Handle window-level TextInput when menu pointer is active to forward printable characters to the machine
    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if ((_menuPointerActive || IsAnyMenuOpen()) && !string.IsNullOrEmpty(e.Text))
        {
            if (IsAnyMenuOpen())
            {
                CloseAllMenus();
            }
            InjectTextToMachine(e.Text);
            e.Handled = true;
        }
    }

    private bool HandleAccelerator(KeyEventArgs e)
    {
        // ALT accelerators
        if ((e.KeyModifiers & KeyModifiers.Alt) !=0)
        {
            switch (e.Key)
            {
                case Key.A:
                    OnSelectAllClicked(this, new RoutedEventArgs());
                    return true;
                case Key.L:
                    OnClearTextClicked(this, new RoutedEventArgs());
                    return true;
                case Key.F4:
                    OnQuitClicked(this, new RoutedEventArgs());
                    return true;
            }
        }
        // Function keys
        switch (e.Key)
        {
            case Key.F5:
                OnEmuStartClicked(this, new RoutedEventArgs());
                return true;
            case Key.F6:
                OnEmuCapsLockClicked(this, new RoutedEventArgs());
                return true;
            case Key.F7:
                OnEmuThrottleClicked(this, new RoutedEventArgs());
                return true;
            case Key.F10:
                OnEmuStepOnceClicked(this, new RoutedEventArgs());
                return true;
            case Key.F12:
                OnEmuResetClicked(this, new RoutedEventArgs());
                return true;
        }
        // Shift+F5
        if (e.Key == Key.F5 && (e.KeyModifiers & KeyModifiers.Shift) !=0)
        {
            OnEmuStopClicked(this, new RoutedEventArgs());
            return true;
        }
        return false;
    }

    // Map special (non-printable) keys to Apple II equivalents and inject.
    private bool TryInjectSpecialKey(KeyEventArgs e)
    {
        if ((e.KeyModifiers & KeyModifiers.Control) !=0 && e.Key >= Key.A && e.Key <= Key.Z)
        {
            byte ctrl = (byte)(e.Key - Key.A +1);
            _machine.InjectKey((byte)(ctrl |0x80));
            e.Handled = true;
            return true;
        }
        byte? ascii = e.Key switch
        {
            Key.Up => (byte)0x0B, // ^K
            Key.Down => (byte)0x0A, // LF
            Key.Left => (byte)0x08, // BS
            Key.Right => (byte)0x15, // ^U
            Key.Delete => (byte)0x7F, // DEL
            Key.Enter => (byte) '\r',
            Key.Tab => (byte) '\t',
            Key.Escape => (byte)0x1B,
            Key.Back => ((e.KeyModifiers & KeyModifiers.Shift) !=0) ? (byte)0x7F : (byte)0x08,
            _ => null
        };
        if (ascii.HasValue)
        {
            _machine.InjectKey((byte) (ascii.Value |0x80));
            e.Handled = true;
            return true;
        }
        return false;
    }

    private void InjectTextToMachine(string text)
    {
        foreach (char ch in text)
        {
            char c = ch;
            if (c == '\n') c = '\r';
            if (_capsLockEnabled && c >= 'a' && c <= 'z')
            {
                c = (char)(c -32);
            }
            if (c <=0x7F)
            {
                _machine.InjectKey((byte)(((byte)c) |0x80));
            }
        }
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
                _outputTextBox.CaretIndex = _outputTextBox.Text?.Length ??0;
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
                _outputTextBox.CaretIndex = _outputTextBox.Text?.Length ??0;
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
