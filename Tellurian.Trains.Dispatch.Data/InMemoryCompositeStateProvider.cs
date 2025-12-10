using Tellurian.Trains.Dispatch;
using Tellurian.Trains.Dispatch.Brokers;
using Tellurian.Trains.Dispatch.Layout;
using Tellurian.Trains.Dispatch.Trains;

namespace Tellurian.Trains.Dispatch.Data;

/// <summary>
/// In-memory implementation of <see cref="ICompositeStateProvider"/> for testing.
/// Does not persist state between sessions.
/// </summary>
public class InMemoryCompositeStateProvider : ICompositeStateProvider
{
    private readonly InMemoryTrainStateProvider _trainStateProvider = new();
    private readonly InMemoryDispatchStateProvider _dispatchStateProvider = new();

    public ITrainStateProvider TrainStateProvider => _trainStateProvider;
    public IDispatchStateProvider DispatchStateProvider => _dispatchStateProvider;
    public bool HasAnySavedState => _trainStateProvider.HasSavedState || _dispatchStateProvider.HasSavedState;

    public async Task ApplyAllStateAsync(
        Dictionary<int, TrainSection> sections,
        Dictionary<int, Train> trains,
        Dictionary<int, TrainStationCall> calls,
        Dictionary<int, OperationPlace> operationPlaces,
        Dictionary<int, TrackStretch> trackStretches,
        CancellationToken cancellationToken = default)
    {
        await _trainStateProvider.ApplyStateAsync(sections, trains, calls, operationPlaces, trackStretches, cancellationToken).ConfigureAwait(false);
        await _dispatchStateProvider.ApplyStateAsync(sections, trains, calls, operationPlaces, trackStretches, cancellationToken).ConfigureAwait(false);
    }

    public Task ClearAllAsync(CancellationToken cancellationToken = default)
    {
        _trainStateProvider.Clear();
        _dispatchStateProvider.Clear();
        return Task.CompletedTask;
    }
}

/// <summary>
/// In-memory implementation of <see cref="ITrainStateProvider"/> for testing.
/// </summary>
public class InMemoryTrainStateProvider : ITrainStateProvider
{
    private readonly List<TrainStateChange> _trainStateChanges = [];
    private readonly List<ObservedTimeChange> _observedTimeChanges = [];
    private readonly List<TrackChange> _trackChanges = [];

    public bool HasSavedState => _trainStateChanges.Count > 0 || _observedTimeChanges.Count > 0 || _trackChanges.Count > 0;

    public Task RecordTrainStateChangeAsync(int trainId, TrainState state, CancellationToken cancellationToken = default)
    {
        _trainStateChanges.Add(new TrainStateChange(trainId, state, DateTimeOffset.UtcNow));
        return Task.CompletedTask;
    }

    public Task RecordObservedArrivalAsync(int callId, TimeSpan arrivalTime, CancellationToken cancellationToken = default)
    {
        _observedTimeChanges.Add(new ObservedTimeChange(callId, arrivalTime, true, DateTimeOffset.UtcNow));
        return Task.CompletedTask;
    }

    public Task RecordObservedDepartureAsync(int callId, TimeSpan departureTime, CancellationToken cancellationToken = default)
    {
        _observedTimeChanges.Add(new ObservedTimeChange(callId, departureTime, false, DateTimeOffset.UtcNow));
        return Task.CompletedTask;
    }

    public Task RecordTrackChangeAsync(int callId, string newTrackNumber, CancellationToken cancellationToken = default)
    {
        _trackChanges.Add(new TrackChange(callId, newTrackNumber, DateTimeOffset.UtcNow));
        return Task.CompletedTask;
    }

