using Microsoft.Extensions.Logging;
using Tellurian.Trains.Dispatch;
using Tellurian.Trains.Dispatch.Brokers;
using Tellurian.Trains.Dispatch.Layout;
using Tellurian.Trains.Dispatch.Trains;

namespace Tellurian.Trains.Dispatch.Data.Csv;

/// <summary>
/// Composite state provider that coordinates both train and dispatch state providers.
/// </summary>
/// <remarks>
/// This provider combines <see cref="CsvTrainStateProvider"/> and <see cref="CsvDispatchStateProvider"/>
/// for simpler broker integration. Both providers share the same directory for their CSV files.
/// </remarks>
public class CsvCompositeStateProvider : ICompositeStateProvider, IDisposable
{
    private readonly CsvTrainStateProvider _trainStateProvider;
    private readonly CsvDispatchStateProvider _dispatchStateProvider;
    private bool _disposed;

    /// <summary>
    /// Creates a new composite state provider.
    /// </summary>
    /// <param name="directoryPath">Directory where CSV files will be stored.</param>
    /// <param name="logger">Optional logger (shared by both providers).</param>
    public CsvCompositeStateProvider(string directoryPath, ILogger? logger = null)
    {
        _trainStateProvider = new CsvTrainStateProvider(directoryPath, logger);
        _dispatchStateProvider = new CsvDispatchStateProvider(directoryPath, logger);
    }

    /// <inheritdoc />
    public ITrainStateProvider TrainStateProvider => _trainStateProvider;

    /// <inheritdoc />
    public IDispatchStateProvider DispatchStateProvider => _dispatchStateProvider;

    /// <inheritdoc />
    public bool HasAnySavedState => _trainStateProvider.HasSavedState || _dispatchStateProvider.HasSavedState;

    /// <inheritdoc />
    public async Task ApplyAllStateAsync(
        Dictionary<int, TrainSection> sections,
        Dictionary<int, Train> trains,
        Dictionary<int, TrainStationCall> calls,
        Dictionary<int, OperationPlace> operationPlaces,
        Dictionary<int, TrackStretch> trackStretches,
        CancellationToken cancellationToken = default)
    {
        // Apply train state first (train states), then dispatch state (section states)
        // Order matters because dispatch state may depend on train state
        await _trainStateProvider.ApplyStateAsync(sections, trains, calls, operationPlaces, trackStretches, cancellationToken).ConfigureAwait(false);
        await _dispatchStateProvider.ApplyStateAsync(sections, trains, calls, operationPlaces, trackStretches, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ClearAllAsync(CancellationToken cancellationToken = default)
    {
        await _trainStateProvider.ClearAsync(cancellationToken).ConfigureAwait(false);
        await _dispatchStateProvider.ClearAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;

        _trainStateProvider.Dispose();
        _dispatchStateProvider.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
