using System.Text.Json;
using Tellurian.Trains.Dispatch.Brokers;
using Tellurian.Trains.Dispatch.Layout;
using Tellurian.Trains.Dispatch.Trains;

namespace Tellurian.Trains.Dispatch.Data.Json;

/// <summary>
/// Implementation of <see cref="IBrokerDataProvider"/> that reads layout and train data from JSON files.
/// </summary>
/// <remarks>
/// <para>
/// This provider reads from two separate JSON files:
/// <list type="bullet">
/// <item><description>Layout file: Contains operation places, track stretches, and dispatch stretches</description></item>
/// <item><description>Train file: Contains companies, trains, and train station calls</description></item>
/// </list>
/// </para>
/// <para>
/// The separation allows layout data to be reused with different train schedules.
/// </para>
/// </remarks>
public class JsonFileBrokerDataProvider : IBrokerDataProvider
{
    private readonly string _layoutFilePath;
    private readonly string _trainFilePath;
    private readonly JsonSerializerOptions _jsonOptions;

    // Cached data after loading
    private JsonLayoutData? _layoutData;
    private JsonTrainData? _trainData;
    private Dictionary<int, OperationPlace>? _operationPlaces;
    private Dictionary<int, TrackStretch>? _trackStretches;
    private Dictionary<string, Company>? _companies;
    private Dictionary<int, Train>? _trains;

