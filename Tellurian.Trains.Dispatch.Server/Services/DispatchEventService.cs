using System.Threading.Channels;
using Tellurian.Trains.Dispatch.Shared;

namespace Tellurian.Trains.Dispatch.Server.Services;

/// <summary>
/// Service for broadcasting dispatch events to connected SSE clients.
/// Uses a bounded channel for efficient pub/sub pattern.
/// </summary>
public sealed class DispatchEventService : IDisposable
{
    private readonly Channel<DispatchEvent> _channel;
    private readonly List<Channel<DispatchEvent>> _subscribers = [];
    private readonly Lock _subscribersLock = new();
    private readonly ILogger<DispatchEventService> _logger;

    public DispatchEventService(ILogger<DispatchEventService> logger)
    {
        _logger = logger;
        _channel = Channel.CreateBounded<DispatchEvent>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });

        // Start the broadcast loop
        _ = BroadcastLoopAsync();
    }

    /// <summary>
    /// Publishes an event to all connected subscribers.
    /// </summary>
    public async ValueTask PublishAsync(DispatchEvent evt, CancellationToken cancellationToken = default)
    {
        await _channel.Writer.WriteAsync(evt, cancellationToken);
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Published event {EventType} for train section {SectionId}", evt.EventType, evt.TrainSectionId);
        }
    }

    /// <summary>
    /// Gets an async enumerable of events, optionally filtered by dispatcher ID.
    /// </summary>
    public async IAsyncEnumerable<DispatchEvent> GetEventsAsync(
        int? dispatcherId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var subscriberChannel = Channel.CreateBounded<DispatchEvent>(new BoundedChannelOptions(50)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });

        lock (_subscribersLock)
        {
            _subscribers.Add(subscriberChannel);
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("New SSE subscriber connected (dispatcher filter: {DispatcherId})", dispatcherId?.ToString() ?? "none");
        }

        try
        {
            await foreach (var evt in subscriberChannel.Reader.ReadAllAsync(cancellationToken))
            {
                // Filter by dispatcher ID if specified
                if (dispatcherId is null || evt.AffectedDispatcherIds.Contains(dispatcherId.Value))
                {
                    yield return evt;
                }
            }
        }
        finally
        {
            lock (_subscribersLock)
            {
                _subscribers.Remove(subscriberChannel);
            }
            _logger.LogInformation("SSE subscriber disconnected");
        }
    }

    private async Task BroadcastLoopAsync()
    {
        await foreach (var evt in _channel.Reader.ReadAllAsync())
        {
            List<Channel<DispatchEvent>> currentSubscribers;
            lock (_subscribersLock)
            {
                currentSubscribers = [.. _subscribers];
            }

            foreach (var subscriber in currentSubscribers)
            {
                try
                {
                    // Use TryWrite to avoid blocking if a subscriber is slow
                    if (!subscriber.Writer.TryWrite(evt))
                    {
                        _logger.LogWarning("Dropped event for slow subscriber");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error writing to subscriber");
                }
            }
        }
    }

    public void Dispose()
    {
        _channel.Writer.Complete();
        lock (_subscribersLock)
        {
            foreach (var subscriber in _subscribers)
            {
                subscriber.Writer.Complete();
            }
            _subscribers.Clear();
        }
    }
}
