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
using Pandowdy.UI.ViewModels; // ensure ViewModel type is visible
using System.Text.Json;
using System.Diagnostics;

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
    private MenuItem? _scanLinesMenuItem;
    private MenuItem? _monochromeMenuItem;
    private MenuItem? _decreaseContrastMenuItem;
    private MenuItem? _monoMixedMenuItem;

    private Menu? _mainMenu;
    private Apple2Display? _screen;
    private IRefreshTicker? _refreshTicker;
    private IDisposable? _refreshSub;
    private bool _menuPointerActive; // true while pointer is over the menu bar

    private bool _capsLockEnabled = true; // default ON
    public bool IsCapsLockEnabledForInput => _capsLockEnabled; // expose to Apple2TextScreen
    private Rect _lastNormalBounds;
    private PixelPoint _lastNormalPosition;

    public MainWindow()
    {
        InitializeComponent();

        var app = (App?)Application.Current;
        _machine = (VA2M)app!.Services.GetService(typeof(VA2M))!;
        _refreshTicker = (IRefreshTicker?)app!.Services.GetService(typeof(IRefreshTicker));

        _outputTextBox = this.FindControl<TextBox>("OutputTextBox");
        _throttleMenuItem = this.FindControl<MenuItem>("ThrottleMenuItem");
        _capsLockMenuItem = this.FindControl<MenuItem>("CapsLockMenuItem");
        _scanLinesMenuItem = this.FindControl<MenuItem>("ScanLinesMenuItem");
        _monochromeMenuItem = this.FindControl<MenuItem>("MonochromeMenuItem");
        _decreaseContrastMenuItem = this.FindControl<MenuItem>("DecreaseContrastMenuItem");
        _monoMixedMenuItem = this.FindControl<MenuItem>("MonoMixedMenuItem");

        _mainMenu = this.FindControl<Menu>("MainMenu");
        mDiskReadTest = new DiskReadTestTemp(mAppHook, AppendText, this);

        if (_mainMenu != null)
        {
            _mainMenu.PointerEntered += (_, __) => _menuPointerActive = true;
            _mainMenu.PointerExited += (_, __) => _menuPointerActive = false;
        }

        _screen = this.FindControl<Apple2Display>("ScreenDisplay");
        if (_screen != null)
        {
            _screen.AttachMachine(_machine);
            // Attach frame provider from DI
            var frameProvider = (IFrameProvider)app!.Services.GetService(typeof(IFrameProvider))!;
            _screen.AttachFrameProvider(frameProvider);
            _screen.Focus();
        }

        if (_throttleMenuItem != null)
        {
            _throttleMenuItem.IsChecked = _machine.ThrottleEnabled;
        }

        if (_capsLockMenuItem != null)
        {
            _capsLockMenuItem.IsChecked = _capsLockEnabled;
        }

        if (_scanLinesMenuItem != null && _screen != null)
        {
            _scanLinesMenuItem.IsChecked = _screen.ShowScanLines;
        }

        if (_monoMixedMenuItem != null && _screen != null)
        {
            _monoMixedMenuItem.IsChecked = _screen.DefringeMixedText;
        }
        if (_monochromeMenuItem!= null && _screen != null)
        {
            _monochromeMenuItem.IsChecked = _screen.ForceMono;
        }
        if (_decreaseContrastMenuItem!= null && _screen != null)
        {
            _decreaseContrastMenuItem.IsChecked = _screen.UseNonLumaContrastMask;
        }

        // Manual activation of nested SystemStatus view model
        if (DataContext is MainWindowViewModel vm)
        {
            // Activation no longer required after direct subscription change.
        }

        RestoreSettingsFromConfigFile();

        // Track last normal bounds for saving/restoring unmaximized geometry
        _lastNormalBounds = Bounds;
        _lastNormalPosition = Position;
        this.GetObservable(Window.WindowStateProperty).Subscribe(state =>
        {
            if (state == WindowState.Normal)
            {
                _lastNormalBounds = Bounds;
                _lastNormalPosition = Position;
            }
        });
        this.GetObservable(Window.BoundsProperty).Subscribe(_ =>
        {
            if (WindowState == WindowState.Normal)
            {
                _lastNormalBounds = Bounds;
                _lastNormalPosition = Position;
            }
        });
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        _screen?.Focus();
        if (_refreshTicker != null && _screen != null)
        {
            _refreshTicker.Start();
            _refreshSub = _refreshTicker.Stream.Subscribe(_ =>
            {
                // Request screen refresh; other synced tasks can also hook here
                _screen.RequestRefresh();
                _machine.GenerateStatusData();
            });
        }
        Dispatcher.UIThread.Post(() => OnEmuStartClicked(this, new RoutedEventArgs()));
    }

    protected override void OnClosed(EventArgs e)
    {
        SaveSettingsToConfigFile();
        _refreshSub?.Dispose();
        _refreshSub = null;
        _refreshTicker?.Stop();
        StopEmulator();
        base.OnClosed(e);
    }

    protected override void OnGotFocus(GotFocusEventArgs e)
    {
        base.OnGotFocus(e);
        if (!_menuPointerActive)
        {
            _screen?.Focus();
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!_menuPointerActive)
        {
            _screen?.Focus();
        }
    }

    private bool IsAnyMenuOpen()
    {
        if (_mainMenu == null)
        {
            return false;
        }
        foreach (var item in _mainMenu.Items)
        {
            if (item is MenuItem mi && mi.IsSubMenuOpen)
            {
                return true;
            }
        }
        return false;
    }

    private void CloseAllMenus()
    {
        if (_mainMenu == null)
        {
            return;
        }
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
        if (_emuCts != null)
        {
            return;
        }
        _machine.Reset();
        _emuCts = new CancellationTokenSource();
        var token = _emuCts.Token;
        _emuTask = Task.Run(async () =>
        {
            try
            {
                await _machine.RunAsync(token, 60).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        });
        _ = _emuTask.ContinueWith(t =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                _emuCts?.Dispose();
                _emuCts = null;
                _emuTask = null;
            });
        });
    }

    private void OnEmuStopClicked(object? sender, RoutedEventArgs e) => StopEmulator();
    private void OnEmuResetClicked(object? sender, RoutedEventArgs e) => _machine.UserReset();

    private void OnEmuStepOnceClicked(object? sender, RoutedEventArgs e)
    {
        if (_emuCts != null)
        {
            return;
        }
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

    private void OnViewScanLinesClicked(object? sender, RoutedEventArgs e)
    {
        if (_screen == null)
        {
            return;
        }
        _screen.ShowScanLines = !_screen.ShowScanLines;
        if (_scanLinesMenuItem != null)
        {
            _scanLinesMenuItem.IsChecked = _screen.ShowScanLines;
        }
        _screen.InvalidateVisual();
    }

    private void OnMonochromeClicked(object? sender, RoutedEventArgs e)
    {
        if (_screen == null)
        {
            return;
        }
        _screen.ForceMono = !_screen.ForceMono;
        if (_monochromeMenuItem != null)
        {
            _monochromeMenuItem.IsChecked = _screen.ForceMono;
        }
        _screen.InvalidateVisual();
    }

    private void OnMonoMixedClicked(object? sender, RoutedEventArgs e)
    {
        if (_screen == null)
        {
            return;
        }

        _screen.DefringeMixedText = !_screen.DefringeMixedText;
        if (_monochromeMenuItem != null)
        {
            _monochromeMenuItem.IsChecked = _screen.DefringeMixedText;
        }

        _screen.InvalidateVisual();
    }


    private void OnDecreaseContrastClicked(object? sender, RoutedEventArgs e)
    {
        if (_screen == null)
        {
            return;
        }

        _screen.UseNonLumaContrastMask = !_screen.UseNonLumaContrastMask;
        if (_monochromeMenuItem != null)
        {
            _monochromeMenuItem.IsChecked = _screen.UseNonLumaContrastMask;
        }
        _screen.InvalidateVisual();
    }



    private void StopEmulator()
    {
        if (_emuCts == null)
        {
            return;
        }
        _emuCts.Cancel();
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (_menuPointerActive || IsAnyMenuOpen())
        {
            if (HandleAccelerator(e))
            {
                e.Handled = true;
                return;
            }
            if (IsAnyMenuOpen())
            {
                CloseAllMenus();
            }
            if (!TryInjectSpecialKey(e))
            {
            }
            return;
        }
        if (HandleAccelerator(e))
        {
            e.Handled = true;
            return;
        }
        _screen?.Focus();
    }

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
        if ((e.KeyModifiers & KeyModifiers.Alt) != 0)
        {
            switch (e.Key)
            {
                case Key.A:
                    OnSelectAllClicked(this, new RoutedEventArgs());
                    return true;
                case Key.L:
                    OnClearTextClicked(this, new RoutedEventArgs());
                    return true;
                case Key.S:
                    OnViewScanLinesClicked(this, new RoutedEventArgs());
                    return true;
                case Key.M:
                    OnMonochromeClicked(this, new RoutedEventArgs());
                    return true;
                case Key.D:
                    OnDecreaseContrastClicked(this, new RoutedEventArgs());
                    return true;
                case Key.X:
                    OnMonoMixedClicked(this, new RoutedEventArgs());
                    return true;
                case Key.F4:
                    Close();
                    return true;
            }
        }
        switch (e.Key)
        {
            case Key.F1:
                _machine.SetPushButton(0, true); // pushbutton pressed
                return true;
            case Key.F2:
                _machine.SetPushButton(1, true); // pushbutton 2 pressed
                return true;
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
        }
        if (e.Key == Key.F5 && (e.KeyModifiers & KeyModifiers.Shift) != 0)
        {
            OnEmuStopClicked(this, new RoutedEventArgs());
            return true;
        }
        // Ctrl+F12 now triggers the previous F12 action (Break/Reset); plain F12 ignored.
        if (e.Key == Key.F12 && (e.KeyModifiers & KeyModifiers.Control) != 0)
        {
            OnEmuResetClicked(this, new RoutedEventArgs());
            return true;
        }
        return false;
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (e.Key == Key.F1)
        {
            _machine.SetPushButton(0, false); // pushbutton released
            e.Handled = true;
        }
        else if (e.Key == Key.F2)
        {
            _machine.SetPushButton(1, false); // pushbutton 2 released
            e.Handled = true;
        }
        else if (e.Key == Key.LeftShift || e.Key == Key.RightShift)
        {
            _machine.SetPushButton(2, false); // pushbutton 3 (shift) released
            e.Handled = true;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.LeftShift || e.Key == Key.RightShift)
        {
            _machine.SetPushButton(2, true); // pushbutton 3 (shift) pressed
            // do not mark handled so other shift combos still work
        }
    }

    private bool TryInjectSpecialKey(KeyEventArgs e)
    {
        if ((e.KeyModifiers & KeyModifiers.Control) != 0 && e.Key >= Key.A && e.Key <= Key.Z)
        {
            byte ctrl = (byte)(e.Key - Key.A + 1);
            _machine.InjectKey((byte)(ctrl | 0x80));
            e.Handled = true;
            return true;
        }
        byte? ascii = e.Key switch
        {
            Key.Up => (byte)0x0B,
            Key.Down => (byte)0x0A,
            Key.Left => (byte)0x08,
            Key.Right => (byte)0x15,
            Key.Delete => (byte)0x7F,
            Key.Enter => (byte)'\r',
            Key.Tab => (byte)'\t',
            Key.Escape => (byte)0x1B,
            Key.Back => (e.KeyModifiers & KeyModifiers.Shift) != 0 ? (byte)0x7F : (byte)0x08,
            _ => null
        };
        if (ascii.HasValue)
        {
            _machine.InjectKey((byte)(ascii.Value | 0x80));
            e.Handled = true;
            return true;
        }
        return false;
    }

    private void InjectTextToMachine(string text)
    {
        foreach (char ch in text)
        {
            char c = ch == '\n' ? '\r' : ch;
            if (_capsLockEnabled && c >= 'a' && c <= 'z')
            {
                c = (char)(c - 32);
            }
            if (c <= 0x7F)
            {
                _machine.InjectKey((byte)(((byte)c) | 0x80));
            }
        }
    }

    public void AppendText(string text)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_outputTextBox != null)
            {
                _outputTextBox.Text += text;
                _outputTextBox.CaretIndex = _outputTextBox.Text?.Length ?? 0;
            }
        });
    }

    public void SetText(string text)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_outputTextBox != null)
            {
                _outputTextBox.Text = text;
                _outputTextBox.CaretIndex = _outputTextBox.Text?.Length ?? 0;
            }
        });
    }

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

    private void OnQuitClicked(object? sender, RoutedEventArgs e) => Close();
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
            var clipboard = TopLevel.GetTopLevel(_outputTextBox)?.Clipboard;
            clipboard?.SetTextAsync(_outputTextBox.SelectedText);
        }
        else if (_outputTextBox != null && !string.IsNullOrEmpty(_outputTextBox.Text))
        {
            var clipboard = TopLevel.GetTopLevel(_outputTextBox)?.Clipboard;
            clipboard?.SetTextAsync(_outputTextBox.Text);
        }
    }
    private void OnClearTextClicked(object? sender, RoutedEventArgs e) => ClearText();

    private sealed record WindowGeometry(int Left, int Top, int Width, int Height, int WindowState, int ScreenIndex,
        int NormalLeft, int NormalTop, int NormalWidth, int NormalHeight, int OffsetX, int OffsetY);
    private sealed class SettingsConfig
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public bool? CapsLockEnabled { get; set; }
        public bool? ShowScanLines { get; set; }
        public bool? MonoMixed { get; set; }
        public bool? DecreaseContrast { get; set; }
    }

    private static string GetConfigPath()
    {
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(baseDir, "LydianScaleSoftware", "Pandowdy");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "settings.json");
    }

    private void RestoreSettingsFromConfigFile()
    {
        try
        {
            var path = GetConfigPath();
            if (!File.Exists(path))
            {
                return;
            }
            var json = File.ReadAllText(path);
            var data = JsonSerializer.Deserialize<SettingsConfig>(json);
            if (data == null)
            {
                return;
            }
            if (data.Width > 0 && data.Height > 0)
            {
                Width = data.Width;
                Height = data.Height;
            }
            // Restore menu/feature states if present
            if (data.CapsLockEnabled.HasValue)
            {
                _capsLockEnabled = data.CapsLockEnabled.Value;
                if (_capsLockMenuItem != null)
                {
                    _capsLockMenuItem.IsChecked = _capsLockEnabled;
                }
            }
            if (_screen != null)
            {
                if (data.ShowScanLines.HasValue)
                {
                    _screen.ShowScanLines = data.ShowScanLines.Value;
                    if (_scanLinesMenuItem != null)
                    {
                        _scanLinesMenuItem.IsChecked = _screen.ShowScanLines;
                    }
                }
                if (data.MonoMixed.HasValue)
                {
                    _screen.DefringeMixedText = data.MonoMixed.Value;
                    if (_monoMixedMenuItem != null)
                    {
                        _monoMixedMenuItem.IsChecked = _screen.DefringeMixedText;
                    }
                }
                if (data.DecreaseContrast.HasValue)
                {
                    _screen.UseNonLumaContrastMask = data.DecreaseContrast.Value;
                    if (_decreaseContrastMenuItem != null)
                    {
                        _decreaseContrastMenuItem.IsChecked = _screen.UseNonLumaContrastMask;
                    }
                }
            }
        }
        catch { }
    }

    private void SaveSettingsToConfigFile()
    {
        try
        {
            var data = new SettingsConfig
            {
                Width = (int)Width,
                Height = (int)Height,
                CapsLockEnabled = _capsLockEnabled,
                ShowScanLines = _screen?.ShowScanLines,
                MonoMixed = _screen?.DefringeMixedText,
                DecreaseContrast = _screen?.UseNonLumaContrastMask,
            };
#pragma warning disable CA1869 // Cache and reuse 'JsonSerializerOptions' instances
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
#pragma warning restore CA1869 // Cache and reuse 'JsonSerializerOptions' instances
            File.WriteAllText(GetConfigPath(), json);
        }
        catch { }
    }
}
