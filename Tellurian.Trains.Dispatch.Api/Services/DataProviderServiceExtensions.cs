using Microsoft.EntityFrameworkCore;
using Tellurian.Trains.Dispatch.Brokers;
using Tellurian.Trains.Dispatch.Data.Timetable;
using Tellurian.Trains.Schedules.Importers.Services;
using Tellurian.Trains.Schedules.Importers.Xpln;
using Tellurian.Trains.Schedules.Importers.Xpln.DataSetProviders;
using Tellurian.Trains.Schedules.Model;
using Tellurian.Trains.Schedules.Model.Databases;

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
    ///   "Source": "Xpln",  // or "Json", "Database"
    ///   "Xpln": { "FilePath": "path/to/file.ods" },
    ///   "Json": { "FilePath": "path/to/schedule.json" },
    ///   "Database": { "ConnectionString": "Data Source=schedule.db" }
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
            "json" => services.AddJsonDataProvider(section),
            "database" => services.AddDatabaseDataProvider(section),
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

    private static IServiceCollection AddJsonDataProvider(this IServiceCollection services, IConfigurationSection section)
    {
        var filePath = section.GetSection("Json")["FilePath"];

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new InvalidOperationException(
                "DataProvider:Json:FilePath must be configured when Source is 'Json'");
        }

        services.AddSingleton<IBrokerDataProvider>(sp =>
        {
            var env = sp.GetRequiredService<IWebHostEnvironment>();

            var fullPath = Path.IsPathRooted(filePath)
                ? filePath
                : Path.Combine(env.ContentRootPath, filePath);

            Func<Task<Timetable>> loader = () => LoadJsonTimetableAsync(fullPath);

            return new TimetableBrokerDataProvider(loader);
        });

        return services;
    }

    private static async Task<Timetable> LoadJsonTimetableAsync(string filePath)
    {
        var fileInfo = new FileInfo(filePath);

        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException($"JSON file not found: {filePath}", filePath);
        }

        if (!fileInfo.Extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"JSON file must have .json extension. Got: {fileInfo.Extension}");
        }

        var importer = new JsonImportService(fileInfo);
        var result = await importer.ImportScheduleAsync(Path.GetFileNameWithoutExtension(filePath));

        if (!result.IsSuccess)
        {
            var errors = result.Messages
                .Where(m => m.Severity == Severity.Error)
                .Select(m => m.Text);
            throw new InvalidDataException($"JSON import failed: {string.Join("; ", errors)}");
        }

        return result.Item.Timetable;
    }

    private static IServiceCollection AddDatabaseDataProvider(this IServiceCollection services, IConfigurationSection section)
    {
        var connectionString = section.GetSection("Database")["ConnectionString"];
        var scheduleName = section.GetSection("Database")["ScheduleName"] ?? "Default";

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "DataProvider:Database:ConnectionString must be configured when Source is 'Database'");
        }

        services.AddSingleton<IBrokerDataProvider>(sp =>
        {
            Func<Task<Timetable>> loader = () => LoadDatabaseTimetableAsync(connectionString, scheduleName);

            return new TimetableBrokerDataProvider(loader);
        });

        return services;
    }

    private static async Task<Timetable> LoadDatabaseTimetableAsync(string connectionString, string scheduleName)
    {
        var options = new DbContextOptionsBuilder<ScheduleDbContext>()
            .UseSqlite(connectionString)
            .Options;

        await using var context = new ScheduleDbContext(options);

        var schedule = await context.Schedules
            .Include(s => s.Timetable)
                .ThenInclude(t => t.Layout)
                    .ThenInclude(l => l.OperationLocations)
                        .ThenInclude(ol => ol.Tracks)
            .Include(s => s.Timetable)
                .ThenInclude(t => t.Layout)
                    .ThenInclude(l => l.TrackStretches)
            .Include(s => s.Timetable)
                .ThenInclude(t => t.Layout)
                    .ThenInclude(l => l.DispatchStretches)
            .Include(s => s.Timetable)
                .ThenInclude(t => t.Trains)
                    .ThenInclude(tr => tr.Calls)
            .FirstOrDefaultAsync(s => s.Name == scheduleName);

        if (schedule is null)
        {
            throw new InvalidOperationException($"Schedule '{scheduleName}' not found in database");
        }

        return schedule.Timetable;
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
