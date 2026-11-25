using Tellurian.Trains.Dispatch.Data.Timetable;
using Tellurian.Trains.Dispatch.Layout;
using Tellurian.Trains.Dispatch.Trains;
using Tellurian.Trains.Schedules.Model.Layouts;
using Tellurian.Trains.Schedules.Model.Timetables;
using DispatchStation = Tellurian.Trains.Dispatch.Trains.Station;
using ModelDispatchStretch = Tellurian.Trains.Schedules.Model.Layouts.DispatchStretch;
using ModelLayout = Tellurian.Trains.Schedules.Model.Layouts.Layout;
using ModelOtherLocation = Tellurian.Trains.Schedules.Model.Layouts.OtherLocation;
using ModelStation = Tellurian.Trains.Schedules.Model.Layouts.Station;
using ModelStationTrack = Tellurian.Trains.Schedules.Model.Layouts.StationTrack;
using ModelTimetable = Tellurian.Trains.Schedules.Model.Timetables.Timetable;
using ModelTrackStretch = Tellurian.Trains.Schedules.Model.Layouts.TrackStretch;
using ModelTrain = Tellurian.Trains.Schedules.Model.Timetables.Train;

namespace Tellurian.Trains.Dispatch.Data.Tests;

[TestClass]
public class TimetableBrokerDataProviderTests
{
    [TestMethod]
    public async Task GetOperationPlacesAsync_ReturnsMannedStationsAsStations()
    {
        // Arrange
        var timetable = CreateTestTimetable();
        var provider = new TimetableBrokerDataProvider(timetable);

        // Act
        var places = await provider.GetOperationPlacesAsync();

        // Assert
        var placesList = places.ToList();
        Assert.HasCount(3, placesList);
        Assert.AreEqual(2, placesList.OfType<DispatchStation>().Count());
        Assert.AreEqual(1, placesList.OfType<OtherPlace>().Count());
    }

    [TestMethod]
    public async Task GetTrackStretchesAsync_ReturnsConvertedStretches()
    {
        // Arrange
        var timetable = CreateTestTimetable();
        var provider = new TimetableBrokerDataProvider(timetable);

        // Act
        var stretches = await provider.GetTrackStretchesAsync();

        // Assert
        var stretchesList = stretches.ToList();
        Assert.HasCount(2, stretchesList);
        Assert.IsTrue(stretchesList.All(s => s.Length > 0));
    }

    [TestMethod]
    public async Task GetDispatchStretchesAsync_CreatesBetweenMannedStations()
    {
        // Arrange
        var timetable = CreateTestTimetable();
        var provider = new TimetableBrokerDataProvider(timetable);

        // Act
        var dispatchStretches = await provider.GetDispatchStretchesAsync();

        // Assert
        var stretchesList = dispatchStretches.ToList();
        // Should have one dispatch stretch between A and C (both manned)
        // traversing through B (unmanned)
        Assert.HasCount(1, stretchesList);
    }

    [TestMethod]
    public async Task GetTrainsAsync_ReturnsConvertedTrains()
    {
        // Arrange
        var timetable = CreateTestTimetable();
        var provider = new TimetableBrokerDataProvider(timetable);

        // Act
        var trains = await provider.GetTrainsAsync();

        // Assert
        var trainsList = trains.ToList();
        Assert.HasCount(1, trainsList);
        Assert.AreEqual(101, trainsList[0].Identity.Number);
        Assert.AreEqual("Gt", trainsList[0].Identity.Prefix);
    }

    [TestMethod]
    public async Task GetTrainStationCallsAsync_ReturnsConvertedCallsAtStationsOnly()
    {
        // Arrange
        var timetable = CreateTestTimetable();
        var provider = new TimetableBrokerDataProvider(timetable);

        // Act
        var calls = await provider.GetTrainStationCallsAsync();

        // Assert: the call at the unmanned halt B is not dispatched.
        var callsList = calls.ToList();
        Assert.HasCount(2, callsList);
        Assert.IsTrue(callsList.All(c => c.At is DispatchStation));
        Assert.AreEqual(0, callsList[0].SequenceNumber);
        Assert.AreEqual(1, callsList[1].SequenceNumber);
    }

