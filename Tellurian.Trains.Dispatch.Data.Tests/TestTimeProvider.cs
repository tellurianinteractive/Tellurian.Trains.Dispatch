using Tellurian.Trains.Dispatch;

namespace Tellurian.Trains.Dispatch.Data.Tests;

/// <summary>
/// Simple time provider for testing that returns scheduled time or configured current time.
/// </summary>
public class TestTimeProvider : ITimeProvider
{
    public TimeSpan CurrentTime { get; set; } = TimeSpan.FromHours(10);

    public TimeSpan Time(TimeSpan? scheduledTime = null) =>
        scheduledTime ?? CurrentTime;
}
