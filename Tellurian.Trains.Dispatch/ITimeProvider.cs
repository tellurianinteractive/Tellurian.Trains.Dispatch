namespace Tellurian.Trains.Dispatch;

/// <summary>
/// Provides time for dispatch operations.
/// </summary>
public interface ITimeProvider
{
    Task<TimeStatus> GetTimeStatusAsync(TimeSpan? scheduledTime = null);
}

/// <summary>
/// Default time provider that uses the system wall clock time.
/// </summary>
public sealed class DefaultTimeProvider : ITimeProvider
{
    public static DefaultTimeProvider Instance { get; } = new();

    public Task<TimeStatus> GetTimeStatusAsync(TimeSpan? scheduledTime = null) =>
        Task.FromResult(scheduledTime.HasValue
            ? new TimeStatus(scheduledTime.Value, false, false)
            : new TimeStatus(DateTime.Now.TimeOfDay, true, true));
}

public sealed record TimeStatus(TimeSpan? Time, bool IsRealTime, bool IsRunning, string? Message = null);