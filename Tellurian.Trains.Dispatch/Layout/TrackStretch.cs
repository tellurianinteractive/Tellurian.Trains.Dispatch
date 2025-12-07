using Tellurian.Trains.Dispatch.Utilities;

namespace Tellurian.Trains.Dispatch.Layout;

/// <summary>
/// Represents a physical track section between two <see cref="OperationPlace">operational places</see>.
/// A TrackStretch can be shared by multiple <see cref="DispatchStretch">dispatch stretches</see>
/// and capacity is managed at this level.
/// </summary>
/// <remarks>
/// Creates a new TrackStretch between two operational places.
/// </remarks>
/// <param name="start">Starting operational place.</param>
/// <param name="end">Ending operational place.</param>
/// <param name="tracks">Optional list of tracks. Defaults to a single double-directed track.</param>
public class TrackStretch(OperationPlace start, OperationPlace end, IList<Track>? tracks = null)
{
    /// <summary>
    /// Unique identifier, auto-generated if set to 0.
    /// </summary>
    public int Id { get; set { field = value.OrNextId; } }

    /// <summary>
    /// Starting point of this track stretch.
    /// </summary>
    public OperationPlace Start { get; } = start;

    /// <summary>
    /// Ending point of this track stretch.
    /// </summary>
    public OperationPlace End { get; } = end;

    /// <summary>
    /// Physical tracks available on this stretch.
    /// Default is a single double-directed track.
    /// </summary>
    public IList<Track> Tracks { get; } = tracks ?? [new Track(null, TrackOperationDirection.DoubleDirected)];

    /// <summary>
    /// Current train occupancies on this stretch.
    /// Used for capacity management across all DispatchStretches that share this TrackStretch.
    /// </summary>
    public IList<TrackStretchOccupancy> ActiveOccupancies { get; } = [];

    /// <summary>
    /// Returns true if the track stretch connects to the specified place
    /// (either as <see cref="Start"/> or <see cref="To"/>).
    /// </summary>
    public bool StartsAt(OperationPlace place) =>
        Start.Equals(place) || End.Equals(place);

    /// <summary>
    /// Gets the other end of this stretch given one end.
    /// </summary>
    public OperationPlace OtherEndWhenStartsAt(OperationPlace startsAt) =>
        Start.Equals(startsAt) ? End : Start;

    /// <summary>
    /// Returns true if there is at least one track available in the specified direction
    /// that is not currently occupied.
    /// </summary>
    /// <param name="direction">The direction of travel (Forward = From→To, Reverse = To→From).</param>
    public bool HasAvailableTrack(StretchDirection direction)
    {
        var occupiedTrackNumbers = ActiveOccupancies.Select(o => o.Track.Number).ToHashSet();

        return Tracks.Any(track =>
            !track.Direction.IsClosed &&
            (direction == StretchDirection.Forward ? track.AllowsForward : track.AllowsBackward) &&
            !occupiedTrackNumbers.Contains(track.Number));
    }

    /// <summary>
    /// Finds a track suitable for travel in the specified direction.
    /// Prefers the Up track for forward direction, Down track for reverse direction.
    /// </summary>
    /// <param name="direction">The direction of travel.</param>
    /// <returns>A suitable track, or null if none available.</returns>
    public Track? FindAvailableTrack(StretchDirection direction)
    {
        var occupiedTrackNumbers = ActiveOccupancies.Select(o => o.Track.Number).ToHashSet();

        var allowedTracks = Tracks
            .Where(track =>
                !track.Direction.IsClosed &&
                (direction == StretchDirection.Forward ? track.AllowsForward : track.AllowsBackward) &&
                !occupiedTrackNumbers.Contains(track.Number))
            .ToList();

        if (allowedTracks.Count == 0) return null;

        // Prefer up track for forward, down track for reverse
        var preferUp = direction == StretchDirection.Forward;
        return allowedTracks.FirstOrDefault(t => t.IsUpTrack == preferUp)
            ?? allowedTracks.First();
    }

    public override string ToString() => $"{Start.Signature} - {End.Signature}";
}

/// <summary>
/// Represents a train's occupancy of a track on a TrackStretch.
/// </summary>
/// <param name="Section">The TrainSection occupying the track.</param>
/// <param name="Track">The specific track being occupied.</param>
/// <param name="EnteredAt">When the train entered this track stretch.</param>
public record TrackStretchOccupancy(TrainSection Section, Track Track, DateTimeOffset EnteredAt);
