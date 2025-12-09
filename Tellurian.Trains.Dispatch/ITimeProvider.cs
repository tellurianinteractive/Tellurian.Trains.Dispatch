namespace Tellurian.Trains.Dispatch;

/// <summary>
/// Provides time from fast clock.
/// </summary>
public interface ITimeProvider
{
    TimeSpan Time(TimeSpan? sheduledTime = null);
}

internal class DefaultTimeProvider : ITimeProvider
{
    public static DefaultTimeProvider Instance { get; } = new();

    public TimeSpan Time(TimeSpan? scheduledTime = null) =>
        scheduledTime ?? TimeSpan.Zero;
}