    [TestMethod]
    public async Task GetTrainStationCallsAsync_HasCorrectCallTimes()
    {
        // Arrange
        var timetable = CreateTestTimetable();
        var provider = new TimetableBrokerDataProvider(timetable);

        // Act
        var calls = await provider.GetTrainStationCallsAsync();

        // Assert
        var callsList = calls.OrderBy(c => c.SequenceNumber).ToList();
        Assert.AreEqual(TimeSpan.FromHours(8), callsList[0].Scheduled.DepartureTime);
        Assert.AreEqual(TimeSpan.FromHours(8).Add(TimeSpan.FromMinutes(30)), callsList[1].Scheduled.ArrivalTime);
    }

    [TestMethod]
    public async Task GetScheduleStartTimeAsync_ReturnsStartTime()
    {
        var timetable = CreateTestTimetable();
        timetable.Layout.Settings.General.StartTime = TimeSpan.FromHours(7);
        var provider = new TimetableBrokerDataProvider(timetable);

        Assert.AreEqual(TimeSpan.FromHours(7), await provider.GetScheduleStartTimeAsync());
    }

    [TestMethod]
    public async Task GetScheduleStartTimeAsync_RunsOverMidnight_ReturnsFirstSessionStartTime()
    {
        var timetable = CreateTestTimetable();
        timetable.Layout.Settings.General.RunsOverMidnight = true;
        timetable.Layout.Settings.General.FirstSessionStartTime = TimeSpan.FromHours(5);
        var provider = new TimetableBrokerDataProvider(timetable);

        Assert.AreEqual(TimeSpan.FromHours(5), await provider.GetScheduleStartTimeAsync());
    }

    [TestMethod]
    public async Task GetTrainsAsync_CreatesOneTrainPerOperatingDay()
    {
        var timetable = CreateTestTimetable();
        timetable.Layout.Settings.General.MaxSessions = 3;
        var provider = await LoadAllAsync(timetable);

        var trains = (await provider.GetTrainsAsync()).ToList();

        Assert.HasCount(3, trains);
        CollectionAssert.AreEqual(Enumerable.Range(1, 3).ToArray(), trains.Select(t => t.OperatingDay).Order().ToArray());
        Assert.HasCount(3, trains.Select(t => t.Id).Distinct());
    }

    [TestMethod]
    public async Task GetTrainsAsync_TrainRunsOnlyInItsSessions()
    {
        var timetable = CreateTestTimetable();
        timetable.Layout.Settings.General.MaxSessions = 6;
        timetable.Trains.Single().Sessions = Sessions.FromSessionNumbers(2, 5);
        var provider = await LoadAllAsync(timetable);

        var trains = (await provider.GetTrainsAsync()).ToList();

        Assert.AreEqual("2,5", string.Join(",", trains.Select(t => t.OperatingDay).Order()));
    }

    [TestMethod]
    public async Task GetTrainStationCallsAsync_EachOperatingDayHasOwnCallsAndTimes()
    {
        var timetable = CreateTestTimetable();
        timetable.Layout.Settings.General.MaxSessions = 2;
        var provider = await LoadAllAsync(timetable);

        var calls = (await provider.GetTrainStationCallsAsync()).ToList();

        Assert.HasCount(4, calls);
        var day2 = calls.Where(c => c.Train.OperatingDay == 2).OrderBy(c => c.SequenceNumber).ToList();
        Assert.HasCount(2, day2);
        Assert.AreEqual(TimeSpan.FromDays(1) + TimeSpan.FromHours(8), day2[0].Scheduled.DepartureTime);
        Assert.HasCount(4, calls.Select(c => c.Id).Distinct());
    }

    [TestMethod]
    public async Task GetTrainsAsync_TrainArrivingBeforeFirstSessionStart_DoesNotRunInFirstSession()
    {
        var timetable = CreateTestTimetable();
        var general = timetable.Layout.Settings.General;
        general.MaxSessions = 2;
        general.RunsOverMidnight = true;
        general.FirstSessionStartTime = TimeSpan.FromHours(9); // The train starts 08:00, before the first day starts.
        var provider = await LoadAllAsync(timetable);

        var trains = (await provider.GetTrainsAsync()).ToList();
        var calls = (await provider.GetTrainStationCallsAsync()).OrderBy(c => c.SequenceNumber).ToList();

        Assert.AreEqual(2, trains.Single().OperatingDay);
        Assert.AreEqual(TimeSpan.FromDays(1) + TimeSpan.FromHours(8), calls[0].Scheduled.DepartureTime);
    }

