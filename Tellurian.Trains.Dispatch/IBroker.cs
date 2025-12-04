
using Tellurian.Trains.Dispatch.Trains;

namespace Tellurian.Trains.Dispatch;
/// <summary>
/// 
/// </summary>
public interface IBroker
{
    ITimeProvider TimeProvider { get; }
    IEnumerable<TrainStretch> GetArrivalsFor(Station? station, int maxItems);
    IEnumerable<TrainStretch> GetDeparturesFor(Station? station, int maxItems);
    IEnumerable<IDispatcher> GetDispatchers();
    Task InitAsync(bool isRestart, CancellationToken cancellationToken = default);
}
