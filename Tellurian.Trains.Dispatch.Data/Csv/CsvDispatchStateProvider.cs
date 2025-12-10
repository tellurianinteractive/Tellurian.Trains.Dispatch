using CsvHelper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Tellurian.Trains.Dispatch;
using Tellurian.Trains.Dispatch.Brokers;
using Tellurian.Trains.Dispatch.Layout;
using Tellurian.Trains.Dispatch.Trains;

namespace Tellurian.Trains.Dispatch.Data.Csv;

/// <summary>
/// CSV-based implementation of <see cref="IDispatchStateProvider"/> that records dispatch state changes
/// to a CSV file for event-sourced state persistence.
/// </summary>
/// <remarks>
/// <para>
/// This provider records:
/// <list type="bullet">
/// <item><description>Dispatch state changes (Requested, Accepted, Rejected, Revoked, Departed, Arrived, Canceled)</description></item>
/// <item><description>Pass events when trains pass signal-controlled places</description></item>
/// </list>
/// </para>
/// <para>
/// The file remains open for appending during the session. State is restored by
/// reading all records and applying them in timestamp order.
/// </para>
/// </remarks>
public class CsvDispatchStateProvider : IDispatchStateProvider, IDisposable
{
    private const string DefaultFileName = "dispatch-state.csv";

    private readonly string _filePath;
    private readonly ILogger _logger;
    private StreamWriter? _streamWriter;
    private CsvWriter? _csvWriter;
    private readonly object _writeLock = new();
    private bool _disposed;

    /// <summary>
    /// Creates a new CSV dispatch state provider.
    /// </summary>
    /// <param name="directoryPath">Directory where the CSV file will be stored.</param>
    /// <param name="logger">Optional logger.</param>
    public CsvDispatchStateProvider(string directoryPath, ILogger? logger = null)
    {
        _filePath = Path.Combine(directoryPath, DefaultFileName);
        _logger = logger ?? NullLogger.Instance;
    }

    /// <inheritdoc />
    public bool HasSavedState => File.Exists(_filePath) && new FileInfo(_filePath).Length > 0;

    /// <inheritdoc />
    public async Task RecordDispatchStateChangeAsync(int sectionId, DispatchState state, int? trackStretchIndex = null, CancellationToken cancellationToken = default)
    {
        var record = CsvDispatchStateRecord.ForStateChange(sectionId, state, trackStretchIndex);
        await WriteRecordAsync(record).ConfigureAwait(false);
        _logger.LogDebug("Recorded section {SectionId} dispatch state change to {State}", sectionId, state);
    }

    /// <inheritdoc />
    public async Task RecordPassAsync(int sectionId, int signalControlledPlaceId, int newTrackStretchIndex, CancellationToken cancellationToken = default)
    {
        var record = CsvDispatchStateRecord.ForPass(sectionId, signalControlledPlaceId, newTrackStretchIndex);
        await WriteRecordAsync(record).ConfigureAwait(false);
        _logger.LogDebug("Recorded pass for section {SectionId} at signal place {SignalPlaceId}, new index {NewIndex}",
            sectionId, signalControlledPlaceId, newTrackStretchIndex);
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
            _logger.LogDebug("No saved dispatch state to apply");
            return;
        }

        var records = await CsvRecordExtensions.ReadDispatchStateRecordsAsync(_filePath, cancellationToken).ConfigureAwait(false);

        foreach (var record in records)
        {
            ApplyRecord(record, sections);
        }

        _logger.LogInformation("Applied {RecordCount} dispatch state records", records.Count);
    }

    private static void ApplyRecord(CsvDispatchStateRecord record, Dictionary<int, TrainSection> sections)
    {
        if (!sections.TryGetValue(record.SectionId, out var section))
            return;

        switch (record.ChangeType)
        {
            case DispatchStateChangeType.State when record.State.HasValue:
                section.ApplyState(record.State.Value, record.TrackStretchIndex ?? section.CurrentTrackStretchIndex);
                break;

            case DispatchStateChangeType.Pass when record.TrackStretchIndex.HasValue:
                section.ApplyState(section.State, record.TrackStretchIndex.Value);
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
            _logger.LogInformation("Cleared dispatch state file: {FilePath}", _filePath);
        }

        await Task.CompletedTask;
    }

    private async Task WriteRecordAsync(CsvDispatchStateRecord record)
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
            await _csvWriter.WriteDispatchStateHeaderAsync().ConfigureAwait(false);
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
