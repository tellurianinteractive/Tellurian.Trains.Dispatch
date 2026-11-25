using Tellurian.Trains.Schedules.Importers.Services;
using Tellurian.Trains.Schedules.Importers.Xpln;
using Tellurian.Trains.Schedules.Importers.Xpln.DataSetProviders;
using Tellurian.Trains.Schedules.Model;
using Tellurian.Trains.Schedules.Model.Schedules;

namespace Tellurian.Trains.Dispatch.Server.Services;

/// <summary>
/// Loads timetables from XPLN ODS files.
/// </summary>
public sealed class XplnTimetableLoader(
    OdsDataSetProvider dataSetProvider,
    CompaniesFromJsonService companiesService,
    TrainCategoriesFromCsvService trainCategoriesService,
    ILogger<XplnDataImporter> importerLogger)
{
    private readonly OdsDataSetProvider _dataSetProvider = dataSetProvider;
    private readonly CompaniesFromJsonService _companiesService = companiesService;
    private readonly TrainCategoriesFromCsvService _trainCategoriesService = trainCategoriesService;
    private readonly ILogger<XplnDataImporter> _importerLogger = importerLogger;

    public async Task<Plan> LoadAsync(string filePath)
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

        using var importer = new XplnDataImporter(
            fileInfo,
            _dataSetProvider,
            _companiesService,
            _trainCategoriesService,
            _importerLogger);

        var result = await importer.ImportScheduleAsync(Path.GetFileNameWithoutExtension(filePath));

        if (!result.IsSuccess)
        {
            var errors = result.Messages
                .Where(m => m.Severity == Severity.Error)
                .Select(m => m.Text);
            throw new InvalidDataException($"XPLN import failed: {string.Join("; ", errors)}");
        }

        return result.Item;
    }
}
