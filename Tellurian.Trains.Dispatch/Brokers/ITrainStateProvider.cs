using Tellurian.Trains.Dispatch.Trains;

namespace Tellurian.Trains.Dispatch.Brokers;

/// <summary>
/// Records and restores train-related state changes.
/// </summary>
/// <remarks>
/// This provider handles:
/// <list type="bullet">
/// <item><description>Train state changes (Planned, Manned, Running, Canceled, Aborted, Completed)</description></item>
/// <item><description>Observed arrival and departure times for station calls</description></item>
/// <item><description>Track changes when trains are assigned different tracks than planned</description></item>
/// </list>
/// </remarks>
public interface ITrainStateProvider : IStateProvider
{
    /// <summary>
    /// Records a train state change.
    /// </summary>
    /// <param name="trainId">The train's ID.</param>
    /// <param name="state">The new train state.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RecordTrainStateChangeAsync(int trainId, TrainState state, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records an observed arrival time for a station call.
    /// </summary>
    /// <param name="callId">The call's ID.</param>
    /// <param name="arrivalTime">The observed arrival time.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RecordObservedArrivalAsync(int callId, TimeSpan arrivalTime, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records an observed departure time for a station call.
    /// </summary>
    /// <param name="callId">The call's ID.</param>
    /// <param name="departureTime">The observed departure time.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RecordObservedDepartureAsync(int callId, TimeSpan departureTime, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a track change for a station call.
    /// </summary>
    /// <param name="callId">The call's ID.</param>
    /// <param name="newTrackNumber">The new track number.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RecordTrackChangeAsync(int callId, string newTrackNumber, CancellationToken cancellationToken = default);
}
