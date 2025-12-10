using Microsoft.Extensions.Logging.Abstractions;
using Tellurian.Trains.Dispatch;
using Tellurian.Trains.Dispatch.Brokers;
using Tellurian.Trains.Dispatch.Data.Csv;
using Tellurian.Trains.Dispatch.Data.Json;
using Tellurian.Trains.Dispatch.Trains;

namespace Tellurian.Trains.Dispatch.Data.Tests;

/// <summary>
/// Tests for CSV state providers: CsvTrainStateProvider, CsvDispatchStateProvider, and CsvCompositeStateProvider.
/// </summary>
[TestClass]
public class CsvStateProviderTests
{
    private static string TestDataPath => Path.Combine(AppContext.BaseDirectory, "TestData");
    private static string LayoutFilePath => Path.Combine(TestDataPath, "junction-layout.json");
    private static string TrainFilePath => Path.Combine(TestDataPath, "junction-trains.json");

    private string _testDirectory = null!;
    private TestTimeProvider _timeProvider = null!;

    [TestInitialize]
    public void Setup()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "CsvStateProviderTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
        _timeProvider = new TestTimeProvider { CurrentTime = TimeSpan.FromHours(10) };
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    #region CsvTrainStateProvider Tests

    [TestMethod]
    public async Task TrainStateProvider_RecordsAndRestoresTrainStateChange()
    {
        // Arrange: Create broker and record a state change
        using var stateProvider1 = new CsvCompositeStateProvider(_testDirectory);
        var broker1 = await CreateBrokerWithStateProviderAsync(stateProvider1);
        var train = broker1.TrainSections.First().Departure.Train;
        var trainId = train.Id;
        var trainNumber = train.Identity.Number;

        // Act: Change train state and record it
        train.State = TrainState.Manned;
        await stateProvider1.TrainStateProvider.RecordTrainStateChangeAsync(trainId, train.State);
        stateProvider1.Dispose();

        // Restart broker with saved state
        using var stateProvider2 = new CsvCompositeStateProvider(_testDirectory);
        var broker2 = await CreateBrokerWithStateProviderAsync(stateProvider2, isRestart: true);

        // Find train by train number (which is consistent across restarts)
        var restoredTrain = broker2.TrainSections
            .Select(s => s.Departure.Train)
            .First(t => t.Identity.Number == trainNumber);

        // Assert
        Assert.AreEqual(TrainState.Manned, restoredTrain.State, "Train state should be restored to Manned");
    }

    [TestMethod]
    public async Task TrainStateProvider_RecordsMultipleStateChanges()
    {
        // Arrange
        using var stateProvider1 = new CsvCompositeStateProvider(_testDirectory);
        var broker1 = await CreateBrokerWithStateProviderAsync(stateProvider1);
        var train = broker1.TrainSections.First().Departure.Train;
        var trainId = train.Id;
        var trainNumber = train.Identity.Number;

        // Act: Multiple state changes
        train.State = TrainState.Manned;
        await stateProvider1.TrainStateProvider.RecordTrainStateChangeAsync(trainId, train.State);

        train.State = TrainState.Running;
        await stateProvider1.TrainStateProvider.RecordTrainStateChangeAsync(trainId, train.State);

        train.State = TrainState.Completed;
        await stateProvider1.TrainStateProvider.RecordTrainStateChangeAsync(trainId, train.State);
        stateProvider1.Dispose();

        // Restart broker
        using var stateProvider2 = new CsvCompositeStateProvider(_testDirectory);
        var broker2 = await CreateBrokerWithStateProviderAsync(stateProvider2, isRestart: true);
        var restoredTrain = broker2.TrainSections
            .Select(s => s.Departure.Train)
            .First(t => t.Identity.Number == trainNumber);

        // Assert: Final state should be preserved
        Assert.AreEqual(TrainState.Completed, restoredTrain.State, "Train should be in Completed state after replay");
    }

