namespace Tellurian.Trains.Dispatch.Brokers;

/// <summary>
/// Central coordinator for train dispatch operations.
/// </summary>
public interface IBroker
{
    ITimeProvider TimeProvider { get; }
    IEnumerable<ActionContext> GetArrivalActionsFor(IDispatcher dispatcher, int maxItems);
    IEnumerable<ActionContext> GetDepartureActionsFor(IDispatcher dispatcher, int maxItems);
    IEnumerable<IDispatcher> GetDispatchers();
    Task InitAsync(bool isRestart, CancellationToken cancellationToken = default);
}
