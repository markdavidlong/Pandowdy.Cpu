using Avalonia.Headless.XUnit;
using Pandowdy.EmuCore.Interfaces;
using System.Reactive.Linq;

namespace Pandowdy.UI.Tests.Services;

/// <summary>
/// Tests for AvaloniaRefreshTicker - provides 60Hz timing for UI refresh.
/// Tests observable stream behavior and timing.
/// </summary>
public class AvaloniaRefreshTickerTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_CreatesInstanceSuccessfully()
    {
        // Arrange & Act
        var ticker = new AvaloniaRefreshTicker();

        // Assert
        Assert.NotNull(ticker);
        Assert.NotNull(ticker.Stream);
    }

    #endregion

    #region Stream Tests

    [Fact]
    public void Stream_IsNotNull()
    {
        // Arrange
        var ticker = new AvaloniaRefreshTicker();

        // Act
        var stream = ticker.Stream;

        // Assert
        Assert.NotNull(stream);
    }

    [AvaloniaFact(Timeout = 5000)]
    public async Task Start_EmitsTicksOnStream()
    {
        // Arrange
        var ticker = new AvaloniaRefreshTicker();
        var received = new List<DateTime>();
        var tcs = new TaskCompletionSource<bool>();

        // Act
        using (ticker.Stream.Subscribe(dt => 
        {
            received.Add(dt);
            if (received.Count >= 8)
            {
                tcs.TrySetResult(true);
            }
        }))
        {
            ticker.Start();
            
            // Wait for at least 8 ticks (should take ~133ms at 60Hz)
            var completed = await Task.WhenAny(tcs.Task, Task.Delay(1000));
            
            ticker.Stop();
        }

        // Assert
        // At 60Hz, should receive 8-16 ticks in the time allowed
        Assert.InRange(received.Count, 8, 20);
    }

    #endregion

    #region Start/Stop Tests

    [AvaloniaFact]
    public void Start_DoesNotThrow()
    {
        // Arrange
        var ticker = new AvaloniaRefreshTicker();

        // Act & Assert
        var exception = Record.Exception(() => ticker.Start());
        Assert.Null(exception);
    }

    [Fact]
    public void Stop_DoesNotThrow()
    {
        // Arrange
        var ticker = new AvaloniaRefreshTicker();

        // Act & Assert
        var exception = Record.Exception(() => ticker.Stop());
        Assert.Null(exception);
    }

    #endregion

    #region Interface Compliance Tests

    [Fact]
    public void ImplementsIRefreshTicker()
    {
        // Arrange
        var ticker = new AvaloniaRefreshTicker();

        // Act & Assert
        Assert.IsAssignableFrom<IRefreshTicker>(ticker);
    }

    [Fact]
    public void Stream_ReturnsObservableOfDateTime()
    {
        // Arrange
        var ticker = new AvaloniaRefreshTicker();

        // Act
        var stream = ticker.Stream;

        // Assert
        Assert.IsAssignableFrom<IObservable<DateTime>>(stream);
    }

    #endregion
}
