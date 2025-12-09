using Microsoft.Extensions.Logging.Abstractions;
using Tellurian.Trains.Dispatch;
using Tellurian.Trains.Dispatch.Brokers;
using Tellurian.Trains.Dispatch.Data.Json;
using Tellurian.Trains.Dispatch.Trains;

namespace Tellurian.Trains.Dispatch.Data.Tests;

/// <summary>
/// Tests for <see cref="JsonFileBrokerStateProvider"/> to verify
/// state can be saved and restored correctly.
/// </summary>
[TestClass]
public class JsonFileBrokerStateProviderTests
{
    private static string TestDataPath => Path.Combine(AppContext.BaseDirectory, "TestData");
    private static string LayoutFilePath => Path.Combine(TestDataPath, "junction-layout.json");
    private static string TrainFilePath => Path.Combine(TestDataPath, "junction-trains.json");
    private string _stateFilePath = null!;
    private TestTimeProvider _timeProvider = null!;

    [TestInitialize]
    public void Setup()
    {
        _stateFilePath = Path.Combine(Path.GetTempPath(), $"test-state-{Guid.NewGuid()}.json");
        _timeProvider = new TestTimeProvider { CurrentTime = TimeSpan.FromHours(10) };
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (File.Exists(_stateFilePath))
            File.Delete(_stateFilePath);
    }

    [TestMethod]
    public async Task SaveCreatesStateFile()
    {
        var broker = await CreateAndInitializeBrokerAsync();
        var stateProvider = new JsonFileBrokerStateProvider(_stateFilePath);

        var dispatcherA = broker.GetDispatchers().First(d => d.Signature == "A");
        var sections = broker.GetDepartureActionsFor(dispatcherA, 10).Select(a => a.Section).Distinct().ToList();

        var result = await stateProvider.SaveTrainsectionsAsync(sections);

        Assert.IsTrue(result, "Save should succeed");
        Assert.IsTrue(File.Exists(_stateFilePath), "State file should be created");
    }

    [TestMethod]
    public async Task SavedStateFileContainsValidJson()
    {
        var broker = await CreateAndInitializeBrokerAsync();
        var stateProvider = new JsonFileBrokerStateProvider(_stateFilePath);

        var dispatcherA = broker.GetDispatchers().First(d => d.Signature == "A");
        var sections = broker.GetDepartureActionsFor(dispatcherA, 10).Select(a => a.Section).Distinct().ToList();

        await stateProvider.SaveTrainsectionsAsync(sections);
        var json = await File.ReadAllTextAsync(_stateFilePath, TestContext.CancellationToken);

        Assert.IsNotNull(json);
        Assert.IsTrue(json.Contains("SavedAt", StringComparison.OrdinalIgnoreCase), "JSON should contain SavedAt");
        Assert.IsTrue(json.Contains("SectionStates", StringComparison.OrdinalIgnoreCase), "JSON should contain SectionStates");
    }

    [TestMethod]
    public async Task CanSaveAndApplyState()
    {
        var broker = await CreateAndInitializeBrokerAsync();
        var stateProvider = new JsonFileBrokerStateProvider(_stateFilePath);

        var dispatcherA = broker.GetDispatchers().First(d => d.Signature == "A");
        var originalSections = broker.GetDepartureActionsFor(dispatcherA, 10)
            .Select(a => a.Section)
            .Distinct()
            .ToList();

        // Save state
        await stateProvider.SaveTrainsectionsAsync(originalSections);

        // Create new sections (simulating restart)
        var broker2 = await CreateAndInitializeBrokerAsync();
        var newSections = broker2.GetDepartureActionsFor(
            broker2.GetDispatchers().First(d => d.Signature == "A"), 10)
            .Select(a => a.Section)
            .Distinct()
            .ToList();

        // Apply saved state
        var applied = await stateProvider.ApplyStateAsync(newSections);

        Assert.IsTrue(applied, "State should be applied successfully");
    }

