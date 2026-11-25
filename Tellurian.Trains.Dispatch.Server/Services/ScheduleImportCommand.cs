namespace Tellurian.Trains.Dispatch.Server.Services;

/// <summary>
/// Command line verb that imports a Timetable Planner plan file into a SQLite schedule database.
/// </summary>
/// <remarks>
/// Usage: <c>Tellurian.Trains.Dispatch.Server import &lt;plan file&gt; [&lt;database file&gt;]</c>.
/// When no database file is given, <c>DataProvider:Database:FilePath</c> from configuration is used.
/// </remarks>
public static class ScheduleImportCommand
{
    public const string Name = "import";

    public static async Task<int> RunAsync(string[] args, IConfiguration configuration, string contentRootPath)
    {
        var databasePath = args.Length > 1 ? args[1] : configuration["DataProvider:Database:FilePath"];
        if (args.Length == 0 || string.IsNullOrWhiteSpace(databasePath))
        {
            await Console.Error.WriteLineAsync($"Usage: {Name} <plan file> [<database file>]");
            await Console.Error.WriteLineAsync("The database file defaults to DataProvider:Database:FilePath in configuration.");
            return 1;
        }

        var planFilePath = Path.GetFullPath(args[0]);
        databasePath = Path.IsPathRooted(databasePath) ? databasePath : Path.Combine(contentRootPath, databasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);

        var result = await DatabaseScheduleLoader.CreateDatabase(databasePath).ImportPlanFileAsync(planFilePath);
        if (result.IsFail)
        {
            await Console.Error.WriteLineAsync($"Import failed: {result.Error}");
            return 2;
        }
        var plan = result.Value!;
        Console.WriteLine($"Imported plan '{plan.Name}' with {plan.Timetable.Trains.Count} trains into {databasePath}");
        return 0;
    }
}
