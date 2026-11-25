using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Tellurian.Trains.Dispatch.Brokers;
using Tellurian.Trains.Dispatch.Data.Timetable;
using Tellurian.Trains.Dispatch.Utilities;
using Tellurian.Trains.Schedules.Model.Schedules;
using Tellurian.Trains.Schedules.Model.Databases;
using Tellurian.Trains.Schedules.Model.PlanFiles;
using ModelTimetable = Tellurian.Trains.Schedules.Model.Timetables.Timetable;

namespace Tellurian.Trains.Dispatch.Data.Database;

/// <summary>
/// A database holding one complete schedule (a <see cref="Plan"/>), for example exported from the Timetable Planner.
/// </summary>
/// <remarks>
/// The database is provider agnostic: pass options for SQLite, SQL Server or any other EF Core provider.
/// The database holds one plan; importing replaces whatever it held.
/// </remarks>
/// <param name="options">Options for the <see cref="ScheduleDbContext"/>.</param>
public sealed class ScheduleDatabase(DbContextOptions<ScheduleDbContext> options)
{
    private readonly DbContextOptions<ScheduleDbContext> _options = options ?? throw new ArgumentNullException(nameof(options));

    /// <summary>
    /// Imports a plan file (the JSON file saved by the Timetable Planner) into the database,
    /// replacing any plan it held.
    /// </summary>
    /// <param name="planFilePath">Path to the plan file.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>The imported plan, or the errors.</returns>
    public async Task<Option<Plan>> ImportPlanFileAsync(string planFilePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planFilePath);
        if (!File.Exists(planFilePath)) return Option<Plan>.Fail($"Plan file not found: {planFilePath}");

        Plan plan;
        try
        {
            await using var stream = File.OpenRead(planFilePath);
            plan = await PlanFile.ReadAsync(stream, cancellationToken).ConfigureAwait(false);
        }
        catch (PlanFileException ex)
        {
            return Option<Plan>.Fail($"Cannot read plan file '{planFilePath}': {ex.Message}");
        }
        return await ImportPlanAsync(plan).ConfigureAwait(false);
    }

    /// <summary>
    /// Imports a plan into the database, replacing any plan it held.
    /// </summary>
    /// <param name="plan">The plan to import.</param>
    /// <returns>The imported plan, or the errors.</returns>
    public async Task<Option<Plan>> ImportPlanAsync(Plan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var result = await new DatabaseExportService(_options).ExportScheduleAsync(plan).ConfigureAwait(false);
        return result.IsSuccess && result.Item is not null
            ? Option<Plan>.Success(result.Item)
            : Option<Plan>.Fail([.. result.Messages.Select(m => m.Text)]);
    }

    /// <summary>
    /// Gets the names of the plans in the database.
    /// </summary>
    /// <returns>The plan names; empty if the database does not exist or has no schema.</returns>
    public async Task<IReadOnlyList<string>> GetPlanNamesAsync(CancellationToken cancellationToken = default)
    {
        await using var context = new ScheduleDbContext(_options);
        try
        {
            return await context.Plans.Select(p => p.Name).ToListAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbException)
        {
            return [];
        }
    }

    /// <summary>
    /// Reads the complete plan from the database.
    /// </summary>
    /// <param name="planName">The name of the plan, or null to read the only plan in the database.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>The plan, or the errors.</returns>
    public async Task<Option<Plan>> ReadPlanAsync(string? planName = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(planName))
        {
            var names = await GetPlanNamesAsync(cancellationToken).ConfigureAwait(false);
            if (names.Count != 1)
            {
                return Option<Plan>.Fail(names.Count == 0
                    ? "The database holds no plan."
                    : $"The database holds {names.Count} plans; specify which one to use.");
            }
            planName = names[0];
        }

        await using var context = new ScheduleDbContext(_options);
        var plan = await context.ReadPlanAsync(planName, cancellationToken).ConfigureAwait(false);
        return plan is null
            ? Option<Plan>.Fail($"There is no plan named '{planName}' in the database.")
            : Option<Plan>.Success(plan);
    }

    /// <summary>
    /// Reads the timetable of the plan in the database.
    /// </summary>
    /// <param name="planName">The name of the plan, or null to read the only plan in the database.</param>
    /// <param name="cancellationToken"></param>
    /// <exception cref="InvalidDataException">The plan could not be read.</exception>
    public async Task<ModelTimetable> LoadTimetableAsync(string? planName = null, CancellationToken cancellationToken = default)
    {
        var plan = await LoadPlanAsync(planName, cancellationToken).ConfigureAwait(false);
        return plan.Timetable;
    }

    /// <summary>
    /// Creates an <see cref="IBrokerDataProvider"/> that reads its data from this database when first needed.
    /// </summary>
    /// <param name="planName">The name of the plan, or null to read the only plan in the database.</param>
    public IBrokerDataProvider CreateBrokerDataProvider(string? planName = null) =>
        new TimetableBrokerDataProvider(() => LoadPlanAsync(planName));

    /// <summary>
    /// Reads the plan in the database.
    /// </summary>
    /// <param name="planName">The name of the plan, or null to read the only plan in the database.</param>
    /// <param name="cancellationToken"></param>
    /// <exception cref="InvalidDataException">The plan could not be read.</exception>
    public async Task<Plan> LoadPlanAsync(string? planName = null, CancellationToken cancellationToken = default)
    {
        var plan = await ReadPlanAsync(planName, cancellationToken).ConfigureAwait(false);
        return plan.HasValue ? plan.Value! : throw new InvalidDataException(plan.Error);
    }
}
