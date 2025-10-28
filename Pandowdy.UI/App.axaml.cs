using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace pandowdy;

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
            var mainWindow = new MainWindow();
            desktop.MainWindow = mainWindow;

            // Display all 256 glyphs after window is shown
            // mainWindow.Opened += (s, e) => DisplayAllGlyphs(mainWindow);
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Displays all 256 glyphs in a 16x16 grid on the Apple2TextScreen.
    /// 16 characters per line, 16 lines total.
    /// </summary>
    //private void DisplayAllGlyphs(MainWindow mainWindow)
    //{
    //    // Find the ScreenDisplay control in the window using TopLevel
    //    var topLevel = TopLevel.GetTopLevel(mainWindow);
    //    if (topLevel != null)
    //    {
    //        var screen = topLevel.FindControl<Apple2TextScreen>("ScreenDisplay");
    //        if (screen != null)
    //        {
    //            // Display all 256 glyphs: 16 characters per line, 16 lines
    //            for (int charCode = 0; charCode < 256; charCode++)
    //            {
    //                int x = charCode % 16;
    //                int y = charCode / 16;
    //                screen.SetChar(x, y, (byte)charCode);
    //            }
    //        }
    //    }
    //}
}