    /// <summary>
    /// Creates a new JSON file broker data provider.
    /// </summary>
    /// <param name="layoutFilePath">Path to the JSON file containing layout data.</param>
    /// <param name="trainFilePath">Path to the JSON file containing train data.</param>
    public JsonFileBrokerDataProvider(string layoutFilePath, string trainFilePath)
    {
        _layoutFilePath = layoutFilePath;
        _trainFilePath = trainFilePath;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            WriteIndented = true
        };
    }

    public async Task<IEnumerable<OperationPlace>> GetOperationPlacesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureLayoutDataLoadedAsync(cancellationToken);
        return _operationPlaces!.Values;
    }

    public async Task<IEnumerable<TrackStretch>> GetTrackStretchesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureLayoutDataLoadedAsync(cancellationToken);
        return _trackStretches!.Values;
    }

    public async Task<IEnumerable<DispatchStretch>> GetDispatchStretchesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureLayoutDataLoadedAsync(cancellationToken);

        var dispatchStretches = new List<DispatchStretch>();
        foreach (var ds in _layoutData!.DispatchStretches)
        {
            if (_operationPlaces!.TryGetValue(ds.FromStationId, out var from) && from is Station fromStation &&
                _operationPlaces.TryGetValue(ds.ToStationId, out var to) && to is Station toStation)
            {
                dispatchStretches.Add(new DispatchStretch(fromStation, toStation, _trackStretches!.Values)
                {
                    Id = ds.Id,
                    CssClass = ds.CssClass
                });
            }
            else
            {
                throw new InvalidDataException($"DispatchStretch {ds.Id}: FromStationId {ds.FromStationId} or ToStationId {ds.ToStationId} is not a valid Station.");
            }
        }

        return dispatchStretches;
    }

    public async Task<IEnumerable<Train>> GetTrainsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureTrainDataLoadedAsync(cancellationToken);
        return _trains!.Values;
    }

    public async Task<IEnumerable<TrainStationCall>> GetTrainStationCallsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureLayoutDataLoadedAsync(cancellationToken);
        await EnsureTrainDataLoadedAsync(cancellationToken);

        var calls = new List<TrainStationCall>();
        foreach (var call in _trainData!.TrainStationCalls)
        {
            if (!_trains!.TryGetValue(call.TrainId, out var train))
                throw new InvalidDataException($"TrainStationCall {call.Id}: TrainId {call.TrainId} not found.");

            if (!_operationPlaces!.TryGetValue(call.OperationPlaceId, out var operationPlace))
                throw new InvalidDataException($"TrainStationCall {call.Id}: OperationPlaceId {call.OperationPlaceId} not found.");

            var track = operationPlace.Tracks.FirstOrDefault(t => t.Number == call.TrackNumber);
            if (track is null)
                throw new InvalidDataException($"TrainStationCall {call.Id}: Track '{call.TrackNumber}' not found at {operationPlace.Signature}.");

            calls.Add(new TrainStationCall(train, operationPlace, call.GetCallTime())
            {
                Id = call.Id,
                PlannedTrack = track,
                IsArrival = call.IsArrival,
                IsDeparture = call.IsDeparture,
                SequenceNumber = call.SequenceNumber
            });
        }

        return calls;
    }

    private async Task EnsureLayoutDataLoadedAsync(CancellationToken cancellationToken)
    {
        if (_layoutData is not null) return;

        var json = await File.ReadAllTextAsync(_layoutFilePath, cancellationToken);
        _layoutData = JsonSerializer.Deserialize<JsonLayoutData>(json, _jsonOptions)
            ?? throw new InvalidDataException($"Failed to deserialize layout data from {_layoutFilePath}");

        // Build operation places
        _operationPlaces = [];
        foreach (var op in _layoutData.OperationPlaces)
        {
            var tracks = op.Tracks.Select(t => t.ToDomain()).ToList();
            OperationPlace place = op.Type.ToLowerInvariant() switch
            {
                "station" => new Station(op.Name, op.Signature)
                {
                    Id = op.Id,
                    IsManned = op.IsManned,
                    Tracks = tracks
                },
                "signalcontrolledplace" => new SignalControlledPlace(op.Name, op.Signature, op.ControlledByDispatcherId ?? 0)
                {
                    Id = op.Id,
                    IsJunction = op.IsJunction,
                    Tracks = tracks
                },
                "otherplace" => new OtherPlace(op.Name, op.Signature)
                {
                    Id = op.Id,
                    IsJunction = op.IsJunction,
                    Tracks = tracks
                },
                _ => throw new InvalidDataException($"Unknown operation place type: {op.Type}")
            };
            _operationPlaces[place.Id] = place;
        }

        // Build track stretches
        _trackStretches = [];
        foreach (var ts in _layoutData.TrackStretches)
        {
            if (!_operationPlaces.TryGetValue(ts.StartId, out var start))
                throw new InvalidDataException($"TrackStretch {ts.Id}: StartId {ts.StartId} not found.");
            if (!_operationPlaces.TryGetValue(ts.EndId, out var end))
                throw new InvalidDataException($"TrackStretch {ts.Id}: EndId {ts.EndId} not found.");

            var tracks = new List<Track>();
            for (var i = 0; i < ts.NumberOfTracks; i++)
            {
                tracks.Add(new Track((i + 1).ToString(), TrackOperationDirection.DoubleDirected));
            }

            var stretch = new TrackStretch(start, end, tracks)
            {
                Id = ts.Id,
                Length = ts.Length,
                CssClass = ts.CssClass
            };
            _trackStretches[stretch.Id] = stretch;
        }
    }

    private async Task EnsureTrainDataLoadedAsync(CancellationToken cancellationToken)
    {
        if (_trainData is not null) return;

        var json = await File.ReadAllTextAsync(_trainFilePath, cancellationToken);
        _trainData = JsonSerializer.Deserialize<JsonTrainData>(json, _jsonOptions)
            ?? throw new InvalidDataException($"Failed to deserialize train data from {_trainFilePath}");

        // Build companies
        _companies = _trainData.Companies.ToDictionary(c => c.Signature, c => c.ToDomain());

        // Build trains
        _trains = [];
        foreach (var t in _trainData.Trains)
        {
            if (!_companies.TryGetValue(t.CompanySignature, out var company))
                throw new InvalidDataException($"Train {t.Id}: Company '{t.CompanySignature}' not found.");

            var train = new Train(company, new Identity(t.IdentityPrefix, t.IdentityNumber))
            {
                Id = t.Id,
                MaxLength = t.MaxLength
            };
            _trains[train.Id] = train;
        }
    }
}
