namespace Tellurian.Trains.Dispatch.Brokers;

/// <summary>
/// Records and restores dispatch-related state changes for train sections.
/// </summary>
/// <remarks>
/// This provider handles:
/// <list type="bullet">
/// <item><description>Dispatch state changes (Requested, Accepted, Rejected, Revoked, Departed, Arrived, Canceled)</description></item>
/// <item><description>Pass events when trains pass signal-controlled places</description></item>
/// </list>
/// </remarks>
public interface IDispatchStateProvider : IStateProvider
{
    /// <summary>
    /// Records a dispatch state change for a train section.
    /// </summary>
    /// <param name="sectionId">The train section's ID.</param>
    /// <param name="state">The new dispatch state.</param>
    /// <param name="trackStretchIndex">The current track stretch index (set when Departed).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RecordDispatchStateChangeAsync(int sectionId, DispatchState state, int? trackStretchIndex = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a Pass action when a train passes a signal-controlled place.
    /// </summary>
    /// <param name="sectionId">The train section's ID.</param>
    /// <param name="signalControlledPlaceId">The ID of the signal-controlled place that was passed.</param>
    /// <param name="newTrackStretchIndex">The new track stretch index after passing.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RecordPassAsync(int sectionId, int signalControlledPlaceId, int newTrackStretchIndex, CancellationToken cancellationToken = default);
}
