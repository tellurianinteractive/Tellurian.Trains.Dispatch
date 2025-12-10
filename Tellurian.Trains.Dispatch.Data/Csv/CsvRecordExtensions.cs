using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;

namespace Tellurian.Trains.Dispatch.Data.Csv;

/// <summary>
/// Extension methods for reading and writing CSV state records using CsvHelper.
/// </summary>
public static class CsvRecordExtensions
{
    private static readonly CsvConfiguration CsvConfig = new(CultureInfo.InvariantCulture)
    {
        HasHeaderRecord = true,
        MissingFieldFound = null,
        HeaderValidated = null
    };

    /// <summary>
    /// Creates a CsvWriter configured for state records, appending to an existing stream.
    /// </summary>
    public static CsvWriter CreateCsvWriter(this StreamWriter writer)
    {
        return new CsvWriter(writer, CsvConfig, leaveOpen: true);
    }

    /// <summary>
    /// Writes a single train state record to the CSV file.
    /// </summary>
    public static async Task WriteRecordAsync(this CsvWriter writer, CsvTrainStateRecord record)
    {
        writer.WriteRecord(record);
        await writer.NextRecordAsync().ConfigureAwait(false);
        await writer.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Writes a single dispatch state record to the CSV file.
    /// </summary>
    public static async Task WriteRecordAsync(this CsvWriter writer, CsvDispatchStateRecord record)
    {
        writer.WriteRecord(record);
        await writer.NextRecordAsync().ConfigureAwait(false);
        await writer.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Reads all train state records from a CSV file.
    /// </summary>
    public static async Task<IReadOnlyList<CsvTrainStateRecord>> ReadTrainStateRecordsAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            return [];

        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, CsvConfig);

        var records = new List<CsvTrainStateRecord>();
        await foreach (var record in csv.GetRecordsAsync<CsvTrainStateRecord>(cancellationToken).ConfigureAwait(false))
        {
            records.Add(record);
        }

        return records.OrderBy(r => r.Timestamp).ToList();
    }

    /// <summary>
    /// Reads all dispatch state records from a CSV file.
    /// </summary>
    public static async Task<IReadOnlyList<CsvDispatchStateRecord>> ReadDispatchStateRecordsAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            return [];

        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, CsvConfig);

        var records = new List<CsvDispatchStateRecord>();
        await foreach (var record in csv.GetRecordsAsync<CsvDispatchStateRecord>(cancellationToken).ConfigureAwait(false))
        {
            records.Add(record);
        }

        return records.OrderBy(r => r.Timestamp).ToList();
    }

    /// <summary>
    /// Writes the CSV header for train state records.
    /// </summary>
    public static async Task WriteTrainStateHeaderAsync(this CsvWriter writer)
    {
        writer.WriteHeader<CsvTrainStateRecord>();
        await writer.NextRecordAsync().ConfigureAwait(false);
        await writer.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Writes the CSV header for dispatch state records.
    /// </summary>
    public static async Task WriteDispatchStateHeaderAsync(this CsvWriter writer)
    {
        writer.WriteHeader<CsvDispatchStateRecord>();
        await writer.NextRecordAsync().ConfigureAwait(false);
        await writer.FlushAsync().ConfigureAwait(false);
    }
}
