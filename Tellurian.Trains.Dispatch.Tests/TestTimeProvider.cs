using Tellurian.Trains.Dispatch;

namespace Tellurian.Trains.Dispatch.Tests;

/// <summary>
/// Simple time provider for testing that returns scheduled time or configured current time.
/// </summary>
internal sealed class TestTimeProvider : ITimeProvider
{
    public TimeSpan CurrentTime { get; set; } = TimeSpan.FromHours(10);

    public Task<TimeStatus> GetTimeStatusAsync(TimeSpan? scheduledTime = null) =>
        Task.FromResult(new TimeStatus(scheduledTime ?? CurrentTime, IsRealTime: false, IsRunning: true));
}
