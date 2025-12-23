using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using ReactiveUI;
using ReactiveUI.Avalonia;
using Pandowdy.UI.ViewModels;
using Pandowdy.EmuCore;
using Pandowdy.EmuCore.Interfaces;

namespace Pandowdy.UI;

public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
{
  //  private readonly AppHook mAppHook = new(new SimpleMessageLog());
    private DiskReadTestTemp? mDiskReadTest;
    private string mLastDiskPath = "E:\\develop\\Pandowdy";

    private VA2M? _machine; // injected via Initialize
    private CancellationTokenSource? _emuCts;
    private Task? _emuTask;

    private IRefreshTicker? _refreshTicker; // injected via Initialize
    private IDisposable? _refreshSub;
    private bool _menuPointerActive; // true while pointer is over the menu bar
    private bool _depsInjected; // guard to ensure dependencies injected once

    private bool _capsLockEnabled = true; // default ON
    public bool IsCapsLockEnabledForInput => _capsLockEnabled; // expose to Apple2Display
    
    private bool _showSoftSwitchStatus = true; // default visible
    public bool ShowSoftSwitchStatus
    {
        get => _showSoftSwitchStatus;
        set
        {
            if (_showSoftSwitchStatus != value)
            {
                _showSoftSwitchStatus = value;
                UpdateSoftSwitchStatusVisibility();
            }
        }
    }


    // Parameterless ctor for XAML loader; no heavy work here. Ideally dependencies should be injected here, but 
    // Avalonia needs a parameterless Ctor.  It's icky, but the lesser of evils. I don't want to use a Service Locator
    // inside the class, so there's an Initialize() method that must be called right after the Ctor.
    public MainWindow()
    {
        InitializeComponent();
        
        // Setup menu interaction handlers using x:Name generated field or fallback
        var mainMenu = GetMainMenu();
        if (mainMenu != null)
        {
            mainMenu.PointerEntered += (_, __) => _menuPointerActive = true;
            mainMenu.PointerExited += (_, __) => _menuPointerActive = false;
        }
        
        // No FindControl calls needed - controls are available via x:Name fields (with fallback)
        // Defer attaching machine/frame until Initialize, which should be called next.
    }

    private void UpdateSoftSwitchStatusVisibility()
    {
        var statusPanel = SoftSwitchStatusPanel ?? this.FindControl<SoftSwitchStatusPanel>("SoftSwitchStatusPanel");
        if (statusPanel != null)
        {
            statusPanel.IsVisible = _showSoftSwitchStatus;
        }
    }
    
    // Helper method to get ScreenDisplay with fallback
    private Apple2Display? GetScreenDisplay() => ScreenDisplay ?? this.FindControl<Apple2Display>("ScreenDisplay");
    
    // Helper method to get MainMenu with fallback
    private Menu? GetMainMenu() => MainMenu ?? this.FindControl<Menu>("MainMenu");

    /// <summary>
    /// Initializes the MainWindow with all required dependencies.
    /// Must be called immediately after construction (handled by MainWindowFactory).
    /// </summary>
    public void Initialize(MainWindowViewModel viewModel, VA2M machine, IFrameProvider frameProvider, IRefreshTicker refreshTicker)
    {
        if (_depsInjected)
        {
            throw new InvalidOperationException("Dependencies already initialized.");
        }
        _depsInjected = true;
        ViewModel = viewModel;
        DataContext = viewModel;
        _machine = machine;
        _refreshTicker = refreshTicker;

        // Initialize child controls with their dependencies
        // Use x:Name generated field or fall back to FindControl if XAML compilation didn't generate it
        var screenDisplay = ScreenDisplay ?? this.FindControl<Apple2Display>("ScreenDisplay");
        if (screenDisplay != null)
        {
            screenDisplay.AttachMachine(_machine);
            screenDisplay.AttachFrameProvider(frameProvider);
            screenDisplay.Focus();
        }
        else
        {
            throw new InvalidOperationException("ScreenDisplay control not found. Ensure x:Name='ScreenDisplay' is set in XAML.");
        }
        
        // Initialize status panel with its view model
        var statusPanel = SoftSwitchStatusPanel ?? this.FindControl<SoftSwitchStatusPanel>("SoftSwitchStatusPanel");
        statusPanel?.Initialize(viewModel.SystemStatus);

        this.WhenActivated(disposables =>
        {
            var vm = ViewModel ?? (DataContext as MainWindowViewModel);
            if (vm != null)
            {
                var s1 = vm.WhenAnyValue(x => x.ThrottleEnabled)
                    .Subscribe(v => { if (_machine != null) { _machine.ThrottleEnabled = v; } });
                disposables.Add(s1);
                var s2 = vm.WhenAnyValue(x => x.CapsLockEnabled)
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Subscribe(v =>
                    {
                        _capsLockEnabled = v;
                    });
                disposables.Add(s2);
                var s3 = vm.WhenAnyValue(x => x.ShowScanLines)
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Subscribe(v =>
                    {
                        var screen = GetScreenDisplay();
                        if (screen != null)
                        {
                            screen.ShowScanLines = v;
                            screen.RequestRefresh();
                        }
                    });
                disposables.Add(s3);
                var s4 = vm.WhenAnyValue(x => x.ForceMonochrome)
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Subscribe(v =>
                    {
                        var screen = GetScreenDisplay();
                        if (screen != null)
                        {
                            screen.ForceMono = v;
                            screen.RequestRefresh();
                        }
                    });
                disposables.Add(s4);
                var s5 = vm.WhenAnyValue(x => x.DecreaseContrast)
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Subscribe(v =>
                    {
                        var screen = GetScreenDisplay();
                        if (screen != null)
                        {
                            screen.UseNonLumaContrastMask = v;
                            screen.RequestRefresh();
                        }
                    });
                disposables.Add(s5);
                var s6 = vm.WhenAnyValue(x => x.MonoMixed)
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Subscribe(v =>
                    {
                        var screen = GetScreenDisplay();
                        if (screen != null)
                        {
                            screen.DefringeMixedText = v;
                            screen.RequestRefresh();
                        }
                    });
                disposables.Add(s6);

                // Bridge emulator commands to actions
                var c1 = vm.StartEmu.Subscribe(_ => OnEmuStartClicked(this, new RoutedEventArgs()));
                disposables.Add(c1);
                var c2 = vm.StopEmu.Subscribe(_ => OnEmuStopClicked(this, new RoutedEventArgs()));
                disposables.Add(c2);
                var c3 = vm.ResetEmu.Subscribe(_ => OnEmuResetClicked(this, new RoutedEventArgs()));
                disposables.Add(c3);
                var c4 = vm.StepOnce.Subscribe(_ => OnEmuStepOnceClicked(this, new RoutedEventArgs()));
                disposables.Add(c4);
            }
        });

        RestoreSettingsFromConfigFile();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (!_depsInjected) { return; }
        
        var screenDisplay = ScreenDisplay ?? this.FindControl<Apple2Display>("ScreenDisplay");
        screenDisplay?.Focus();
        
        if (_refreshTicker != null && screenDisplay != null)
        {
            _refreshTicker.Start();
            _refreshSub = _refreshTicker.Stream
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ =>
                {
                    // Signal screen that it's OK to render at 60Hz
                    screenDisplay.RequestRefresh();
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
            GetScreenDisplay()?.Focus();
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!_menuPointerActive)
        {
            GetScreenDisplay()?.Focus();
        }
    }

    private bool IsAnyMenuOpen()
    {
        var mainMenu = GetMainMenu();
        if (mainMenu == null)
        {
            return false;
        }
        foreach (var item in mainMenu.Items)
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
        var mainMenu = GetMainMenu();
        if (mainMenu == null)
        {
            return;
        }
        foreach (var item in mainMenu.Items)
        {
            if (item is MenuItem mi)
            {
                mi.IsSubMenuOpen = false;
            }
        }
        _menuPointerActive = false;
        GetScreenDisplay()?.Focus();
    }

    private async void OnEmuStartClicked(object? sender, RoutedEventArgs e)
    {
        if (!_depsInjected || _machine is null) { return; }
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
            catch (Exception)
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
    private void OnEmuResetClicked(object? sender, RoutedEventArgs e) { if (_depsInjected) { _machine?.UserReset(); } }

    private void OnEmuStepOnceClicked(object? sender, RoutedEventArgs e)
    {
        if (!_depsInjected || _emuCts != null || _machine is null)
        {
            return;
        }
        _machine.Clock();
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
            if (ViewModel != null)
            {
                if (data.ShowScanLines.HasValue) { ViewModel.ShowScanLines = data.ShowScanLines.Value; }
                if (data.MonoMixed.HasValue) { ViewModel.MonoMixed = data.MonoMixed.Value; }
                if (data.ForceMonochrome.HasValue) { ViewModel.ForceMonochrome = data.ForceMonochrome.Value; }
                if (data.DecreaseContrast.HasValue) { ViewModel.DecreaseContrast = data.DecreaseContrast.Value; }
                if (data.ThrottleEnabled.HasValue) { ViewModel.ThrottleEnabled = data.ThrottleEnabled.Value; } else { ViewModel.ThrottleEnabled = true; }
            }
            if (data.ShowSoftSwitchStatus.HasValue) 
            { 
                ShowSoftSwitchStatus = data.ShowSoftSwitchStatus.Value; 
            }
            else 
            { 
                ShowSoftSwitchStatus = true; 
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
                ShowScanLines = ViewModel?.ShowScanLines,
                MonoMixed = ViewModel?.MonoMixed,
                DecreaseContrast = ViewModel?.DecreaseContrast,
                ForceMonochrome = ViewModel?.ForceMonochrome,
                ThrottleEnabled = ViewModel?.ThrottleEnabled,
                ShowSoftSwitchStatus = ShowSoftSwitchStatus,
            };
#pragma warning disable CA1869 // Cache and reuse 'JsonSerializerOptions' instances
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
#pragma warning restore CA1869 // Cache and reuse 'JsonSerializerOptions' instances
            File.WriteAllText(GetConfigPath(), json);
        }
        catch { }
    }

    private void StopEmulator()
    {
        if (_emuCts == null)
        {
            return;
        }
        _emuCts.Cancel();
    }

    private void OnQuitClicked(object? sender, RoutedEventArgs e) => Close();
    
    private void OnToggleSoftSwitchStatusClicked(object? sender, RoutedEventArgs e)
    {
        ShowSoftSwitchStatus = !ShowSoftSwitchStatus;
    }

    private static string GetConfigPath()
    {
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(baseDir, "LydianScaleSoftware", "Pandowdy");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "settings.json");
    }

    private sealed class SettingsConfig
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public bool? ShowScanLines { get; set; }
        public bool? MonoMixed { get; set; }
        public bool? DecreaseContrast { get; set; }
        public bool? ForceMonochrome { get; set; }
        public bool? ThrottleEnabled { get; set; }
        public bool? ShowSoftSwitchStatus { get; set; }
    }

    private void OnMainWindowKeyDown(object? sender, KeyEventArgs e)
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
        GetScreenDisplay()?.Focus();
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (e.Key == Key.F1)
        {
            _machine?.SetPushButton(0, false); // pushbutton released
            e.Handled = true;
        }
        else if (e.Key == Key.F2)
        {
            _machine?.SetPushButton(1, false); // pushbutton 2 released
            e.Handled = true;
        }
        else if (e.Key == Key.LeftShift || e.Key == Key.RightShift)
        {
            _machine?.SetPushButton(2, false); // pushbutton 3 (shift) released
            e.Handled = true;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.LeftShift || e.Key == Key.RightShift)
        {
            _machine?.SetPushButton(2, true); // pushbutton 3 (shift) pressed
            // do not mark handled so other shift combos still work
        }
    }

    private bool HandleAccelerator(KeyEventArgs e)
    {
        if ((e.KeyModifiers & KeyModifiers.Alt) != 0)
        {
            switch (e.Key)
            {
                case Key.S:
                    ViewModel?.ToggleScanLines.Execute().Subscribe();
                    return true;
                case Key.M:
                    ViewModel?.ToggleMonochrome.Execute().Subscribe();
                    return true;
                case Key.D:
                    ViewModel?.ToggleDecreaseContrast.Execute().Subscribe();
                    return true;
                case Key.X:
                    ViewModel?.ToggleMonoMixed.Execute().Subscribe();
                    return true;
                case Key.W:
                    ShowSoftSwitchStatus = !ShowSoftSwitchStatus;
                    return true;
                case Key.F4:
                    Close();
                    return true;
            }
        }
        switch (e.Key)
        {
            case Key.F1:
                _machine?.SetPushButton(0, true); // pushbutton pressed
                return true;
            case Key.F2:
                _machine?.SetPushButton(1, true); // pushbutton 2 pressed
                return true;
            case Key.F5:
                ViewModel?.StartEmu.Execute().Subscribe();
                return true;
            case Key.F6:
                ViewModel?.ToggleCapsLock.Execute().Subscribe();
                return true;
            case Key.F7:
                ViewModel?.ToggleThrottle.Execute().Subscribe();
                return true;
            case Key.F10:
                ViewModel?.StepOnce.Execute().Subscribe();
                return true;
        }
        if (e.Key == Key.F5 && (e.KeyModifiers & KeyModifiers.Shift) != 0)
        {
            ViewModel?.StopEmu.Execute().Subscribe();
            return true;
        }
        if ((e.KeyModifiers & KeyModifiers.Control) != 0 && (e.KeyModifiers & KeyModifiers.Shift) != 0 && e.Key == Key.D2)
        {
            _machine?.InjectKey(0x00);
            e.Handled = true;
            return true;
        }
        if (e.Key == Key.F12 && (e.KeyModifiers & KeyModifiers.Control) != 0)
        {
            ViewModel?.ResetEmu.Execute().Subscribe();
            return true;
        }
        return false;
    }

    private bool TryInjectSpecialKey(KeyEventArgs e)
    {
        if ((e.KeyModifiers & KeyModifiers.Control) != 0 && e.Key >= Key.A && e.Key <= Key.Z)
        {
            byte ctrl = (byte)(e.Key - Key.A + 1);
            _machine?.InjectKey((byte)(ctrl | 0x80));
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
            _machine?.InjectKey((byte)(ascii.Value | 0x80));
            e.Handled = true;
            return true;
        }
        return false;
    }
}