    [TestMethod]
    public async Task GetTrainsAsync_TrainArrivingAtFirstSessionStart_RunsInFirstSession()
    {
        var timetable = CreateTestTimetable();
        var general = timetable.Layout.Settings.General;
        general.RunsOverMidnight = true;
        general.FirstSessionStartTime = TimeSpan.FromHours(8);
        var provider = await LoadAllAsync(timetable);

        var trains = (await provider.GetTrainsAsync()).ToList();

        Assert.AreEqual(1, trains.Single().OperatingDay);
    }

    [TestMethod]
    public async Task GetTrainsAsync_NotRunningOverMidnight_IgnoresFirstSessionStartTime()
    {
        var timetable = CreateTestTimetable();
        timetable.Layout.Settings.General.FirstSessionStartTime = TimeSpan.FromHours(9);
        var provider = await LoadAllAsync(timetable);

        var trains = (await provider.GetTrainsAsync()).ToList();

        Assert.AreEqual(1, trains.Single().OperatingDay);
    }

    private static async Task<TimetableBrokerDataProvider> LoadAllAsync(ModelTimetable timetable)
    {
        var provider = new TimetableBrokerDataProvider(timetable);
        await provider.GetOperationPlacesAsync();
        await provider.GetTrackStretchesAsync();
        await provider.GetDispatchStretchesAsync();
        return provider;
    }

    internal static ModelTimetable CreateTestTimetable()
    {
        var layout = new ModelLayout
        {
            Id = 1,
            Name = "Test Layout"
        };
        layout.Settings.General.MaxSessions = 1;

        // Create stations (manned)
        var stationA = new ModelStation(1, "Station A", "A") { IsManned = true };
        stationA.Add(new ModelStationTrack(1, "1"));
        stationA.Add(new ModelStationTrack(2, "2"));
        layout.Add(stationA);

        // Create unmanned halt
        var haltB = new ModelOtherLocation(2, "Halt B", "B");
        haltB.Add(new ModelStationTrack(3, "1"));
        layout.Add(haltB);

        // Create station (manned)
        var stationC = new ModelStation(3, "Station C", "C") { IsManned = true };
        stationC.Add(new ModelStationTrack(4, "1"));
        stationC.Add(new ModelStationTrack(5, "2"));
        layout.Add(stationC);

        // Create track stretches (direction A → B → C)
        var stretchAB = new ModelTrackStretch(1, stationA, haltB, 5.0, 1);
        layout.Add(stretchAB);

        var stretchBC = new ModelTrackStretch(2, haltB, stationC, 7.0, 1);
        layout.Add(stretchBC);

        // Create dispatch stretch between manned stations A and C
        var dispatchStretch = new ModelDispatchStretch(1, stationA, stationC);
        layout.DispatchStretches.Add(dispatchStretch);

        // Create timetable
        var timetable = new ModelTimetable("Test Timetable", layout) { Id = 1 };

        // Create train category
        var category = new TrainCategory
        {
            Id = 1,
            Prefix = "Gt",
            Suffix = "",
            Name = "Godståg",
            Content = TrainContent.Cargo
        };

        // Create train
        var train = new ModelTrain(1, category, 101) { Sessions = Sessions.All };
        timetable.Add(train);

        // Add calls
        var callA = new StationCall(1, stationA["1"],
            Time.FromHourAndMinute(8, 0), Time.FromHourAndMinute(8, 0));
        callA.IsDeparture = true;
        callA.IsArrival = false;
        train.Add(callA);

        var callB = new StationCall(2, haltB["1"],
            Time.FromHourAndMinute(8, 15), Time.FromHourAndMinute(8, 17));
        callB.IsDeparture = true;
        callB.IsArrival = true;
        train.Add(callB);

        var callC = new StationCall(3, stationC["1"],
            Time.FromHourAndMinute(8, 30), Time.FromHourAndMinute(8, 30));
        callC.IsDeparture = false;
        callC.IsArrival = true;
        train.Add(callC);

        return timetable;
    }
}
