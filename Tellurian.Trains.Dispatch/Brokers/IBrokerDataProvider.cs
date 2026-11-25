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
/// 6. <see cref="GetScheduleStartTimeAsync(CancellationToken)"/>
/// 7. <see cref="GetTrainSectionsAsync(CancellationToken)"/>
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

    /// <summary>
    /// Gets the fast clock time the schedule's operating day starts at.
    /// </summary>
    /// <remarks>
    /// Train sections are presented to dispatchers in order of time within the operating day:
    /// in a schedule running over midnight and starting at 05:00, a train at 00:30 comes after one at 23:30.
    /// The default is 00:00, so the operating day equals the calendar day.
    /// </remarks>
    Task<TimeSpan> GetScheduleStartTimeAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(TimeSpan.Zero);

    /// <summary>
    /// Gets the <see cref="TrainSection">train sections</see>: where each train runs between the calls it is cleared at,
    /// and which dispatchers clear it, using the calls from <see cref="GetTrainStationCallsAsync(CancellationToken)"/>.
    /// </summary>
    /// <remarks>
    /// The default is none: the broker then creates a <see cref="TrainSectionKind.Normal"/> section between each
    /// pair of consecutive calls at the stations of a <see cref="DispatchStretch"/>.
    /// </remarks>
    Task<IEnumerable<TrainSectionDefinition>> GetTrainSectionsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IEnumerable<TrainSectionDefinition>>([]);
}
