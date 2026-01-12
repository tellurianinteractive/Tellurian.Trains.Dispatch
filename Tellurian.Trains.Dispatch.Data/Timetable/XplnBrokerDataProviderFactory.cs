using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Tellurian.Trains.Schedules.Importers.Services;
using Tellurian.Trains.Schedules.Importers.Xpln;
using Tellurian.Trains.Schedules.Importers.Xpln.DataSetProviders;
using Tellurian.Trains.Schedules.Model;

namespace Tellurian.Trains.Dispatch.Data.Timetable;

/// <summary>
/// Factory for creating <see cref="TimetableBrokerDataProvider"/> instances from XPLN ODS files.
/// </summary>
public static class XplnBrokerDataProviderFactory
{
    /// <summary>
    /// Creates a TimetableBrokerDataProvider from an XPLN ODS file.
    /// </summary>
    /// <param name="filePath">Path to the ODS file.</param>
    /// <param name="loggerFactory">Optional logger factory for import messages. Uses NullLoggerFactory if not provided.</param>
    /// <returns>A TimetableBrokerDataProvider initialized with data from the XPLN file.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    /// <exception cref="ArgumentException">Thrown when the file is not an ODS file.</exception>
    /// <exception cref="InvalidDataException">Thrown when the import fails.</exception>
    public static async Task<TimetableBrokerDataProvider> FromXplnAsync(
        string filePath,
        ILoggerFactory? loggerFactory = null)
    {
        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException($"XPLN file not found: {filePath}", filePath);
        }

        if (!fileInfo.Extension.Equals(".ods", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"File must be an ODS file. Got: {fileInfo.Extension}", nameof(filePath));
        }

        loggerFactory ??= NullLoggerFactory.Instance;

        var dataSetProvider = new OdsDataSetProvider(loggerFactory.CreateLogger<OdsDataSetProvider>());
        var companiesService = new CompaniesFromJsonService();
        var trainCategoriesService = new TrainCategoriesFromCsvService();
        var importLogger = loggerFactory.CreateLogger<XplnDataImporter>();

        using var importer = new XplnDataImporter(
            fileInfo,
            dataSetProvider,
            companiesService,
            trainCategoriesService,
            importLogger);

        var result = await importer.ImportScheduleAsync(Path.GetFileNameWithoutExtension(filePath));

        if (!result.IsSuccess)
        {
            var errorMessages = string.Join("; ", result.Messages.Where(m => m.Severity == Severity.Error).Select(m => m.Text));
            throw new InvalidDataException($"XPLN import failed: {errorMessages}");
        }

        return new TimetableBrokerDataProvider(result.Item.Timetable);
    }
}
