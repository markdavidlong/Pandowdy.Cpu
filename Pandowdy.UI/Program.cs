using Avalonia;
using System;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pandowdy.Core;
using Pandowdy.UI.ViewModels;

namespace Pandowdy.UI;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var builder = new HostApplicationBuilder(args);
        builder.Services.AddLogging(l => l.AddDebug());
        builder.Services.AddSingleton<IFrameProvider, FrameProvider>();
        //builder.Services.AddSingleton<IErrorProvider, ErrorProvider>();
        builder.Services.AddSingleton<IEmulatorState, EmulatorStateProvider>();
        builder.Services.AddSingleton<IRefreshTicker, AvaloniaRefreshTicker>();
        //builder.Services.AddSingleton<IDisassemblyProvider, DisassemblyProvider>();
        builder.Services.AddSingleton<ISystemStatusProvider, SystemStatusProvider>();

        // ViewModels
        builder.Services.AddTransient<EmulatorStateViewModel>();
        //builder.Services.AddTransient<ErrorLogViewModel>();
        //builder.Services.AddTransient<DisassemblyViewModel>();
        builder.Services.AddTransient<MainWindowViewModel>();
        builder.Services.AddTransient<SystemStatusViewModel>();

        // Machine factory (singleton instance for now)
        builder.Services.AddSingleton(provider =>
        {
            var state = provider.GetRequiredService<IEmulatorState>();
            var frame = provider.GetRequiredService<IFrameProvider>();
            var sysStatus = provider.GetRequiredService<ISystemStatusProvider>();
            return new VA2M(state, frame, sysStatus);
        });

        var host = builder.Build();

        BuildAvaloniaApp(host.Services)
            .StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp(IServiceProvider services)
        => AppBuilder.Configure(() => new App(services))
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
