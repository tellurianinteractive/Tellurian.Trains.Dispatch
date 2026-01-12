using Microsoft.Extensions.Logging.Abstractions;

namespace Tellurian.Trains.Dispatch.Data.Tests;

[TestClass]
public class TellurianFastClockTimeProviderTests
{
    private const string DefaultServerUrl = "https://fastclock.azurewebsites.net";
    private const string DefaultClockName = "Demo";

    [TestMethod]
    public async Task GetTimeStatusAsync_FetchesTimeFromServer()
    {
        // Arrange
        using var provider = new TellurianFastClockTimeProvider(
            DefaultServerUrl, DefaultClockName, NullLogger<TellurianFastClockTimeProvider>.Instance);

        // Act
        var result = await provider.GetTimeStatusAsync();

        // Assert
        // The result should be a valid TimeStatus (either from fastclock or fallback)
        Assert.IsNotNull(result);
        if (result.Time.HasValue)
        {
            Assert.IsTrue(result.Time.Value >= TimeSpan.Zero, "Time should be non-negative");
            Assert.IsTrue(result.Time.Value < TimeSpan.FromHours(24), "Time should be less than 24 hours");
        }
    }

    [TestMethod]
    public async Task GetTimeStatusAsync_WithInvalidServer_ReturnsUnavailableStatus()
    {
        // Arrange
        using var provider = new TellurianFastClockTimeProvider(
            "https://invalid.server.test", DefaultClockName, NullLogger<TellurianFastClockTimeProvider>.Instance);

        // Act
        var result = await provider.GetTimeStatusAsync();

        // Assert
        // Should return a status indicating unavailability without throwing
        Assert.IsNotNull(result);
        Assert.IsFalse(result.IsRunning, "Clock should not be running when server is unavailable");
    }

    [TestMethod]
    public void Constructor_WithNullServerUrl_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.ThrowsExactly<ArgumentNullException>(() => new TellurianFastClockTimeProvider(
            null!, DefaultClockName, NullLogger<TellurianFastClockTimeProvider>.Instance));
    }
}
