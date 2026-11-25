using Tellurian.Trains.Schedules.Model.Settings;
using Tellurian.Trains.Schedules.Model.Timetables;
using Tellurian.Trains.Schedules.Model.Dispatching;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Tellurian.Trains.Dispatch.Data.Database;
using Tellurian.Trains.Dispatch.Data.Timetable;
using Tellurian.Trains.Schedules.Model.Databases;
using Tellurian.Trains.Schedules.Model.PlanFiles;
using Tellurian.Trains.Schedules.Model.Schedules;
using Tellurian.Trains.Dispatch.Brokers;
using Tellurian.Trains.Dispatch.Utilities;

namespace Tellurian.Trains.Dispatch.Data.Tests;

[TestClass]
public sealed class ScheduleDatabaseTests
{
    /// <summary>A complete schedule saved by the Timetable Planner.</summary>
    private const string VarnamoPlanFile = "TestData/Varnamo-Moduldagar.json";

    private string _directory = string.Empty;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"ScheduleDatabaseTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void TestCleanup()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_directory, recursive: true); } catch (IOException) { }
    }

    [TestMethod]
    public async Task ImportPlanFile_ThenBrokerDataMatchesPlan()
    {
        var plan = Plan.Create("Test Plan", TimetableBrokerDataProviderTests.CreateTestTimetable());
        var expected = new TimetableBrokerDataProvider(plan.Timetable);
        var planFile = WritePlanFile(plan);
        var database = CreateDatabase();

        var imported = await database.ImportPlanFileAsync(planFile, TestContext.CancellationToken);

        Assert.IsTrue(imported.HasValue, imported.Error);
        await AssertSameBrokerDataAsync(expected, database.CreateBrokerDataProvider());
    }

    [TestMethod]
    public async Task ImportTimetablePlannerFile_ThenBrokerDataMatchesPlan()
    {
        var planText = await File.ReadAllTextAsync(VarnamoPlanFile, TestContext.CancellationToken);
        var expected = new TimetableBrokerDataProvider(PlanFile.Read(planText));
        var database = CreateDatabase();

        var imported = await database.ImportPlanFileAsync(VarnamoPlanFile, TestContext.CancellationToken);

        Assert.IsTrue(imported.HasValue, imported.Error);
        Assert.AreEqual("Värnamo Moduldagar", imported.Value!.Name);
        Assert.IsNotEmpty(imported.Value.Timetable.Trains);
        Assert.IsNotEmpty(await expected.GetTrainStationCallsAsync(TestContext.CancellationToken));
        await AssertSameBrokerDataAsync(expected, database.CreateBrokerDataProvider());
    }

    [TestMethod]
    public async Task ImportTimetablePlannerFile_ThenBrokerInitializes()
    {
        var database = CreateDatabase();
        await database.ImportPlanFileAsync(VarnamoPlanFile, TestContext.CancellationToken);
        var broker = new Broker(database.CreateBrokerDataProvider(), new InMemoryCompositeStateProvider(), new TestTimeProvider(), NullLogger<Broker>.Instance);

        await broker.InitAsync(isRestart: false, TestContext.CancellationToken);

        Assert.IsNotEmpty(broker.GetDispatchers());
    }

    [TestMethod]
    public async Task ImportTimetablePlannerFile_TrainsRunOnTheirOperatingDays()
    {
        var plan = PlanFile.Read(await File.ReadAllTextAsync(VarnamoPlanFile, TestContext.CancellationToken));
        var general = plan.Timetable.Layout.Settings.General;
        var days = general.MaxSessions;
        var settings = general.SessionSettings();
        // Trains that some dispatcher clears; shunting trains staying at one station are not dispatched.
        var dispatched = plan.Timetable.Trains.Where(t => t.DispatchLegs.Count > 0).ToList();
        // In the Värnamo plan, 30 driver duties of the first session start before 05:00; they have 34 trains.
        const int TrainsBeforeFirstSession = 34;
        var expectedTrainCount = dispatched.Sum(t => t.Sessions.NumbersWithin(settings).Count) - TrainsBeforeFirstSession;
        var database = CreateDatabase();
        await database.ImportPlanFileAsync(VarnamoPlanFile, TestContext.CancellationToken);
        var provider = database.CreateBrokerDataProvider();
        await provider.GetOperationPlacesAsync(TestContext.CancellationToken);

        var trains = (await provider.GetTrainsAsync(TestContext.CancellationToken)).ToList();

        Assert.AreEqual(6, days);
        Assert.HasCount(expectedTrainCount, trains);
        Assert.IsLessThan(plan.Timetable.Trains.Count * days, trains.Count, "Some trains run only in some sessions.");
        CollectionAssert.AreEqual(Enumerable.Range(1, days).ToArray(), trains.Select(t => t.OperatingDay).Distinct().Order().ToArray());
        Assert.HasCount(trains.Count, trains.Select(t => t.Id).Distinct());
    }

    [TestMethod]
    public async Task ImportTimetablePlannerFile_TrainInDutyStartingBeforeFirstSession_DoesNotRunInFirstSession()
    {
        var database = CreateDatabase();
        await database.ImportPlanFileAsync(VarnamoPlanFile, TestContext.CancellationToken);
        var provider = database.CreateBrokerDataProvider();
        await provider.GetOperationPlacesAsync(TestContext.CancellationToken);

        var trains = (await provider.GetTrainsAsync(TestContext.CancellationToken)).ToList();

        // Duty 21 starts 02:20 with train 4101 and continues with train 4102 from 08:15.
        int[] DaysOf(int number) => [.. trains.Where(t => t.Identity.Number == number).Select(t => t.OperatingDay).Order()];
        CollectionAssert.AreEqual(Enumerable.Range(2, 5).ToArray(), DaysOf(4101));
        CollectionAssert.AreEqual(Enumerable.Range(2, 5).ToArray(), DaysOf(4102));
    }

    [TestMethod]
    public async Task ImportTimetablePlannerFile_TrainsHaveCompanyOfTrainOrCategory()
    {
        var plan = PlanFile.Read(await File.ReadAllTextAsync(VarnamoPlanFile, TestContext.CancellationToken));
        var withoutCompany = plan.Timetable.Trains.Count(t => (t.Company ?? t.Category?.Company) is null);
        var provider = new TimetableBrokerDataProvider(plan);
        await provider.GetOperationPlacesAsync(TestContext.CancellationToken);

        var trains = (await provider.GetTrainsAsync(TestContext.CancellationToken)).ToList();

        Assert.IsLessThan(plan.Timetable.Trains.Count, withoutCompany);
        Assert.AreEqual(withoutCompany, trains.Select(t => (t.Identity.Number, t.Company.Signature)).Distinct().Count(t => t.Signature == "??"));
    }

    [TestMethod]
    public async Task ImportTimetablePlannerFile_ScheduleStartsAtFirstSessionStartTime()
    {
        var database = CreateDatabase();
        await database.ImportPlanFileAsync(VarnamoPlanFile, TestContext.CancellationToken);

        var startTime = await database.CreateBrokerDataProvider().GetScheduleStartTimeAsync(TestContext.CancellationToken);

        Assert.AreEqual(TimeSpan.FromHours(5), startTime);
    }

    [TestMethod]
    public async Task ImportTimetablePlannerFile_DeparturesAreOrderedWithinOperatingDay()
    {
        var database = CreateDatabase();
        await database.ImportPlanFileAsync(VarnamoPlanFile, TestContext.CancellationToken);
        var broker = new Broker(database.CreateBrokerDataProvider(), new InMemoryCompositeStateProvider(), new TestTimeProvider(), NullLogger<Broker>.Instance);
        await broker.InitAsync(isRestart: false, TestContext.CancellationToken);

        foreach (var dispatcher in broker.GetDispatchers())
        {
            var departures = broker.GetDepartureActionsFor(dispatcher, int.MaxValue)
                .Select(a => a.Section)
                .DistinctBy(s => s.Id)
                .Select(s => s.Departure.Scheduled.DepartureTime.InOperatingDayStartingAt(broker.ScheduleStartTime))
                .ToList();
            if (departures.Count == 0) continue;

            CollectionAssert.AreEqual(departures.Order().ToList(), departures, dispatcher.Name);
            Assert.IsGreaterThanOrEqualTo(TimeSpan.FromHours(5), departures[0], dispatcher.Name);
        }
    }

    [TestMethod]
    public async Task ImportPlanFile_ReplacesPreviousPlan()
    {
        var database = CreateDatabase();
        await database.ImportPlanFileAsync(WritePlanFile(Plan.Create("First", TimetableBrokerDataProviderTests.CreateTestTimetable())), TestContext.CancellationToken);

        await database.ImportPlanFileAsync(WritePlanFile(Plan.Create("Second", TimetableBrokerDataProviderTests.CreateTestTimetable())), TestContext.CancellationToken);

        var names = await database.GetPlanNamesAsync(TestContext.CancellationToken);
        Assert.HasCount(1, names);
        Assert.AreEqual("Second", names[0]);
    }

    [TestMethod]
    public async Task ImportPlanFile_MissingFile_Fails()
    {
        var database = CreateDatabase();

        var result = await database.ImportPlanFileAsync(Path.Combine(_directory, "missing.json"), TestContext.CancellationToken);

        Assert.IsTrue(result.IsFail);
    }

    [TestMethod]
    public async Task ImportPlanFile_NotAPlan_Fails()
    {
        var file = Path.Combine(_directory, "invalid.json");
        await File.WriteAllTextAsync(file, """{ "format": "something-else" }""", TestContext.CancellationToken);
        var database = CreateDatabase();

        var result = await database.ImportPlanFileAsync(file, TestContext.CancellationToken);

        Assert.IsTrue(result.IsFail);
    }

    [TestMethod]
    public async Task ReadPlan_EmptyDatabase_Fails()
    {
        var database = CreateDatabase();

        var result = await database.ReadPlanAsync(cancellationToken: TestContext.CancellationToken);

        Assert.IsTrue(result.IsFail);
    }

    [TestMethod]
    public async Task ReadPlan_UnknownName_Fails()
    {
        var database = CreateDatabase();
        await database.ImportPlanAsync(Plan.Create("Test Plan", TimetableBrokerDataProviderTests.CreateTestTimetable()));

        var result = await database.ReadPlanAsync("Other", TestContext.CancellationToken);

        Assert.IsTrue(result.IsFail);
    }

    private ScheduleDatabase CreateDatabase()
    {
        var connectionString = new SqliteConnectionStringBuilder { DataSource = Path.Combine(_directory, "schedule.db") }.ToString();
        return new(new DbContextOptionsBuilder<ScheduleDbContext>().UseSqlite(connectionString).Options);
    }

    private string WritePlanFile(Plan plan)
    {
        var path = Path.Combine(_directory, $"{plan.Name}.json");
        File.WriteAllText(path, PlanFile.Write(plan, writeIndented: true));
        return path;
    }

    private async Task AssertSameBrokerDataAsync(TimetableBrokerDataProvider expected, IBrokerDataProvider actual)
    {
        var ct = TestContext.CancellationToken;
        var expectedPlaces = (await expected.GetOperationPlacesAsync(ct)).Select(p => $"{p.GetType().Name}:{p.Signature}:{p.Tracks.Count}").Order().ToList();
        var actualPlaces = (await actual.GetOperationPlacesAsync(ct)).Select(p => $"{p.GetType().Name}:{p.Signature}:{p.Tracks.Count}").Order().ToList();
        CollectionAssert.AreEqual(expectedPlaces, actualPlaces);

        Assert.HasCount((await expected.GetTrackStretchesAsync(ct)).Count(), await actual.GetTrackStretchesAsync(ct));
        Assert.HasCount((await expected.GetDispatchStretchesAsync(ct)).Count(), await actual.GetDispatchStretchesAsync(ct));

        var expectedTrains = (await expected.GetTrainsAsync(ct)).Select(TrainKey).Order().ToList();
        var actualTrains = (await actual.GetTrainsAsync(ct)).Select(TrainKey).Order().ToList();
        CollectionAssert.AreEqual(expectedTrains, actualTrains);

        var expectedCalls = (await expected.GetTrainStationCallsAsync(ct)).Select(CallKey).Order().ToList();
        var actualCalls = (await actual.GetTrainStationCallsAsync(ct)).Select(CallKey).Order().ToList();
        CollectionAssert.AreEqual(expectedCalls, actualCalls);
    }

    private static string CallKey(Trains.TrainStationCall call) =>
        $"{TrainKey(call.Train)}:{call.At.Signature}:{call.PlannedTrack.Number}:{call.Scheduled.ArrivalTime}:{call.Scheduled.DepartureTime}";

    private static string TrainKey(Trains.Train train) =>
        $"{train.Company.Signature} {train.Identity.Prefix} {train.Identity.Number}";
}
