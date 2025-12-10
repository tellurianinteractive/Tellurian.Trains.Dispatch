using Tellurian.Trains.Dispatch.Layout;
using Tellurian.Trains.Dispatch.Trains;

namespace Tellurian.Trains.Dispatch.Brokers;

/// <summary>
/// Combines multiple state providers for simpler broker integration.
/// </summary>
/// <remarks>
/// The composite provider coordinates:
/// <list type="bullet">
/// <item><description><see cref="ITrainStateProvider"/> for train states, observed times, and track changes</description></item>
/// <item><description><see cref="IDispatchStateProvider"/> for dispatch states and pass events</description></item>
/// </list>
/// </remarks>
public interface ICompositeStateProvider
{
    /// <summary>
    /// Gets the train state provider.
    /// </summary>
    ITrainStateProvider TrainStateProvider { get; }

    /// <summary>
    /// Gets the dispatch state provider.
    /// </summary>
    IDispatchStateProvider DispatchStateProvider { get; }

    /// <summary>
    /// Applies all state from both providers to the provided objects.
    /// </summary>
    /// <param name="sections">Dictionary of train sections by ID.</param>
    /// <param name="trains">Dictionary of trains by ID.</param>
    /// <param name="calls">Dictionary of train station calls by ID.</param>
    /// <param name="operationPlaces">Dictionary of operation places by ID for resolving references.</param>
    /// <param name="trackStretches">Dictionary of track stretches by ID for resolving references.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ApplyAllStateAsync(
        Dictionary<int, TrainSection> sections,
        Dictionary<int, Train> trains,
        Dictionary<int, TrainStationCall> calls,
        Dictionary<int, OperationPlace> operationPlaces,
        Dictionary<int, TrackStretch> trackStretches,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all state from both providers.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ClearAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true if either provider has saved state to apply.
    /// </summary>
    bool HasAnySavedState { get; }
}