    public Task ApplyStateAsync(
        Dictionary<int, TrainSection> sections,
        Dictionary<int, Train> trains,
        Dictionary<int, TrainStationCall> calls,
        Dictionary<int, OperationPlace> operationPlaces,
        Dictionary<int, TrackStretch> trackStretches,
        CancellationToken cancellationToken = default)
    {
        foreach (var change in _trainStateChanges.OrderBy(c => c.Timestamp))
        {
            if (trains.TryGetValue(change.TrainId, out var train))
            {
                train.State = change.State;
            }
        }

        foreach (var change in _observedTimeChanges.OrderBy(c => c.Timestamp))
        {
            if (calls.TryGetValue(change.CallId, out var call))
            {
                if (change.IsArrival)
                    call.SetArrivalTime(change.Time);
                else
                    call.SetDepartureTime(change.Time);
            }
        }

        foreach (var change in _trackChanges.OrderBy(c => c.Timestamp))
        {
            if (calls.TryGetValue(change.CallId, out var call))
            {
                // Use operationPlaces to resolve the station and find the track
                var stationId = call.At.Id;
                if (operationPlaces.TryGetValue(stationId, out var place))
                {
                    var newTrack = place.Tracks.FirstOrDefault(t => t.Number == change.NewTrackNumber);
                    if (newTrack is not null)
                    {
                        call.ChangeTrack(newTrack);
                    }
                }
            }
        }

        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        Clear();
        return Task.CompletedTask;
    }

    public void Clear()
    {
        _trainStateChanges.Clear();
        _observedTimeChanges.Clear();
        _trackChanges.Clear();
    }

    private sealed record TrainStateChange(int TrainId, TrainState State, DateTimeOffset Timestamp);
    private sealed record ObservedTimeChange(int CallId, TimeSpan Time, bool IsArrival, DateTimeOffset Timestamp);
    private sealed record TrackChange(int CallId, string NewTrackNumber, DateTimeOffset Timestamp);
}

/// <summary>
/// In-memory implementation of <see cref="IDispatchStateProvider"/> for testing.
/// </summary>
public class InMemoryDispatchStateProvider : IDispatchStateProvider
{
    private readonly List<DispatchStateChange> _stateChanges = [];
    private readonly List<PassChange> _passChanges = [];

    public bool HasSavedState => _stateChanges.Count > 0 || _passChanges.Count > 0;

    public Task RecordDispatchStateChangeAsync(int sectionId, DispatchState state, int? trackStretchIndex = null, CancellationToken cancellationToken = default)
    {
        _stateChanges.Add(new DispatchStateChange(sectionId, state, trackStretchIndex, DateTimeOffset.UtcNow));
        return Task.CompletedTask;
    }

    public Task RecordPassAsync(int sectionId, int signalControlledPlaceId, int newTrackStretchIndex, CancellationToken cancellationToken = default)
    {
        _passChanges.Add(new PassChange(sectionId, signalControlledPlaceId, newTrackStretchIndex, DateTimeOffset.UtcNow));
        return Task.CompletedTask;
    }

    public Task ApplyStateAsync(
        Dictionary<int, TrainSection> sections,
        Dictionary<int, Train> trains,
        Dictionary<int, TrainStationCall> calls,
        Dictionary<int, OperationPlace> operationPlaces,
        Dictionary<int, TrackStretch> trackStretches,
        CancellationToken cancellationToken = default)
    {
        foreach (var change in _stateChanges.OrderBy(c => c.Timestamp))
        {
            if (sections.TryGetValue(change.SectionId, out var section))
            {
                section.ApplyState(change.State, change.TrackStretchIndex ?? section.CurrentTrackStretchIndex);
            }
        }

        foreach (var change in _passChanges.OrderBy(c => c.Timestamp))
        {
            if (sections.TryGetValue(change.SectionId, out var section))
            {
                section.ApplyState(section.State, change.NewTrackStretchIndex);
            }
        }

        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        Clear();
        return Task.CompletedTask;
    }

    public void Clear()
    {
        _stateChanges.Clear();
        _passChanges.Clear();
    }

    private sealed record DispatchStateChange(int SectionId, DispatchState State, int? TrackStretchIndex, DateTimeOffset Timestamp);
    private sealed record PassChange(int SectionId, int SignalControlledPlaceId, int NewTrackStretchIndex, DateTimeOffset Timestamp);
}
