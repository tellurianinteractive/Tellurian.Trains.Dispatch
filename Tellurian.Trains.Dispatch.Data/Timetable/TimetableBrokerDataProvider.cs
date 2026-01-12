using Tellurian.Trains.Dispatch.Brokers;
using Tellurian.Trains.Dispatch.Layout;
using Tellurian.Trains.Dispatch.Trains;
using DispatchTrackStretch = Tellurian.Trains.Dispatch.Layout.TrackStretch;
using DispatchTrain = Tellurian.Trains.Dispatch.Trains.Train;
using ModelTimetable = Tellurian.Trains.Schedules.Model.Timetable;

namespace Tellurian.Trains.Dispatch.Data.Timetable;

/// <summary>
/// Implementation of <see cref="IBrokerDataProvider"/> that converts data from
/// the <see cref="ModelTimetable"/> model to Dispatch domain types.
/// </summary>
/// <remarks>
/// This adapter enables using timetable data imported from XPLN spreadsheets
/// or other sources with the Dispatch broker.
/// </remarks>
public class TimetableBrokerDataProvider : IBrokerDataProvider
{
    private readonly ModelTimetable _timetable;

    // Cached converted data
    private Dictionary<int, OperationPlace>? _operationPlaces;
    private Dictionary<int, DispatchTrackStretch>? _trackStretches;
    private List<DispatchStretch>? _dispatchStretches;
    private Dictionary<int, DispatchTrain>? _trains;
    private List<TrainStationCall>? _trainStationCalls;

    // Mapping from model IDs to converted types
    private Dictionary<int, OperationPlace>? _operationPlacesByModelId;
    private Dictionary<int, Station>? _stationsByModelId;

    /// <summary>
    /// Creates a new TimetableBrokerDataProvider from a fully-loaded timetable.
    /// </summary>
    /// <param name="timetable">The timetable containing layout and train data.</param>
    public TimetableBrokerDataProvider(ModelTimetable timetable)
    {
        _timetable = timetable ?? throw new ArgumentNullException(nameof(timetable));
    }

    public Task<IEnumerable<OperationPlace>> GetOperationPlacesAsync(CancellationToken cancellationToken = default)
    {
        EnsureOperationPlacesConverted();
        return Task.FromResult<IEnumerable<OperationPlace>>(_operationPlaces!.Values);
    }

    public Task<IEnumerable<DispatchTrackStretch>> GetTrackStretchesAsync(CancellationToken cancellationToken = default)
    {
        EnsureOperationPlacesConverted();
        EnsureTrackStretchesConverted();
        return Task.FromResult<IEnumerable<DispatchTrackStretch>>(_trackStretches!.Values);
    }

    public Task<IEnumerable<DispatchStretch>> GetDispatchStretchesAsync(CancellationToken cancellationToken = default)
    {
        EnsureOperationPlacesConverted();
        EnsureTrackStretchesConverted();
        EnsureDispatchStretchesConverted();
        return Task.FromResult<IEnumerable<DispatchStretch>>(_dispatchStretches!);
    }

    public Task<IEnumerable<DispatchTrain>> GetTrainsAsync(CancellationToken cancellationToken = default)
    {
        EnsureTrainsConverted();
        return Task.FromResult<IEnumerable<DispatchTrain>>(_trains!.Values);
    }

    public Task<IEnumerable<TrainStationCall>> GetTrainStationCallsAsync(CancellationToken cancellationToken = default)
    {
        EnsureOperationPlacesConverted();
        EnsureTrainsConverted();
        EnsureTrainStationCallsConverted();
        return Task.FromResult<IEnumerable<TrainStationCall>>(_trainStationCalls!);
    }

    private void EnsureOperationPlacesConverted()
    {
        if (_operationPlaces is not null) return;

        _operationPlaces = [];
        _operationPlacesByModelId = [];
        _stationsByModelId = [];

        foreach (var location in _timetable.Layout.OperationLocations)
        {
            var place = location.ToOperationPlace();
            _operationPlaces[place.Id] = place;
            _operationPlacesByModelId[location.Id] = place;
            if (place is Station station)
            {
                _stationsByModelId[location.Id] = station;
            }
        }
    }

    private void EnsureTrackStretchesConverted()
    {
        if (_trackStretches is not null) return;

        _trackStretches = [];

        foreach (var modelStretch in _timetable.Layout.TrackStretches)
        {
            var dispatchStretch = modelStretch.ToDispatchTrackStretch(_operationPlacesByModelId!);
            if (dispatchStretch is not null)
            {
                _trackStretches[dispatchStretch.Id] = dispatchStretch;
            }
        }
    }

    private void EnsureDispatchStretchesConverted()
    {
        if (_dispatchStretches is not null) return;

        _dispatchStretches = [];

        foreach (var modelDispatchStretch in _timetable.Layout.DispatchStretches)
        {
            var dispatchStretch = modelDispatchStretch.ToDispatchStretch(
                _stationsByModelId!,
                _trackStretches!.Values);

            if (dispatchStretch is not null)
            {
                _dispatchStretches.Add(dispatchStretch);
            }
        }
    }

    private void EnsureTrainsConverted()
    {
        if (_trains is not null) return;

        _trains = [];

        foreach (var modelTrain in _timetable.Trains)
        {
            var dispatchTrain = modelTrain.ToDispatchTrain();
            _trains[dispatchTrain.Id] = dispatchTrain;
        }
    }

    private void EnsureTrainStationCallsConverted()
    {
        if (_trainStationCalls is not null) return;

        _trainStationCalls = [];

        // Build mapping from model train to dispatch train by identity
        var trainMapping = BuildTrainMapping();

        foreach (var modelTrain in _timetable.Trains)
        {
            if (!trainMapping.TryGetValue(modelTrain.Id, out var dispatchTrain))
                continue;

            var sequenceNumber = 0;
            foreach (var modelCall in modelTrain.Calls.OrderBy(c => c.Departure.Value))
            {
                var trainStationCall = modelCall.ToTrainStationCall(
                    dispatchTrain,
                    _operationPlacesByModelId!,
                    sequenceNumber);

                if (trainStationCall is not null)
                {
                    _trainStationCalls.Add(trainStationCall);
                    sequenceNumber++;
                }
            }
        }
    }

    private Dictionary<int, DispatchTrain> BuildTrainMapping()
    {
        var mapping = new Dictionary<int, DispatchTrain>();
        var modelTrains = _timetable.Trains.ToList();
        var dispatchTrains = _trains!.Values.ToList();

        foreach (var modelTrain in modelTrains)
        {
            var dispatchTrain = dispatchTrains.FirstOrDefault(t =>
                t.Identity.Number == modelTrain.Number &&
                t.Identity.Prefix == (modelTrain.Category?.Prefix ?? ""));

            if (dispatchTrain is not null)
            {
                mapping[modelTrain.Id] = dispatchTrain;
            }
        }

        return mapping;
    }
}
