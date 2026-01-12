using Tellurian.Trains.Dispatch.Data.Timetable;
using Tellurian.Trains.Dispatch.Layout;
using Tellurian.Trains.Dispatch.Trains;
using Tellurian.Trains.Schedules.Model;
using DispatchStation = Tellurian.Trains.Dispatch.Trains.Station;
using ModelDispatchStretch = Tellurian.Trains.Schedules.Model.DispatchStretch;
using ModelLayout = Tellurian.Trains.Schedules.Model.Layout;
using ModelOtherLocation = Tellurian.Trains.Schedules.Model.OtherLocation;
using ModelStation = Tellurian.Trains.Schedules.Model.Station;
using ModelStationTrack = Tellurian.Trains.Schedules.Model.StationTrack;
using ModelTimetable = Tellurian.Trains.Schedules.Model.Timetable;
using ModelTrackStretch = Tellurian.Trains.Schedules.Model.TrackStretch;
using ModelTrain = Tellurian.Trains.Schedules.Model.Train;

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
        Assert.AreEqual(3, placesList.Count);
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
        Assert.AreEqual(2, stretchesList.Count);
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
        Assert.AreEqual(1, stretchesList.Count);
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
        Assert.AreEqual(1, trainsList.Count);
        Assert.AreEqual(101, trainsList[0].Identity.Number);
        Assert.AreEqual("Gt", trainsList[0].Identity.Prefix);
    }

    [TestMethod]
    public async Task GetTrainStationCallsAsync_ReturnsConvertedCalls()
    {
        // Arrange
        var timetable = CreateTestTimetable();
        var provider = new TimetableBrokerDataProvider(timetable);

        // Act
        var calls = await provider.GetTrainStationCallsAsync();

        // Assert
        var callsList = calls.ToList();
        Assert.AreEqual(3, callsList.Count);
        Assert.IsTrue(callsList.All(c => c.SequenceNumber >= 0));
        Assert.AreEqual(0, callsList[0].SequenceNumber);
        Assert.AreEqual(1, callsList[1].SequenceNumber);
        Assert.AreEqual(2, callsList[2].SequenceNumber);
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
        Assert.AreEqual(TimeSpan.FromHours(8).Add(TimeSpan.FromMinutes(15)), callsList[1].Scheduled.ArrivalTime);
    }

    private static ModelTimetable CreateTestTimetable()
    {
        var layout = new ModelLayout
        {
            Id = 1,
            Name = "Test Layout"
        };

        // Create stations (manned)
        var stationA = new ModelStation(1, "Station A", "A");
        stationA.Add(new ModelStationTrack(1, "1"));
        stationA.Add(new ModelStationTrack(2, "2"));
        layout.Add(stationA);

        // Create unmanned halt
        var haltB = new ModelOtherLocation(2, "Halt B", "B");
        haltB.Add(new ModelStationTrack(3, "1"));
        layout.Add(haltB);

        // Create station (manned)
        var stationC = new ModelStation(3, "Station C", "C");
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
            ResourceName = "Godståg",
            IsFreight = true
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
