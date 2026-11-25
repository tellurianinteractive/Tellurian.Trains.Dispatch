using Tellurian.Trains.Dispatch.Brokers;
using Tellurian.Trains.Dispatch.Data.Timetable;
using Tellurian.Trains.Schedules.Importers.Services;
using Tellurian.Trains.Schedules.Importers.Xpln.DataSetProviders;
using Tellurian.Trains.Schedules.Model;
using Tellurian.Trains.Schedules.Model.Timetables;

namespace Tellurian.Trains.Dispatch.Server.Services;

/// <summary>
/// Extension methods for configuring data providers via dependency injection.
/// </summary>
public static class DataProviderServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Adds the <see cref="IBrokerDataProvider"/> based on configuration.
        /// </summary>
        /// <remarks>
        /// Configuration in appsettings.json:
        /// <code>
        /// "DataProvider": {
        ///   "Source": "Xpln",  // or "Json"
        ///   "Xpln": {
        ///     "FilePath": "path/to/file.ods",
        ///     "OperatingCompaniesFilePath": "path/to/OperatingCompanies.json"  // optional
        ///   },
        ///   "Json": { "FilePath": "path/to/schedule.json" }
        /// }
        /// </code>
        /// </remarks>
        public IServiceCollection AddBrokerDataProvider(IConfiguration configuration)
        {
            var section = configuration.GetSection("DataProvider");
            var source = section["Source"]?.ToLowerInvariant() ?? "";

            return source switch
            {
                "xpln" => services.AddXplnDataProvider(section),
                "json" => services.AddJsonDataProvider(section),
                _ => services.AddEmptyDataProvider()
            };
        }

        private IServiceCollection AddXplnDataProvider(IConfigurationSection section)
        {
            var xplnSection = section.GetSection("Xpln");
            var filePath = xplnSection["FilePath"];
            var operatingCompaniesFilePath = xplnSection["OperatingCompaniesFilePath"];

            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new InvalidOperationException(
                    "DataProvider:Xpln:FilePath must be configured when Source is 'Xpln'");
            }

            // Register XPLN import services
            services.AddSingleton<OdsDataSetProvider>();
            services.AddSingleton<TrainCategoriesFromCsvService>();
            services.AddSingleton(sp =>
            {
                if (string.IsNullOrWhiteSpace(operatingCompaniesFilePath))
                    return new CompaniesFromJsonService();

                var env = sp.GetRequiredService<IWebHostEnvironment>();
                var fullPath = Path.IsPathRooted(operatingCompaniesFilePath)
                    ? operatingCompaniesFilePath
                    : Path.Combine(env.ContentRootPath, operatingCompaniesFilePath);
                return new CompaniesFromJsonService(fullPath);
            });
            services.AddSingleton<XplnTimetableLoader>();

            services.AddSingleton<IBrokerDataProvider>(sp =>
            {
                var env = sp.GetRequiredService<IWebHostEnvironment>();
                var loader = sp.GetRequiredService<XplnTimetableLoader>();

                var fullPath = Path.IsPathRooted(filePath)
                    ? filePath
                    : Path.Combine(env.ContentRootPath, filePath);

                return new TimetableBrokerDataProvider(() => loader.LoadAsync(fullPath));
            });

            return services;
        }

        private IServiceCollection AddJsonDataProvider(IConfigurationSection section)
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

                Task<Timetable> loader() => LoadJsonTimetableAsync(fullPath);

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

        private IServiceCollection AddEmptyDataProvider()
        {
            services.AddSingleton<IBrokerDataProvider, EmptyDataProvider>();
            return services;
        }

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
