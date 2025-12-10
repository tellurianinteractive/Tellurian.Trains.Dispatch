using Microsoft.Extensions.Logging.Abstractions;
using Tellurian.Trains.Dispatch.Brokers;
using Tellurian.Trains.Dispatch.Data.Json;
using Tellurian.Trains.Dispatch.Layout;
using Tellurian.Trains.Dispatch.Trains;

namespace Tellurian.Trains.Dispatch.Data.Tests;

/// <summary>
/// Tests for the <see cref="JsonFileBrokerDataProvider"/> to verify
/// that JSON test data files can be loaded and used with the Broker.
/// </summary>
[TestClass]
public class JsonFileBrokerDataProviderTests
{
    private static string TestDataPath => Path.Combine(AppContext.BaseDirectory, "TestData");
    private static string LayoutFilePath => Path.Combine(TestDataPath, "junction-layout.json");
    private static string TrainFilePath => Path.Combine(TestDataPath, "junction-trains.json");

    [TestMethod]
    public async Task CanLoadOperationPlacesFromJson()
    {
        var provider = new JsonFileBrokerDataProvider(LayoutFilePath, TrainFilePath);

        var places = (await provider.GetOperationPlacesAsync(TestContext.CancellationToken)).ToList();

        Assert.HasCount(4, places, "Should have 4 operation places (A, B, C, D)");
    }

    [TestMethod]
    public async Task OperationPlacesHaveCorrectTypes()
    {
        var provider = new JsonFileBrokerDataProvider(LayoutFilePath, TrainFilePath);

        var places = (await provider.GetOperationPlacesAsync(TestContext.CancellationToken)).ToList();

        var stations = places.OfType<Station>().ToList();
        var signalPlaces = places.OfType<SignalControlledPlace>().ToList();

        Assert.HasCount(3, stations, "Should have 3 stations (A, C, D)");
        Assert.HasCount(1, signalPlaces, "Should have 1 signal controlled place (B)");
    }

    [TestMethod]
    public async Task SignalControlledPlaceHasCorrectControlledByDispatcher()
    {
        var provider = new JsonFileBrokerDataProvider(LayoutFilePath, TrainFilePath);

        var places = (await provider.GetOperationPlacesAsync(TestContext.CancellationToken)).ToList();
        var signalB = places.OfType<SignalControlledPlace>().First();
        var stationA = places.OfType<Station>().First(s => s.Signature == "A");

        Assert.AreEqual("B", signalB.Signature);
        Assert.AreEqual(stationA.Id, signalB.ControlledByDispatcherId, "Signal B should be controlled by Station A");
        Assert.IsTrue(signalB.IsJunction, "Signal B should be marked as junction");
    }

    [TestMethod]
    public async Task CanLoadTrackStretchesFromJson()
    {
        var provider = new JsonFileBrokerDataProvider(LayoutFilePath, TrainFilePath);

        var stretches = (await provider.GetTrackStretchesAsync(TestContext.CancellationToken)).ToList();

        Assert.HasCount(3, stretches, "Should have 3 track stretches (A-B, B-C, B-D)");
    }

    [TestMethod]
    public async Task CanLoadDispatchStretchesFromJson()
    {
        var provider = new JsonFileBrokerDataProvider(LayoutFilePath, TrainFilePath);

        var stretches = (await provider.GetDispatchStretchesAsync(TestContext.CancellationToken)).ToList();

        Assert.HasCount(2, stretches, "Should have 2 dispatch stretches (A-C, D-A)");
    }

    [TestMethod]
    public async Task CanLoadTrainsFromJson()
    {
        var provider = new JsonFileBrokerDataProvider(LayoutFilePath, TrainFilePath);

        var trains = (await provider.GetTrainsAsync(TestContext.CancellationToken)).ToList();

        Assert.HasCount(2, trains, "Should have 2 trains (201, 202)");
        Assert.IsTrue(trains.Any(t => t.Identity.Number == 201), "Should have train 201");
        Assert.IsTrue(trains.Any(t => t.Identity.Number == 202), "Should have train 202");
    }

    [TestMethod]
    public async Task CanLoadTrainStationCallsFromJson()
    {
        var provider = new JsonFileBrokerDataProvider(LayoutFilePath, TrainFilePath);

        var calls = (await provider.GetTrainStationCallsAsync(TestContext.CancellationToken)).ToList();

        Assert.HasCount(4, calls, "Should have 4 train station calls");
    }

    [TestMethod]
    public async Task TrainStationCallsHaveCorrectScheduledTimes()
    {
        var provider = new JsonFileBrokerDataProvider(LayoutFilePath, TrainFilePath);

        var calls = (await provider.GetTrainStationCallsAsync(TestContext.CancellationToken)).ToList();
        var train201Departure = calls.First(c => c.Train.Identity.Number == 201 && c.IsDeparture && !c.IsArrival);

        Assert.AreEqual(TimeSpan.FromHours(10), train201Departure.Scheduled.DepartureTime);
    }

    [TestMethod]
    public async Task CanInitializeBrokerWithJsonProvider()
    {
        var provider = new JsonFileBrokerDataProvider(LayoutFilePath, TrainFilePath);
        var stateProvider = new InMemoryCompositeStateProvider();
        var timeProvider = new TestTimeProvider { CurrentTime = TimeSpan.FromHours(10) };
        var broker = new Broker(provider, stateProvider, timeProvider, NullLogger<Broker>.Instance);

        await broker.InitAsync(isRestart: false, TestContext.CancellationToken);

        var dispatchers = broker.GetDispatchers().ToList();
        Assert.HasCount(3, dispatchers, "Should have 3 dispatchers (stations A, C, D)");
    }

    [TestMethod]
    public async Task BrokerWithJsonProviderHasDepartureActionsForTrains()
    {
        var provider = new JsonFileBrokerDataProvider(LayoutFilePath, TrainFilePath);
        var stateProvider = new InMemoryCompositeStateProvider();
        var timeProvider = new TestTimeProvider { CurrentTime = TimeSpan.FromHours(10) };
        var broker = new Broker(provider, stateProvider, timeProvider, NullLogger<Broker>.Instance);
        await broker.InitAsync(isRestart: false, TestContext.CancellationToken);

        var dispatcherA = broker.GetDispatchers().First(d => d.Signature == "A");
        var dispatcherD = broker.GetDispatchers().First(d => d.Signature == "D");

        var actionsA = broker.GetDepartureActionsFor(dispatcherA, 10).ToList();
        var actionsD = broker.GetDepartureActionsFor(dispatcherD, 10).ToList();

        Assert.IsNotEmpty(actionsA, "Station A should have departure actions for train 201");
        Assert.IsNotEmpty(actionsD, "Station D should have departure actions for train 202");
    }

    public TestContext TestContext { get; set; }
}
