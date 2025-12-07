namespace Tellurian.Trains.Dispatch.Layout;

/// <summary>
/// Centralized capacity management at the TrackStretch level.
/// Handles occupancy tracking across all TrackStretches and manages
/// cascading occupancy for OtherPlaces (unsignalled junctions and halts).
/// </summary>
public class TrackStretchCapacityManager
{
    private readonly Dictionary<int, TrackStretch> _trackStretches;

    public TrackStretchCapacityManager(IEnumerable<TrackStretch> trackStretches)
    {
        _trackStretches = trackStretches.ToDictionary(s => s.Id);
    }

    /// <summary>
    /// Checks if a train can occupy the given TrackStretch in the specified direction.
    /// </summary>
    public bool CanOccupy(TrainSection section, TrackStretch trackStretch, StretchDirection direction)
    {
        return trackStretch.HasAvailableTrack(direction);
    }

    /// <summary>
    /// Occupies a TrackStretch with the given TrainSection.
    /// Handles cascading occupancy for OtherPlaces.
    /// </summary>
    public void Occupy(TrainSection section, TrackStretch trackStretch, Track track)
    {
        var occupancy = new TrackStretchOccupancy(section, track, DateTimeOffset.UtcNow);
        trackStretch.ActiveOccupancies.Add(occupancy);

        // Handle cascading occupancy for OtherPlace
        if (trackStretch.End is OtherPlace { RequiresCascadingOccupancy: true })
        {
            OccupyCascading(section, trackStretch.End, trackStretch.Id, track);
        }
    }

    /// <summary>
    /// Releases the TrainSection's occupancy from a TrackStretch.
    /// Handles release of cascading occupancies.
    /// </summary>
    public void Release(TrainSection section, TrackStretch trackStretch)
    {
        ReleaseFromTrackStretch(section, trackStretch);

        if (trackStretch.End is OtherPlace { RequiresCascadingOccupancy: true })
        {
            ReleaseCascading(section, trackStretch.End, trackStretch.Id);
        }
    }

    /// <summary>
    /// Releases all occupancies for a TrainSection across all TrackStretches.
    /// Used when a train arrives at destination or is cleared.
    /// </summary>
    public void ReleaseAll(TrainSection section)
    {
        foreach (var trackStretch in _trackStretches.Values)
        {
            ReleaseFromTrackStretch(section, trackStretch);
        }
    }

    private void ReleaseFromTrackStretch(TrainSection section, TrackStretch trackStretch)
    {
        var toRemove = trackStretch.ActiveOccupancies
            .FirstOrDefault(o => o.Section.Id == section.Id);
        if (toRemove != null)
        {
            trackStretch.ActiveOccupancies.Remove(toRemove);
        }
    }

    private void OccupyCascading(TrainSection section, OperationPlace place,
        int excludeTrackStretchId, Track track)
    {
        var connectedTrackStretches = _trackStretches.Values
            .Where(s => s.Id != excludeTrackStretchId && s.StartsAt(place));

        foreach (var trackStretch in connectedTrackStretches)
        {
            var occupancy = new TrackStretchOccupancy(section, track, DateTimeOffset.UtcNow);
            trackStretch.ActiveOccupancies.Add(occupancy);

            var otherEnd = trackStretch.OtherEndWhenStartsAt(place);
            if (otherEnd is OtherPlace { RequiresCascadingOccupancy: true })
            {
                OccupyCascading(section, otherEnd, trackStretch.Id, track);
            }
        }
    }

    private void ReleaseCascading(TrainSection section, OperationPlace place,
        int excludeTrackStretchId)
    {
        var connectedTrackStretches = _trackStretches.Values
            .Where(s => s.Id != excludeTrackStretchId && s.StartsAt(place));

        foreach (var trackStretch in connectedTrackStretches)
        {
            ReleaseFromTrackStretch(section, trackStretch);

            var otherEnd = trackStretch.OtherEndWhenStartsAt(place);
            if (otherEnd is OtherPlace { RequiresCascadingOccupancy: true })
            {
                ReleaseCascading(section, otherEnd, trackStretch.Id);
            }
        }
    }
}