    [TestMethod]
    public async Task AppliedStatePreservesDispatchState()
    {
        var broker = await CreateAndInitializeBrokerAsync();
        var stateProvider = new JsonFileBrokerStateProvider(_stateFilePath);

        var dispatcherA = broker.GetDispatchers().First(d => d.Signature == "A");

        // Progress the train to Requested state
        var mannedAction = broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.Manned);
        mannedAction.Execute();

        var runningAction = broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.Running);
        runningAction.Execute();

        var requestAction = broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.Request);
        requestAction.Execute();

        var section = requestAction.Section;
        var sectionId = section.Id;

        // Save state
        await stateProvider.SaveTrainsectionsAsync([section]);

        // Verify section state was saved correctly by applying to same section after reset
        section.ApplyState(DispatchState.None, 0); // Reset the state
        Assert.AreEqual(DispatchState.None, section.State, "State should be reset");

        // Apply saved state to the same section
        await stateProvider.ApplyStateAsync([section]);

        Assert.AreEqual(DispatchState.Requested, section.State, "Dispatch state should be preserved after apply");
    }

    [TestMethod]
    public async Task AppliedStatePreservesTrainState()
    {
        var broker = await CreateAndInitializeBrokerAsync();
        var stateProvider = new JsonFileBrokerStateProvider(_stateFilePath);

        var dispatcherA = broker.GetDispatchers().First(d => d.Signature == "A");

        // Progress the train to Running state
        var mannedAction = broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.Manned);
        mannedAction.Execute();

        var runningAction = broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.Running);
        runningAction.Execute();

        var section = runningAction.Section;
        var train = section.Departure.Train;

        // Save state
        await stateProvider.SaveTrainsectionsAsync([section]);

        // Reset train state
        train.State = TrainState.Planned;
        Assert.AreEqual(TrainState.Planned, train.State, "Train state should be reset");

        // Apply saved state
        await stateProvider.ApplyStateAsync([section]);

        Assert.AreEqual(TrainState.Running, train.State, "Train state should be preserved after apply");
    }

    [TestMethod]
    public async Task HasSavedStateReturnsFalseWhenNoFile()
    {
        var stateProvider = new JsonFileBrokerStateProvider(_stateFilePath);

        Assert.IsFalse(stateProvider.HasSavedState, "HasSavedState should be false when no file exists");
    }

    [TestMethod]
    public async Task HasSavedStateReturnsTrueAfterSave()
    {
        var broker = await CreateAndInitializeBrokerAsync();
        var stateProvider = new JsonFileBrokerStateProvider(_stateFilePath);

        var dispatcherA = broker.GetDispatchers().First(d => d.Signature == "A");
        var sections = broker.GetDepartureActionsFor(dispatcherA, 10).Select(a => a.Section).Distinct().ToList();

        await stateProvider.SaveTrainsectionsAsync(sections);

        Assert.IsTrue(stateProvider.HasSavedState, "HasSavedState should be true after save");
    }

    [TestMethod]
    public async Task ApplyStateReturnsFalseWhenNoFile()
    {
        var stateProvider = new JsonFileBrokerStateProvider(_stateFilePath);
        var broker = await CreateAndInitializeBrokerAsync();

        var dispatcherA = broker.GetDispatchers().First(d => d.Signature == "A");
        var sections = broker.GetDepartureActionsFor(dispatcherA, 10).Select(a => a.Section).Distinct().ToList();

        var applied = await stateProvider.ApplyStateAsync(sections);

        Assert.IsFalse(applied, "ApplyState should return false when no state file exists");
    }

    private async Task<Broker> CreateAndInitializeBrokerAsync()
    {
        var dataProvider = new JsonFileBrokerDataProvider(LayoutFilePath, TrainFilePath);
        var stateProvider = new InMemoryStateProvider();
        var broker = new Broker(dataProvider, stateProvider, _timeProvider, NullLogger<Broker>.Instance);
        await broker.InitAsync(isRestart: false);
        return broker;
    }

    public TestContext TestContext { get; set; }
}
