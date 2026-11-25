using Tellurian.Trains.Dispatch;
using Tellurian.Trains.Dispatch.Data;

namespace Tellurian.Trains.Dispatch.Server.Services;

/// <summary>
/// Extension methods for configuring time providers via dependency injection.
/// </summary>
public static class TimeProviderServiceExtensions
{
    /// <summary>
    /// Adds the <see cref="ITimeProvider"/> based on configuration.
    /// </summary>
    /// <remarks>
    /// Configuration in appsettings.json:
    /// <code>
    /// "TimeProvider": {
    ///   "Type": "Default",  // or "Fastclock"
    ///   "Fastclock": {
    ///     "ServerUrl": "https://fastclock.azurewebsites.net"
    ///   }
    /// }
    /// </code>
    /// </remarks>
    public static IServiceCollection AddTimeProvider(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection("TimeProvider");
        var providerType = section["Type"]?.ToLowerInvariant() ?? "";

        return providerType switch
        {
            "fastclock" => services.AddFastclockTimeProvider(section),
            _ => services.AddDefaultTimeProvider()
        };
    }

    private static IServiceCollection AddDefaultTimeProvider(this IServiceCollection services)
    {
        services.AddSingleton<ITimeProvider>(DefaultTimeProvider.Instance);
        return services;
    }

    private static IServiceCollection AddFastclockTimeProvider(this IServiceCollection services, IConfigurationSection section)
    {
        var fastclockSection = section.GetSection("Fastclock");
        var serverUrl = fastclockSection["ServerUrl"] ?? "https://fastclock.azurewebsites.net";
        var clockName = fastclockSection["ClockName"] ?? "Demo";

        services.AddSingleton<ITimeProvider>(sp =>
        {
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            return new TellurianFastClockTimeProvider(serverUrl, clockName, loggerFactory.CreateLogger<TellurianFastClockTimeProvider>());
        });

        return services;
    }
}
