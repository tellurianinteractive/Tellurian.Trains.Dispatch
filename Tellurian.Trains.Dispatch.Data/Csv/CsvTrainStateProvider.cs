using CsvHelper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Tellurian.Trains.Dispatch;
using Tellurian.Trains.Dispatch.Brokers;
using Tellurian.Trains.Dispatch.Layout;
using Tellurian.Trains.Dispatch.Trains;

namespace Tellurian.Trains.Dispatch.Data.Csv;

/// <summary>
/// CSV-based implementation of <see cref="ITrainStateProvider"/> that records train state changes
/// to a CSV file for event-sourced state persistence.
/// </summary>
/// <remarks>
/// <para>
/// This provider records:
/// <list type="bullet">
/// <item><description>Train state changes (Planned, Manned, Running, Canceled, Aborted, Completed)</description></item>
/// <item><description>Observed arrival and departure times for station calls</description></item>
/// <item><description>Track changes when trains are assigned different tracks than planned</description></item>
/// </list>
/// </para>
/// <para>
/// The file remains open for appending during the session. State is restored by
/// reading all records and applying them in timestamp order.
/// </para>
/// </remarks>
public class CsvTrainStateProvider : ITrainStateProvider, IDisposable
{
    private const string DefaultFileName = "train-state.csv";

    private readonly string _filePath;
    private readonly ILogger _logger;
    private StreamWriter? _streamWriter;
    private CsvWriter? _csvWriter;
    private readonly object _writeLock = new();
    private bool _disposed;

    /// <summary>
    /// Creates a new CSV train state provider.
    /// </summary>
    /// <param name="directoryPath">Directory where the CSV file will be stored.</param>
    /// <param name="logger">Optional logger.</param>
    public CsvTrainStateProvider(string directoryPath, ILogger? logger = null)
    {
        _filePath = Path.Combine(directoryPath, DefaultFileName);
        _logger = logger ?? NullLogger.Instance;
    }

    /// <inheritdoc />
    public bool HasSavedState => File.Exists(_filePath) && new FileInfo(_filePath).Length > 0;

    /// <inheritdoc />
    public async Task RecordTrainStateChangeAsync(int trainId, TrainState state, CancellationToken cancellationToken = default)
    {
        var record = CsvTrainStateRecord.ForStateChange(trainId, state);
        await WriteRecordAsync(record).ConfigureAwait(false);
        _logger.LogDebug("Recorded train {TrainId} state change to {State}", trainId, state);
    }

    /// <inheritdoc />
    public async Task RecordObservedArrivalAsync(int callId, TimeSpan arrivalTime, CancellationToken cancellationToken = default)
    {
        // We need trainId for the record, but the approach says we reference by callId
        // For now, we'll set trainId to 0 and look it up during apply
        var record = CsvTrainStateRecord.ForObservedArrival(trainId: 0, callId, arrivalTime);
        await WriteRecordAsync(record).ConfigureAwait(false);
        _logger.LogDebug("Recorded observed arrival for call {CallId} at {ArrivalTime}", callId, arrivalTime);
    }

    /// <inheritdoc />
    public async Task RecordObservedDepartureAsync(int callId, TimeSpan departureTime, CancellationToken cancellationToken = default)
    {
        var record = CsvTrainStateRecord.ForObservedDeparture(trainId: 0, callId, departureTime);
        await WriteRecordAsync(record).ConfigureAwait(false);
        _logger.LogDebug("Recorded observed departure for call {CallId} at {DepartureTime}", callId, departureTime);
    }

    /// <inheritdoc />
    public async Task RecordTrackChangeAsync(int callId, string newTrackNumber, CancellationToken cancellationToken = default)
    {
        var record = CsvTrainStateRecord.ForTrackChange(trainId: 0, callId, newTrackNumber);
        await WriteRecordAsync(record).ConfigureAwait(false);
        _logger.LogDebug("Recorded track change for call {CallId} to track {NewTrack}", callId, newTrackNumber);
    }

    /// <inheritdoc />
    public async Task ApplyStateAsync(
        Dictionary<int, TrainSection> sections,
        Dictionary<int, Train> trains,
        Dictionary<int, TrainStationCall> calls,
        Dictionary<int, OperationPlace> operationPlaces,
        Dictionary<int, TrackStretch> trackStretches,
        CancellationToken cancellationToken = default)
    {
        if (!HasSavedState)
        {
            _logger.LogDebug("No saved train state to apply");
            return;
        }

        var records = await CsvRecordExtensions.ReadTrainStateRecordsAsync(_filePath, cancellationToken).ConfigureAwait(false);

        foreach (var record in records)
        {
            ApplyRecord(record, trains, calls, operationPlaces);
        }

        _logger.LogInformation("Applied {RecordCount} train state records", records.Count);
    }

    private static void ApplyRecord(
        CsvTrainStateRecord record,
        Dictionary<int, Train> trains,
        Dictionary<int, TrainStationCall> calls,
        Dictionary<int, OperationPlace> operationPlaces)
    {
        switch (record.ChangeType)
        {
            case TrainStateChangeType.State when record.State.HasValue:
                if (trains.TryGetValue(record.TrainId, out var train))
                {
                    train.State = record.State.Value;
                }
                break;

            case TrainStateChangeType.ObservedArrival when record.CallId.HasValue && record.Time.HasValue:
                if (calls.TryGetValue(record.CallId.Value, out var arrivalCall))
                {
                    arrivalCall.SetArrivalTime(record.Time.Value);
                }
                break;

            case TrainStateChangeType.ObservedDeparture when record.CallId.HasValue && record.Time.HasValue:
                if (calls.TryGetValue(record.CallId.Value, out var departureCall))
                {
                    departureCall.SetDepartureTime(record.Time.Value);
                }
                break;

            case TrainStateChangeType.TrackChange when record.CallId.HasValue && record.NewTrack is not null:
                if (calls.TryGetValue(record.CallId.Value, out var trackCall))
                {
                    // Use operationPlaces to resolve the station and find the track
                    var stationId = trackCall.At.Id;
                    if (operationPlaces.TryGetValue(stationId, out var place))
                    {
                        var newTrack = place.Tracks.FirstOrDefault(t => t.Number == record.NewTrack);
                        if (newTrack is not null)
                        {
                            trackCall.ChangeTrack(newTrack);
                        }
                    }
                }
                break;
        }
    }

    /// <inheritdoc />
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        CloseWriter();

        if (File.Exists(_filePath))
        {
            File.Delete(_filePath);
            _logger.LogInformation("Cleared train state file: {FilePath}", _filePath);
        }

        await Task.CompletedTask;
    }

    private async Task WriteRecordAsync(CsvTrainStateRecord record)
    {
        await EnsureWriterOpenAsync().ConfigureAwait(false);

        lock (_writeLock)
        {
            _csvWriter!.WriteRecord(record);
            _csvWriter.NextRecord();
            _csvWriter.Flush();
        }
    }

    private async Task EnsureWriterOpenAsync()
    {
        if (_csvWriter is not null)
            return;

        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var fileExists = File.Exists(_filePath) && new FileInfo(_filePath).Length > 0;

        _streamWriter = new StreamWriter(_filePath, append: true);
        _csvWriter = _streamWriter.CreateCsvWriter();

        if (!fileExists)
        {
            await _csvWriter.WriteTrainStateHeaderAsync().ConfigureAwait(false);
        }
    }

    private void CloseWriter()
    {
        lock (_writeLock)
        {
            _csvWriter?.Dispose();
            _csvWriter = null;
            _streamWriter?.Dispose();
            _streamWriter = null;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;

        CloseWriter();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
