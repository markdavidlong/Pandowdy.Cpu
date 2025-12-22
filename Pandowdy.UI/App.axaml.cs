using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using ReactiveUI.Avalonia;
using System;
using Pandowdy.UI.ViewModels;
using Pandowdy.EmuCore;

namespace Pandowdy.UI;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var services = DesktopServiceProvider.Current;
            var vm = services.GetService(typeof(MainWindowViewModel)) as MainWindowViewModel;
            var machine = services.GetService(typeof(VA2M)) as VA2M;
            var frameProvider = services.GetService(typeof(IFrameProvider)) as IFrameProvider;
            var ticker = services.GetService(typeof(IRefreshTicker)) as IRefreshTicker;
            var mainWindow = new MainWindow();
            mainWindow.InjectDependencies(vm!, machine!, frameProvider!, ticker!);
            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }
}

public static class DesktopServiceProvider
{
    private static IServiceProvider? _provider;

    public static void SetProvider(IServiceProvider provider)
    {
        _provider = provider;
    }

    public static IServiceProvider Current
    {
        get
        {
            if (_provider == null)
            {
                throw new InvalidOperationException("ServiceProvider has not been initialized.");
            }

            return _provider;
        }
    }
}