    [TestMethod]
    public async Task TrainStateProvider_HasSavedState_ReturnsFalseWhenNoFile()
    {
        using var provider = new CsvTrainStateProvider(_testDirectory);
        Assert.IsFalse(provider.HasSavedState, "HasSavedState should be false when no file exists");
    }

    [TestMethod]
    public async Task TrainStateProvider_HasSavedState_ReturnsTrueAfterRecording()
    {
        using var provider = new CsvTrainStateProvider(_testDirectory);

        await provider.RecordTrainStateChangeAsync(1, TrainState.Manned);

        Assert.IsTrue(provider.HasSavedState, "HasSavedState should be true after recording");
    }

    [TestMethod]
    public async Task TrainStateProvider_ClearAsync_RemovesFile()
    {
        using var provider = new CsvTrainStateProvider(_testDirectory);
        await provider.RecordTrainStateChangeAsync(1, TrainState.Manned);

        await provider.ClearAsync();

        Assert.IsFalse(provider.HasSavedState, "HasSavedState should be false after clearing");
    }

    #endregion

    #region CsvDispatchStateProvider Tests

    [TestMethod]
    public async Task DispatchStateProvider_WritesRecordsToFile()
    {
        // Arrange
        using var provider = new CsvDispatchStateProvider(_testDirectory);

        // Act
        await provider.RecordDispatchStateChangeAsync(1, DispatchState.Requested);
        await provider.RecordDispatchStateChangeAsync(1, DispatchState.Accepted);
        await provider.RecordDispatchStateChangeAsync(1, DispatchState.Departed, trackStretchIndex: 0);
        await provider.RecordPassAsync(1, signalControlledPlaceId: 2, newTrackStretchIndex: 1);
        provider.Dispose();

        // Assert: File was created with content
        var filePath = Path.Combine(_testDirectory, "dispatch-state.csv");
        Assert.IsTrue(File.Exists(filePath), "Dispatch state file should exist");
        var content = await File.ReadAllTextAsync(filePath);
        Assert.Contains(content, "Requested", "File should contain Requested state");
        Assert.Contains(content, "Departed", "File should contain Departed state");
        Assert.Contains(content, "Pass", "File should contain Pass event");
    }

    [TestMethod]
    public async Task DispatchStateProvider_ReadRecordsFromFile()
    {
        // Arrange: Write records
        using var provider1 = new CsvDispatchStateProvider(_testDirectory);
        await provider1.RecordDispatchStateChangeAsync(1, DispatchState.Requested);
        await provider1.RecordDispatchStateChangeAsync(1, DispatchState.Departed, trackStretchIndex: 0);
        provider1.Dispose();

        // Act: Read records
        var records = await CsvRecordExtensions.ReadDispatchStateRecordsAsync(
            Path.Combine(_testDirectory, "dispatch-state.csv"));

        // Assert
        Assert.HasCount(2, records, "Should have 2 records");
        Assert.AreEqual(DispatchState.Requested, records[0].State);
        Assert.AreEqual(DispatchState.Departed, records[1].State);
        Assert.AreEqual(0, records[1].TrackStretchIndex);
    }

    [TestMethod]
    public async Task DispatchStateProvider_HasSavedState_ReturnsFalseWhenNoFile()
    {
        using var provider = new CsvDispatchStateProvider(_testDirectory);
        Assert.IsFalse(provider.HasSavedState, "HasSavedState should be false when no file exists");
    }

    #endregion

    #region CsvCompositeStateProvider Tests

