using Tellurian.Trains.Dispatch.Layout;
using Tellurian.Trains.Dispatch.Trains;

namespace Tellurian.Trains.Dispatch.Brokers;

/// <summary>
/// Base interface for state providers that record individual state changes.
/// </summary>
public interface IStateProvider
{
    /// <summary>
    /// Applies all recorded state changes to the provided objects.
    /// </summary>
    /// <param name="sections">Dictionary of train sections by ID.</param>
    /// <param name="trains">Dictionary of trains by ID.</param>
    /// <param name="calls">Dictionary of train station calls by ID.</param>
    /// <param name="operationPlaces">Dictionary of operation places by ID for resolving references.</param>
    /// <param name="trackStretches">Dictionary of track stretches by ID for resolving references.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ApplyStateAsync(
        Dictionary<int, TrainSection> sections,
        Dictionary<int, Train> trains,
        Dictionary<int, TrainStationCall> calls,
        Dictionary<int, OperationPlace> operationPlaces,
        Dictionary<int, TrackStretch> trackStretches,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all recorded state (for session reset).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ClearAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true if there is saved state to apply.
    /// </summary>
    bool HasSavedState { get; }
}
