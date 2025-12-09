using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using System.Text.Json.Serialization;
using Tellurian.Trains.Dispatch;
using Tellurian.Trains.Dispatch.Brokers;
using Tellurian.Trains.Dispatch.Trains;

namespace Tellurian.Trains.Dispatch.Data.Json;

/// <summary>
/// Implementation of <see cref="IBrokerStateProvider"/> that persists state to a JSON file.
/// </summary>
/// <remarks>
/// <para>
/// This provider saves state delta (changes from initial state) to keep files small and readable.
/// The state file captures:
/// <list type="bullet">
/// <item><description>Train states (Planned, Manned, Running, etc.)</description></item>
/// <item><description>Section dispatch states and current track stretch index</description></item>
/// <item><description>Observed times and track changes for calls</description></item>
/// </list>
/// </para>
/// <para>
/// On restart, the Broker should first load from <see cref="IBrokerDataProvider"/> (isRestart: false),
/// then call <see cref="ApplyStateAsync"/> to restore the saved state.
/// </para>
/// </remarks>
public class JsonFileBrokerStateProvider : IBrokerStateProvider
{
    private readonly string _stateFilePath;
    private readonly ILogger _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    /// <summary>
    /// Creates a new JSON file broker state provider.
    /// </summary>
    /// <param name="stateFilePath">Path to the JSON file for persisting state.</param>
    /// <param name="logger">Optional logger.</param>
    public JsonFileBrokerStateProvider(string stateFilePath, ILogger? logger = null)
    {
        _stateFilePath = stateFilePath;
        _logger = logger ?? NullLogger.Instance;
        _jsonOptions = CreateJsonOptions();
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        options.Converters.Add(new TimeSpanJsonConverter());
        return options;
    }

    public async Task<bool> SaveTrainsectionsAsync(IEnumerable<TrainSection> dispatchCalls, CancellationToken cancellationToken = default)
    {
        if (!_semaphore.Wait(0, cancellationToken))
            return false;

        try
        {
            var sections = dispatchCalls.ToList();
            var state = new JsonBrokerState
            {
                SavedAt = DateTimeOffset.UtcNow,
                TrainStates = ExtractTrainStates(sections),
                SectionStates = ExtractSectionStates(sections),
                CallStates = ExtractCallStates(sections)
            };

            var json = JsonSerializer.Serialize(state, _jsonOptions);
            await File.WriteAllTextAsync(_stateFilePath, json, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save broker state to {FilePath}", _stateFilePath);
            return false;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Reads train sections - returns empty collection.
    /// Use <see cref="ApplyStateAsync"/> after loading sections from data provider.
    /// </summary>
    public Task<IEnumerable<TrainSection>> ReadTrainSections(CancellationToken cancellationToken = default)
    {
        // Return empty - the Broker should use ApplyStateAsync after loading from data provider
        return Task.FromResult<IEnumerable<TrainSection>>([]);
    }

    /// <summary>
    /// Applies saved state to existing train sections.
    /// Call this after the Broker has loaded sections from <see cref="IBrokerDataProvider"/>.
    /// </summary>
    /// <param name="sections">Sections loaded from data provider.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if state was applied, false if no state file exists.</returns>
    public async Task<bool> ApplyStateAsync(IEnumerable<TrainSection> sections, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_stateFilePath))
            return false;

        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var json = await File.ReadAllTextAsync(_stateFilePath, cancellationToken);
            var state = JsonSerializer.Deserialize<JsonBrokerState>(json, _jsonOptions);

            if (state is null)
                return false;

            var sectionsList = sections.ToList();
            var sectionsById = sectionsList.ToDictionary(s => s.Id);
            var trains = sectionsList.Select(s => s.Departure.Train).DistinctBy(t => t.Id).ToDictionary(t => t.Id);
            var calls = sectionsList
                .SelectMany(s => new[] { s.Departure, s.Arrival })
                .DistinctBy(c => c.Id)
                .ToDictionary(c => c.Id);

            ApplyTrainStates(state.TrainStates, trains);
            ApplySectionStates(state.SectionStates, sectionsById);
            ApplyCallStates(state.CallStates, calls);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply broker state from {FilePath}", _stateFilePath);
            return false;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Checks if a state file exists.
    /// </summary>
    public bool HasSavedState => File.Exists(_stateFilePath);

    private static List<JsonTrainState> ExtractTrainStates(List<TrainSection> sections)
    {
        return sections
            .Select(s => s.Departure.Train)
            .DistinctBy(t => t.Id)
            .Select(t => new JsonTrainState
            {
                TrainId = t.Id,
                State = t.State
            })
            .ToList();
    }

    private static List<JsonTrainSectionState> ExtractSectionStates(List<TrainSection> sections)
    {
        return sections
            .Select(s => new JsonTrainSectionState
            {
                SectionId = s.Id,
                State = s.State,
                CurrentTrackStretchIndex = s.CurrentTrackStretchIndex
            })
            .ToList();
    }

    private static List<JsonCallState> ExtractCallStates(List<TrainSection> sections)
    {
        var calls = new Dictionary<int, JsonCallState>();

        foreach (var section in sections)
        {
            AddCallState(calls, section.Departure);
            AddCallState(calls, section.Arrival);
        }

        return calls.Values.ToList();
    }

    private static void AddCallState(Dictionary<int, JsonCallState> calls, TrainStationCall call)
    {
        if (calls.ContainsKey(call.Id))
            return;

        if (call.Observed.ArrivalTime != TimeSpan.Zero ||
            call.Observed.DepartureTime != TimeSpan.Zero ||
            call.NewTrack is not null)
        {
            calls[call.Id] = new JsonCallState
            {
                CallId = call.Id,
                ObservedArrivalTime = call.Observed.ArrivalTime != TimeSpan.Zero
                    ? call.Observed.ArrivalTime
                    : null,
                ObservedDepartureTime = call.Observed.DepartureTime != TimeSpan.Zero
                    ? call.Observed.DepartureTime
                    : null,
                NewTrackNumber = call.NewTrack?.Number
            };
        }
    }

    private static void ApplyTrainStates(List<JsonTrainState> trainStates, Dictionary<int, Train> trains)
    {
        foreach (var state in trainStates)
        {
            if (trains.TryGetValue(state.TrainId, out var train))
            {
                train.State = state.State;
            }
        }
    }

    private static void ApplySectionStates(List<JsonTrainSectionState> sectionStates, Dictionary<int, TrainSection> sections)
    {
        foreach (var state in sectionStates)
        {
            if (sections.TryGetValue(state.SectionId, out var section))
            {
                section.ApplyState(state.State, state.CurrentTrackStretchIndex);
            }
        }
    }

    private static void ApplyCallStates(List<JsonCallState> callStates, Dictionary<int, TrainStationCall> calls)
    {
        foreach (var state in callStates)
        {
            if (calls.TryGetValue(state.CallId, out var call))
            {
                if (state.ObservedArrivalTime.HasValue || state.ObservedDepartureTime.HasValue)
                {
                    call.ApplyObserved(new CallTime
                    {
                        ArrivalTime = state.ObservedArrivalTime ?? TimeSpan.Zero,
                        DepartureTime = state.ObservedDepartureTime ?? TimeSpan.Zero
                    });
                }

                if (state.NewTrackNumber is not null)
                {
                    var track = call.At.Tracks.FirstOrDefault(t => t.Number == state.NewTrackNumber);
                    if (track is not null)
                    {
                        call.ChangeTrack(track);
                    }
                }
            }
        }
    }
}
