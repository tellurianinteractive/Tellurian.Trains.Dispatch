using Tellurian.Trains.Dispatch;
using Tellurian.Trains.Dispatch.Brokers;

namespace Tellurian.Trains.Dispatch.Data;

/// <summary>
/// Thread-safe in-memory state provider.
/// Useful for testing and scenarios where persistence is not required.
/// </summary>
/// <remarks>
/// Only one operation at a time. If Save is already ongoing, returns immediately without saving.
/// </remarks>
public class InMemoryStateProvider : IBrokerStateProvider
{
    private readonly List<TrainSection> _savedSections = [];
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task<IEnumerable<TrainSection>> ReadTrainSections(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return [.. _savedSections];
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public Task<bool> SaveTrainsectionsAsync(IEnumerable<TrainSection> dispatchCalls, CancellationToken cancellationToken = default)
    {
        if (!_semaphore.Wait(0, cancellationToken))
            return Task.FromResult(false);

        try
        {
            _savedSections.Clear();
            _savedSections.AddRange(dispatchCalls);
            return Task.FromResult(true);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Clears all saved state.
    /// </summary>
    public void Clear()
    {
        _semaphore.Wait();
        try
        {
            _savedSections.Clear();
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
