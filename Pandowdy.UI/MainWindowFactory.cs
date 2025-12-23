using System;
using Pandowdy.UI.ViewModels;
using Pandowdy.EmuCore;
using Pandowdy.EmuCore.Interfaces;

namespace Pandowdy.UI;

/// <summary>
/// Factory implementation that creates and initializes MainWindow instances.
/// All dependencies are injected via constructor, ensuring the factory itself is properly configured.
/// </summary>
public sealed class MainWindowFactory : IMainWindowFactory
{
    private readonly MainWindowViewModel _viewModel;
    private readonly VA2M _machine;
    private readonly IFrameProvider _frameProvider;
    private readonly IRefreshTicker _refreshTicker;

    /// <summary>
    /// Initializes a new instance of MainWindowFactory with all required dependencies.
    /// </summary>
    /// <param name="viewModel">The view model for the main window.</param>
    /// <param name="machine">The Apple II emulator instance.</param>
    /// <param name="frameProvider">The frame provider for display rendering.</param>
    /// <param name="refreshTicker">The 60Hz refresh ticker for UI updates.</param>
    public MainWindowFactory(
        MainWindowViewModel viewModel,
        VA2M machine,
        IFrameProvider frameProvider,
        IRefreshTicker refreshTicker)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _machine = machine ?? throw new ArgumentNullException(nameof(machine));
        _frameProvider = frameProvider ?? throw new ArgumentNullException(nameof(frameProvider));
        _refreshTicker = refreshTicker ?? throw new ArgumentNullException(nameof(refreshTicker));
    }

    /// <summary>
    /// Creates a new MainWindow and initializes it with all dependencies atomically.
    /// </summary>
    /// <returns>A fully initialized MainWindow instance.</returns>
    public MainWindow Create()
    {
        var window = new MainWindow();
        window.Initialize(_viewModel, _machine, _frameProvider, _refreshTicker);
        return window;
    }
}
