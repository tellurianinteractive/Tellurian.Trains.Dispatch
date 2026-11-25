namespace Tellurian.Trains.Dispatch.Brokers;

/// <summary>
/// Central coordinator for train dispatch operations.
/// </summary>
public interface IBroker
{
    ITimeProvider TimeProvider { get; }

    /// <summary>
    /// Gets the fast clock time the schedule's operating day starts at.
    /// Arrivals and departures are ordered by time within the operating day.
    /// </summary>
    TimeSpan ScheduleStartTime { get; }

    /// <summary>
    /// Gets all train sections managed by this broker.
    /// Used by state providers for state restoration.
    /// </summary>
    IEnumerable<TrainSection> TrainSections { get; }

    IEnumerable<ActionContext> GetArrivalActionsFor(IDispatcher dispatcher, int maxItems);
    IEnumerable<ActionContext> GetDepartureActionsFor(IDispatcher dispatcher, int maxItems);
    IEnumerable<IDispatcher> GetDispatchers();
    Task InitAsync(bool isRestart, CancellationToken cancellationToken = default);
}
