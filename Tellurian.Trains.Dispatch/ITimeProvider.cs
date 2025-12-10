namespace Tellurian.Trains.Dispatch;

/// <summary>
/// Provides time from fast clock.
/// </summary>
public interface ITimeProvider
{
    TimeSpan Time(TimeSpan? scheduledTime = null);
}

internal sealed class DefaultTimeProvider : ITimeProvider
{
    public static DefaultTimeProvider Instance { get; } = new();

    public TimeSpan Time(TimeSpan? scheduledTime = null) =>
        scheduledTime ?? TimeSpan.Zero;
}