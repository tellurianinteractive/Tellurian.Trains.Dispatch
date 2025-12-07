using Tellurian.Trains.Dispatch.Layout;
using Tellurian.Trains.Dispatch.Trains;

namespace Tellurian.Trains.Dispatch.Brokers;
/// <summary>
/// Methods for getting data needed for the <see cref="IBroker"/> to work.
/// </summary>
/// <remarks>
/// IMPORTANT! the <see cref="IBroker"/> calls these methods in the following order:
/// 1. <see cref="GetOperationPlacesAsync(CancellationToken)"/>
/// 2. <see cref="GetTrackStretchesAsync(CancellationToken)"/>
/// 3. <see cref="GetDispatchStretchesAsync(CancellationToken)"/>
/// 4. <see cref="GetTrainsAsync(CancellationToken)"/>
/// 5. <see cref="GetTrainStationCallsAsync(CancellationToken)"/>
/// </remarks>
public interface IBrokerDataProvider
{
    /// <summary>
    /// Get data for <see cref="OperationPlace">operation places</see> including <see cref="StationTrack">station tracks</see>.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IEnumerable<OperationPlace>> GetOperationPlacesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets data <see cref="TrackStretch">track stretches</see>.</see>
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IEnumerable<TrackStretch>> GetTrackStretchesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets data for <see cref="DispatchStretch">dispatch stretches</see> including <see cref="TrackStretch">track stretches</see>.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IEnumerable<DispatchStretch>> GetDispatchStretchesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets data for <see cref="Train">trains</see>.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IEnumerable<Train>> GetTrainsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets data for <see cref="TrainStationCall">train calls</see> for <see cref="Station">stations</see> that have a dispatch function.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IEnumerable<TrainStationCall>> GetTrainStationCallsAsync(CancellationToken cancellationToken = default);
}
