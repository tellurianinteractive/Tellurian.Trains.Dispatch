using Tellurian.Trains.Dispatch;
using Tellurian.Trains.Dispatch.Brokers;
using Tellurian.Trains.Dispatch.Layout;
using Tellurian.Trains.Dispatch.Trains;

namespace Tellurian.Trains.Dispatch.Tests;

/// <summary>
/// Thread-safe in-memory state provider for tests.
/// </summary>
/// <remarks>
/// Only one operation at a time. If Save is already ongoing, returns immediately without saving.
/// </remarks>
internal class InMemoryStateProvider : IBrokerStateProvider
{
    private readonly List<TrainSection> _savedStretches = [];
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task<IEnumerable<TrainSection>> ReadDispatchCallsAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return [.. _savedStretches];
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public Task<bool> SaveDispatchCallsAsync(IEnumerable<TrainSection> dispatchCalls, CancellationToken cancellationToken = default)
    {
        if (!_semaphore.Wait(0, cancellationToken))
            return Task.FromResult(false);

        try
        {
            _savedStretches.Clear();
            _savedStretches.AddRange(dispatchCalls);
            return Task.FromResult(true);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}

/// <summary>
/// Simple time provider that returns scheduled time or current time.
/// </summary>
internal class TestTimeProvider : ITimeProvider
{
    public TimeSpan CurrentTime { get; set; } = TimeSpan.FromHours(10);

    public TimeSpan Time(TimeSpan? scheduledTime = null) =>
        scheduledTime ?? CurrentTime;
}