    [TestMethod]
    public async Task CompositeStateProvider_WritesAndReadsCompleteState()
    {
        // Arrange: Write state changes
        using var provider1 = new CsvCompositeStateProvider(_testDirectory);
        await provider1.TrainStateProvider.RecordTrainStateChangeAsync(1, TrainState.Manned);
        await provider1.TrainStateProvider.RecordTrainStateChangeAsync(1, TrainState.Running);
        await provider1.DispatchStateProvider.RecordDispatchStateChangeAsync(1, DispatchState.Requested);
        await provider1.DispatchStateProvider.RecordDispatchStateChangeAsync(1, DispatchState.Departed, trackStretchIndex: 0);
        provider1.Dispose();

        // Act: Verify files exist and have content
        var trainStateFile = Path.Combine(_testDirectory, "train-state.csv");
        var dispatchStateFile = Path.Combine(_testDirectory, "dispatch-state.csv");

        // Assert
        Assert.IsTrue(File.Exists(trainStateFile), "Train state file should exist");
        Assert.IsTrue(File.Exists(dispatchStateFile), "Dispatch state file should exist");

        var trainRecords = await CsvRecordExtensions.ReadTrainStateRecordsAsync(trainStateFile);
        var dispatchRecords = await CsvRecordExtensions.ReadDispatchStateRecordsAsync(dispatchStateFile);

        Assert.HasCount(2, trainRecords, "Should have 2 train state records");
        Assert.HasCount(2, dispatchRecords, "Should have 2 dispatch state records");
        Assert.AreEqual(TrainState.Running, trainRecords[1].State, "Final train state should be Running");
        Assert.AreEqual(DispatchState.Departed, dispatchRecords[1].State, "Final dispatch state should be Departed");
    }

    [TestMethod]
    public async Task CompositeStateProvider_HasAnySavedState_WorksCorrectly()
    {
        using var provider = new CsvCompositeStateProvider(_testDirectory);

        Assert.IsFalse(provider.HasAnySavedState, "Should be false initially");

        await provider.TrainStateProvider.RecordTrainStateChangeAsync(1, TrainState.Manned);

        Assert.IsTrue(provider.HasAnySavedState, "Should be true after recording train state");
    }

    [TestMethod]
    public async Task CompositeStateProvider_ClearAllAsync_ClearsBothProviders()
    {
        using var provider = new CsvCompositeStateProvider(_testDirectory);

        await provider.TrainStateProvider.RecordTrainStateChangeAsync(1, TrainState.Manned);
        await provider.DispatchStateProvider.RecordDispatchStateChangeAsync(1, DispatchState.Requested);

        Assert.IsTrue(provider.HasAnySavedState, "Should have saved state");

        await provider.ClearAllAsync();

        Assert.IsFalse(provider.HasAnySavedState, "Should not have saved state after clearing");
    }

    #endregion

    #region Integration Tests

    [TestMethod]
    public async Task ExecuteAndRecordAsync_RecordsMannedActionToCsv()
    {
        // Arrange: Create broker with CSV state provider
        using var stateProvider = new CsvCompositeStateProvider(_testDirectory);
        var broker = await CreateBrokerWithStateProviderAsync(stateProvider);
        var dispatcherA = broker.GetDispatchers().First(d => d.Signature == "A");

        var departureActions = broker.GetDepartureActionsFor(dispatcherA, 10).ToList();

        // Act: Execute Manned action with recording
        var mannedAction = departureActions.First(a => a.Action == DispatchAction.Manned);
        await mannedAction.ExecuteAndRecordAsync(stateProvider);

        stateProvider.Dispose();

        // Assert: Verify train state file has the expected content
        var trainStateFile = Path.Combine(_testDirectory, "train-state.csv");
        Assert.IsTrue(File.Exists(trainStateFile), "Train state file should exist");

        var trainRecords = await CsvRecordExtensions.ReadTrainStateRecordsAsync(trainStateFile);
        Assert.IsTrue(trainRecords.Any(r => r.State == TrainState.Manned), "Should have Manned state");
    }

    #endregion

    #region Helper Methods

    private async Task<Broker> CreateBrokerWithStateProviderAsync(ICompositeStateProvider stateProvider, bool isRestart = false)
    {
        var dataProvider = new JsonFileBrokerDataProvider(LayoutFilePath, TrainFilePath);
        var broker = new Broker(dataProvider, stateProvider, _timeProvider, NullLogger<Broker>.Instance);
        await broker.InitAsync(isRestart);
        return broker;
    }

    #endregion

    public TestContext TestContext { get; set; }
}
