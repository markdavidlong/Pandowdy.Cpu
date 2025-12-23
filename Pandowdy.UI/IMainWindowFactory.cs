namespace Pandowdy.UI;

/// <summary>
/// Factory for creating and initializing MainWindow instances with proper dependency injection.
/// Encapsulates the two-phase initialization required by Avalonia's parameterless constructor constraint.
/// </summary>
public interface IMainWindowFactory
{
    /// <summary>
    /// Creates a new MainWindow instance and initializes it with all required dependencies.
    /// This method handles both construction and initialization atomically.
    /// </summary>
    /// <returns>A fully initialized MainWindow ready to be displayed.</returns>
    MainWindow Create();
}
