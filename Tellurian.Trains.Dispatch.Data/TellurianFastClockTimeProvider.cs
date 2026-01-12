using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using Tellurian.Trains.MeetingApp.Contracts;

namespace Tellurian.Trains.Dispatch.Data;

/// <summary>
/// Time provider that fetches the current time from the Tellurian Fastclock service.
/// </summary>
/// <remarks>
/// The Tellurian Fastclock is a model railway fast clock service that can run at
/// accelerated speeds. This provider fetches the current fast clock time for use
/// in dispatch operations.
/// </remarks>
public sealed class TellurianFastClockTimeProvider : ITimeProvider, IDisposable
{
    private readonly string _serverUrl;
    private readonly string _clockName;
    private readonly ILogger<TellurianFastClockTimeProvider> _logger;
    private readonly HttpClient _httpClient;
    private bool _disposed;

    /// <summary>
    /// Creates a new TellurianFastClockTimeProvider.
    /// </summary>
    /// <param name="serverUrl">The base URL of the fastclock server (e.g., "https://fastclock.azurewebsites.net").</param>
    /// <param name="clockName">The name of the clock to use (default: "Demo").</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public TellurianFastClockTimeProvider(
        string serverUrl,
        string clockName,
        ILogger<TellurianFastClockTimeProvider> logger)
    {
        _serverUrl = serverUrl?.TrimEnd('/') ?? throw new ArgumentNullException(nameof(serverUrl));
        _clockName = clockName ?? "demo";
        _logger = logger;
        _httpClient = new HttpClient { BaseAddress = new Uri(_serverUrl) };
    }

    public async Task<TimeStatus> GetTimeStatusAsync(TimeSpan? scheduledTime = null)
    {
        try
        {
            var status = await GetClockStatusAsync().ConfigureAwait(false);

            if (status is null || status.IsUnavailable)
            {
                if (_logger.IsEnabled(LogLevel.Warning))
                    _logger.LogWarning("Fastclock unavailable, falling back to wall clock time");
                return new(null, false, false, "Waiting for better times");
            }

            if (TryParseTime(status.Time, out var time))
            {
                return new(time, status.IsRealtime, status.IsRunning, status.Message);
            }

            if (_logger.IsEnabled(LogLevel.Warning))
                _logger.LogWarning("Failed to parse fastclock time '{Time}', falling back to wall clock", status.Time);
            return new(null, false, false, "Waiting for better times");
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
                _logger.LogError(ex, "Error fetching fastclock time, falling back to wall clock");
            return new(null, false, false, "Waiting for better times");
        }
    }

    private async Task<ClockStatus?> GetClockStatusAsync()
    {
        var requestUri = $"/api/clocks/{_clockName}/time";
        return await _httpClient.GetFromJsonAsync<ClockStatus>(requestUri).ConfigureAwait(false);
    }

    private static bool TryParseTime(string? timeString, out TimeSpan time)
    {
        time = TimeSpan.Zero;

        if (string.IsNullOrWhiteSpace(timeString))
            return false;

        // Time is in "HH:MM" format
        var parts = timeString.Split(':');
        if (parts.Length >= 2 &&
            int.TryParse(parts[0], out var hours) &&
            int.TryParse(parts[1], out var minutes))
        {
            time = new TimeSpan(hours, minutes, 0);
            return true;
        }

        return false;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _httpClient.Dispose();
            _disposed = true;
        }
    }
}
