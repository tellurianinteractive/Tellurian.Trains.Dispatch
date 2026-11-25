using CsvHelper;

namespace Tellurian.Trains.Dispatch.Data.Csv;

/// <summary>
/// Extension methods for reading and writing CSV state records using CsvHelper.
/// </summary>
/// <remarks>
/// The CsvHelper defaults are used: invariant culture, a header row, and missing fields read as empty.
/// Record mapping is source generated from the <see cref="CsvRecordAttribute"/> on the record types.
/// </remarks>
public static class CsvRecordExtensions
{
    extension(StreamWriter writer)
    {
        /// <summary>
        /// Creates a CsvWriter configured for state records, appending to an existing stream.
        /// </summary>
        public CsvWriter CreateCsvWriter() =>
            CsvWriter.Create(writer, CsvWriterOptions.Default, leaveOpen: true);
    }

    extension(CsvWriter writer)
    {
        /// <summary>
        /// Writes a single train state record to the CSV file.
        /// </summary>
        public async Task WriteRecordAsync(CsvTrainStateRecord record)
        {
            await writer.WriteRecordAsync(record, CancellationToken.None).ConfigureAwait(false);
            await writer.FlushAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Writes a single dispatch state record to the CSV file.
        /// </summary>
        public async Task WriteRecordAsync(CsvDispatchStateRecord record)
        {
            await writer.WriteRecordAsync(record, CancellationToken.None).ConfigureAwait(false);
            await writer.FlushAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Writes the CSV header for train state records.
        /// </summary>
        public async Task WriteTrainStateHeaderAsync()
        {
            writer.WriteHeader<CsvTrainStateRecord>();
            await writer.FlushAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Writes the CSV header for dispatch state records.
        /// </summary>
        public async Task WriteDispatchStateHeaderAsync()
        {
            writer.WriteHeader<CsvDispatchStateRecord>();
            await writer.FlushAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Reads all train state records from a CSV file.
    /// </summary>
    public static Task<IReadOnlyList<CsvTrainStateRecord>> ReadTrainStateRecordsAsync(string filePath, CancellationToken cancellationToken = default) =>
        ReadRecordsAsync<CsvTrainStateRecord>(filePath, r => r.Timestamp, cancellationToken);

    /// <summary>
    /// Reads all dispatch state records from a CSV file.
    /// </summary>
    public static Task<IReadOnlyList<CsvDispatchStateRecord>> ReadDispatchStateRecordsAsync(string filePath, CancellationToken cancellationToken = default) =>
        ReadRecordsAsync<CsvDispatchStateRecord>(filePath, r => r.Timestamp, cancellationToken);

    private static async Task<IReadOnlyList<T>> ReadRecordsAsync<T>(string filePath, Func<T, DateTimeOffset> timestamp, CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
            return [];

        using var reader = new StreamReader(filePath);
        await using var csv = CsvReader.Create(reader, CsvReaderOptions.Default);

        var records = new List<T>();
        await foreach (var record in csv.GetRecordsAsync<T>(cancellationToken).ConfigureAwait(false))
        {
            records.Add(record);
        }

        return records.OrderBy(timestamp).ToList();
    }
}
