using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Tellurian.Trains.Dispatch.Data.Database;
using Tellurian.Trains.Schedules.Model.Databases;
using Tellurian.Trains.Schedules.Model.Schedules;

namespace Tellurian.Trains.Dispatch.Server.Services;

/// <summary>
/// Settings for running from a SQLite schedule database.
/// </summary>
public sealed record DatabaseScheduleSettings
{
    /// <summary>Path to the SQLite database file.</summary>
    public required string DatabasePath { get; init; }

    /// <summary>The plan to use; null or empty to use the only plan in the database.</summary>
    public string? PlanName { get; init; }

    /// <summary>
    /// Optional path to a plan file exported from the Timetable Planner. It is imported into the database
    /// when the database does not exist or the plan file is newer than the database.
    /// </summary>
    public string? ImportFilePath { get; init; }
}

/// <summary>
/// Loads the timetable from a SQLite schedule database, first importing a plan file when configured and needed.
/// </summary>
public sealed class DatabaseScheduleLoader(DatabaseScheduleSettings settings, ILogger<DatabaseScheduleLoader> logger)
{
    private readonly DatabaseScheduleSettings _settings = settings;
    private readonly ILogger<DatabaseScheduleLoader> _logger = logger;

    public ScheduleDatabase Database { get; } = CreateDatabase(settings.DatabasePath);

    public static ScheduleDatabase CreateDatabase(string databasePath)
    {
        var connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString();
        var options = new DbContextOptionsBuilder<ScheduleDbContext>().UseSqlite(connectionString).Options;
        return new ScheduleDatabase(options);
    }

    public async Task<Plan> LoadAsync(CancellationToken cancellationToken = default)
    {
        await ImportIfNeededAsync(cancellationToken);
        if (!File.Exists(_settings.DatabasePath))
        {
            throw new FileNotFoundException($"Schedule database not found: {_settings.DatabasePath}", _settings.DatabasePath);
        }
        var plan = await Database.LoadPlanAsync(_settings.PlanName, cancellationToken);
        var timetable = plan.Timetable;
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Loaded timetable {Timetable} with {TrainCount} trains for {OperatingDays} operating days from {DatabasePath}",
                timetable.Name, timetable.Trains.Count, timetable.Layout.Settings.General.MaxSessions, _settings.DatabasePath);
        }
        return plan;
    }

    private async Task ImportIfNeededAsync(CancellationToken cancellationToken)
    {
        var importFile = _settings.ImportFilePath;
        if (string.IsNullOrWhiteSpace(importFile)) return;
        if (!File.Exists(importFile))
        {
            _logger.LogWarning("Plan file to import not found: {ImportFilePath}", importFile);
            return;
        }
        var database = new FileInfo(_settings.DatabasePath);
        if (database.Exists && database.LastWriteTimeUtc >= File.GetLastWriteTimeUtc(importFile)) return;

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Importing plan file {ImportFilePath} into {DatabasePath}", importFile, _settings.DatabasePath);
        }
        database.Directory?.Create();
        var result = await Database.ImportPlanFileAsync(importFile, cancellationToken);
        if (result.IsFail)
        {
            throw new InvalidDataException($"Import of '{importFile}' failed: {result.Error}");
        }
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Imported plan {PlanName}", result.Value!.Name);
        }
    }
}
