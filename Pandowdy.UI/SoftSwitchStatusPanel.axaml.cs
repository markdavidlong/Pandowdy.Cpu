using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Pandowdy.UI.ViewModels;

namespace Pandowdy.UI;

/// <summary>
/// User control that displays Apple II soft switch status information.
/// Shows memory configuration, video modes, pushbuttons, and annunciators.
/// </summary>
public partial class SoftSwitchStatusPanel : UserControl
{
    private bool _isInitialized;

    public SoftSwitchStatusPanel()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Initializes the panel with its view model.
    /// Must be called by parent window after construction.
    /// </summary>
    /// <param name="viewModel">The SystemStatusViewModel to bind to.</param>
    public void Initialize(SystemStatusViewModel viewModel)
    {
        if (_isInitialized)
        {
            throw new System.InvalidOperationException("Panel already initialized.");
        }
        
        _isInitialized = true;
        DataContext = viewModel;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
