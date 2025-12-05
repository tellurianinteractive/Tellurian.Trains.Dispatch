using Tellurian.Trains.Dispatch.Layout;
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
    /// <summary>
    /// Gets all <see cref="Station">stations</see> that are manned and have the role of <see cref="StationDispatcher"/>.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IEnumerable<Station>> GetStationsAsync(CancellationToken cancellationToken = default);
    /// <summary>
    /// Gets all <see cref="DispatchStretch">dispatch stretches</see> including evenyally <see cref="BlockSignal">block signals</see>
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IEnumerable<DispatchStretch>> GetTrackStretchesAsync(CancellationToken cancellationToken = default);
}
