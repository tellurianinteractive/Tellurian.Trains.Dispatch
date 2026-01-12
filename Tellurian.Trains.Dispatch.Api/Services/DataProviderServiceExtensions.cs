using Tellurian.Trains.Dispatch.Brokers;
using Tellurian.Trains.Dispatch.Data.Timetable;
using Tellurian.Trains.Schedules.Importers.Services;
using Tellurian.Trains.Schedules.Importers.Xpln;
using Tellurian.Trains.Schedules.Importers.Xpln.DataSetProviders;
using Tellurian.Trains.Schedules.Model;

namespace Tellurian.Trains.Dispatch.Api.Services;

/// <summary>
/// Extension methods for configuring data providers via dependency injection.
/// </summary>
public static class DataProviderServiceExtensions
{
    /// <summary>
    /// Adds the <see cref="IBrokerDataProvider"/> based on configuration.
    /// </summary>
    /// <remarks>
    /// Configuration in appsettings.json:
    /// <code>
    /// "DataProvider": {
    ///   "Source": "Xpln",  // or "Json", "Database" when supported
    ///   "Xpln": { "FilePath": "path/to/file.ods" }
    /// }
    /// </code>
    /// </remarks>
    public static IServiceCollection AddBrokerDataProvider(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection("DataProvider");
        var source = section["Source"]?.ToLowerInvariant() ?? "";

        return source switch
        {
            "xpln" => services.AddXplnDataProvider(section),
            // Future sources can be added here:
            // "json" => services.AddJsonDataProvider(section),
            // "database" => services.AddDatabaseDataProvider(section),
            _ => services.AddEmptyDataProvider()
        };
    }

    private static IServiceCollection AddXplnDataProvider(this IServiceCollection services, IConfigurationSection section)
    {
        var filePath = section.GetSection("Xpln")["FilePath"];

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new InvalidOperationException(
                "DataProvider:Xpln:FilePath must be configured when Source is 'Xpln'");
        }

        services.AddSingleton<IBrokerDataProvider>(sp =>
        {
            var env = sp.GetRequiredService<IWebHostEnvironment>();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

            var fullPath = Path.IsPathRooted(filePath)
                ? filePath
                : Path.Combine(env.ContentRootPath, filePath);

            // Create loader function - timetable is loaded lazily when first needed
            Func<Task<Timetable>> loader = () => LoadXplnTimetableAsync(fullPath, loggerFactory);

            return new TimetableBrokerDataProvider(loader);
        });

        return services;
    }

    private static async Task<Timetable> LoadXplnTimetableAsync(string filePath, ILoggerFactory loggerFactory)
    {
        var fileInfo = new FileInfo(filePath);

        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException($"XPLN file not found: {filePath}", filePath);
        }

        if (!fileInfo.Extension.Equals(".ods", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"XPLN file must be an ODS file. Got: {fileInfo.Extension}");
        }

        var dataSetProvider = new OdsDataSetProvider(loggerFactory.CreateLogger<OdsDataSetProvider>());
        var companiesService = new CompaniesFromJsonService();
        var trainCategoriesService = new TrainCategoriesFromCsvService();

        using var importer = new XplnDataImporter(
            fileInfo,
            dataSetProvider,
            companiesService,
            trainCategoriesService,
            loggerFactory.CreateLogger<XplnDataImporter>());

        var result = await importer.ImportScheduleAsync(Path.GetFileNameWithoutExtension(filePath));

        if (!result.IsSuccess)
        {
            var errors = result.Messages
                .Where(m => m.Severity == Severity.Error)
                .Select(m => m.Text);
            throw new InvalidDataException($"XPLN import failed: {string.Join("; ", errors)}");
        }

        return result.Item.Timetable;
    }

    private static IServiceCollection AddEmptyDataProvider(this IServiceCollection services)
    {
        services.AddSingleton<IBrokerDataProvider, EmptyDataProvider>();
        return services;
    }
}

/// <summary>
/// Empty data provider for when no data source is configured.
/// </summary>
internal sealed class EmptyDataProvider : IBrokerDataProvider
{
    public Task<IEnumerable<Tellurian.Trains.Dispatch.Layout.OperationPlace>> GetOperationPlacesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IEnumerable<Tellurian.Trains.Dispatch.Layout.OperationPlace>>([]);

    public Task<IEnumerable<Tellurian.Trains.Dispatch.Layout.TrackStretch>> GetTrackStretchesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IEnumerable<Tellurian.Trains.Dispatch.Layout.TrackStretch>>([]);

    public Task<IEnumerable<Tellurian.Trains.Dispatch.Layout.DispatchStretch>> GetDispatchStretchesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IEnumerable<Tellurian.Trains.Dispatch.Layout.DispatchStretch>>([]);

    public Task<IEnumerable<Tellurian.Trains.Dispatch.Trains.Train>> GetTrainsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IEnumerable<Tellurian.Trains.Dispatch.Trains.Train>>([]);

    public Task<IEnumerable<Tellurian.Trains.Dispatch.Trains.TrainStationCall>> GetTrainStationCallsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IEnumerable<Tellurian.Trains.Dispatch.Trains.TrainStationCall>>([]);
}
