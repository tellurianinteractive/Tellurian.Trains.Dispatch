
using System.Collections;
using Tellurian.Trains.Dispatch.Trains;

namespace Tellurian.Trains.Dispatch.Configurations;

public interface IBrokerConfiguration
{
    /// <summary>
    /// Gets all <see cref="TrainStationCall">train calls</see> for <see cref="Station">stations</see> that have a dispatch function.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IEnumerable<TrainStationCall>> GetTrainStationCallsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<Station>> GetStationsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<DispatchStretch>> GetTrackStretchesAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<BlockSignal>> GetBlockSignalsAsync(CancellationToken cancellationToken = default);
}
