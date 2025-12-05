using Tellurian.Trains.Dispatch.Trains;

namespace Tellurian.Trains.Dispatch.Brokers;
/// <summary>
/// 
/// </summary>
public interface IBroker
{
    ITimeProvider TimeProvider { get; }
    IEnumerable<TrainSection> GetArrivalsFor(Station? station, int maxItems);
    IEnumerable<TrainSection> GetDeparturesFor(Station? station, int maxItems);
    IEnumerable<IDispatcher> GetDispatchers();
    Task InitAsync(bool isRestart, CancellationToken cancellationToken = default);
}
