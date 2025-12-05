using Microsoft.Extensions.Logging.Abstractions;
using Tellurian.Trains.Dispatch.Brokers;
using Tellurian.Trains.Dispatch.Trains;

namespace Tellurian.Trains.Dispatch.Tests;

/// <summary>
/// Tests for dispatch scenario with block signals between stations.
/// Verifies block signal passage workflow and dispatcher actions.
/// </summary>
[TestClass]
public class BlockSignalDispatchTests
{
    private Broker _broker = default!;
    private StationDispatcher _departureDispatcher = default!; // Alpha
    private StationDispatcher _arrivalDispatcher = default!;   // Beta (controls block signal)
    private TrainSection _trainSection = default!;

    [TestInitialize]
    public async Task Setup()
    {
        var configuration = new BlockSignalDispatchConfiguration();
        var stateProvider = new InMemoryStateProvider();
        var timeProvider = new TestTimeProvider();
        var logger = NullLogger<Broker>.Instance;

        _broker = new Broker(configuration, stateProvider, timeProvider, logger);
        await _broker.InitAsync(isRestart: false);

        var dispatchers = _broker.GetDispatchers().Cast<StationDispatcher>().ToList();
        _departureDispatcher = dispatchers.Single(d => d.Name == "Alpha");
        _arrivalDispatcher = dispatchers.Single(d => d.Name == "Beta");
        _trainSection = _departureDispatcher.Departures.Single();
    }

    #region 1. Happy Path with Block Signal

    [TestMethod]
    public void Request_Accept_Departed_PassBlockSignal_Arrived_CompletesDispatch()
    {
        // Initial state
        Assert.AreEqual(DispatchState.None, _trainSection.State);
        Assert.HasCount(1, _trainSection.BlockSignalPassages);

        // Request
        var requestAction = GetDepartureAction(DispatchState.Requested);
        Assert.IsTrue(requestAction());
        Assert.AreEqual(DispatchState.Requested, _trainSection.State);

        // Accept
        var acceptAction = GetArrivalAction(DispatchState.Accepted);
        Assert.IsTrue(acceptAction());
        Assert.AreEqual(DispatchState.Accepted, _trainSection.State);

        // Departed
        var departedAction = GetDepartureAction(DispatchState.Departed);
        Assert.IsTrue(departedAction());
        Assert.AreEqual(DispatchState.Departed, _trainSection.State);

        // Block signal is expected
        Assert.AreEqual(BlockSignalPassageState.Expected, _trainSection.BlockSignalPassages[0].State);

        // Pass block signal (controlled by arrival dispatcher)
        var passAction = GetArrivalAction(DispatchState.Passed);
        Assert.IsTrue(passAction());
        Assert.AreEqual(BlockSignalPassageState.Passed, _trainSection.BlockSignalPassages[0].State);

        // Arrived
        var arrivedAction = GetArrivalAction(DispatchState.Arrived);
        Assert.IsTrue(arrivedAction());
        Assert.AreEqual(DispatchState.Arrived, _trainSection.State);
        Assert.AreEqual(TrainState.Completed, _trainSection.Departure.Train.State);
    }

    #endregion

    #region 2. Block Signal Passage Workflow

    [TestMethod]
    public void AfterDeparted_ArrivalDispatcherHasPassAction()
    {
        // Setup: Request -> Accept -> Departed
        GetDepartureAction(DispatchState.Requested)();
        GetArrivalAction(DispatchState.Accepted)();
        GetDepartureAction(DispatchState.Departed)();

        // Arrival dispatcher (Beta) controls the block signal, so has Pass action
        Assert.IsTrue(HasArrivalAction(DispatchState.Passed));
    }

    [TestMethod]
    public void AfterDeparted_DepartureDispatcherHasNoPassAction()
    {
        // Setup: Request -> Accept -> Departed
        GetDepartureAction(DispatchState.Requested)();
        GetArrivalAction(DispatchState.Accepted)();
        GetDepartureAction(DispatchState.Departed)();

        // Departure dispatcher (Alpha) doesn't control the block signal
        Assert.IsFalse(HasDepartureAction(DispatchState.Passed));
        Assert.IsFalse(_trainSection.DepartureActions.Any());
    }

    [TestMethod]
    public void PassBlockSignal_MarksPassageAsPassed()
    {
        // Setup: Request -> Accept -> Departed
        GetDepartureAction(DispatchState.Requested)();
        GetArrivalAction(DispatchState.Accepted)();
        GetDepartureAction(DispatchState.Departed)();

        Assert.AreEqual(BlockSignalPassageState.Expected, _trainSection.BlockSignalPassages[0].State);

        // Pass block signal
        GetArrivalAction(DispatchState.Passed)();

        Assert.AreEqual(BlockSignalPassageState.Passed, _trainSection.BlockSignalPassages[0].State);
    }

    [TestMethod]
    public void PassBlockSignal_SetsCurrentBlockIndex()
    {
        // Setup: Request -> Accept -> Departed
        GetDepartureAction(DispatchState.Requested)();
        GetArrivalAction(DispatchState.Accepted)();
        GetDepartureAction(DispatchState.Departed)();

        // Before passing: train is in block 0
        Assert.AreEqual(0, GetCurrentBlockIndex());

        // Pass block signal
        GetArrivalAction(DispatchState.Passed)();

        // After passing: train is in block 1
        Assert.AreEqual(1, GetCurrentBlockIndex());
    }

    #endregion

    #region 3. Arrival Blocked Until Block Signal Passed

    [TestMethod]
    public void CannotArrive_BeforePassingBlockSignal()
    {
        // Setup: Request -> Accept -> Departed
        GetDepartureAction(DispatchState.Requested)();
        GetArrivalAction(DispatchState.Accepted)();
        GetDepartureAction(DispatchState.Departed)();

        // Arrived action should NOT be available (block signal not passed)
        Assert.IsFalse(HasArrivalAction(DispatchState.Arrived));

        // Only Pass action should be available
        Assert.IsTrue(HasArrivalAction(DispatchState.Passed));
        Assert.AreEqual(1, _trainSection.ArrivalActions.Count());
    }

    [TestMethod]
    public void AfterPassingBlockSignal_ArrivalActionBecomesAvailable()
    {
        // Setup: Request -> Accept -> Departed
        GetDepartureAction(DispatchState.Requested)();
        GetArrivalAction(DispatchState.Accepted)();
        GetDepartureAction(DispatchState.Departed)();

        // Pass block signal
        GetArrivalAction(DispatchState.Passed)();

        // Now Arrived should be available
        Assert.IsTrue(HasArrivalAction(DispatchState.Arrived));
        Assert.IsFalse(HasArrivalAction(DispatchState.Passed)); // No more signals to pass
    }

    #endregion

    #region 4. State Verification

    [TestMethod]
    public void AfterDeparted_BlockSignalPassageIsExpected()
    {
        // Setup: Request -> Accept -> Departed
        GetDepartureAction(DispatchState.Requested)();
        GetArrivalAction(DispatchState.Accepted)();
        GetDepartureAction(DispatchState.Departed)();

        var passage = _trainSection.BlockSignalPassages[0];
        Assert.AreEqual(BlockSignalPassageState.Expected, passage.State);
        Assert.AreEqual("Signal 1", passage.BlockSignal.Name);
    }

    [TestMethod]
    public void AfterDeparted_HasPassedAllBlockSignals_IsFalse()
    {
        // Setup: Request -> Accept -> Departed
        GetDepartureAction(DispatchState.Requested)();
        GetArrivalAction(DispatchState.Accepted)();
        GetDepartureAction(DispatchState.Departed)();

        Assert.IsFalse(GetHasPassedAllBlockSignals());
    }

    [TestMethod]
    public void AfterPassingBlockSignal_HasPassedAllBlockSignals_IsTrue()
    {
        // Setup: Request -> Accept -> Departed -> Pass
        GetDepartureAction(DispatchState.Requested)();
        GetArrivalAction(DispatchState.Accepted)();
        GetDepartureAction(DispatchState.Departed)();
        GetArrivalAction(DispatchState.Passed)();

        Assert.IsTrue(GetHasPassedAllBlockSignals());
    }

    #endregion

    #region 5. Canceled/Aborted with Block Signal

    [TestMethod]
    public void ClearAbortedTrain_CancelsExpectedBlockSignalPassages()
    {
        // Setup: Request -> Accept -> Departed
        GetDepartureAction(DispatchState.Requested)();
        GetArrivalAction(DispatchState.Accepted)();
        GetDepartureAction(DispatchState.Departed)();

        // Block signal is expected
        Assert.AreEqual(BlockSignalPassageState.Expected, _trainSection.BlockSignalPassages[0].State);

        // Abort the train (train is Running, so only Abort is available)
        var abortAction = GetTrainAction(TrainState.Aborted);
        Assert.IsTrue(abortAction());

        // Clear the train
        var clearAction = _trainSection.ClearCanceledOrAbortedTrain;
        Assert.IsNotNull(clearAction);
        Assert.IsTrue(clearAction());

        // Block signal passage should be Canceled
        Assert.AreEqual(BlockSignalPassageState.Canceled, _trainSection.BlockSignalPassages[0].State);
        Assert.AreEqual(DispatchState.Canceled, _trainSection.State);
    }

    [TestMethod]
    public void AbortedTrain_AfterDeparted_HasClearAction()
    {
        // Setup: Request -> Accept -> Departed
        GetDepartureAction(DispatchState.Requested)();
        GetArrivalAction(DispatchState.Accepted)();
        GetDepartureAction(DispatchState.Departed)();

        // Abort the train (train is Running, so only Abort is available)
        var abortAction = GetTrainAction(TrainState.Aborted);
        Assert.IsTrue(abortAction());

        // Clear action should be available
        var clearAction = _trainSection.ClearCanceledOrAbortedTrain;
        Assert.IsNotNull(clearAction);
    }

    #endregion

    #region 6. GetBlockSignalActionsFor Dispatcher

    [TestMethod]
    public void GetBlockSignalActionsFor_ReturnsAction_ForControllingDispatcher()
    {
        // Setup: Request -> Accept -> Departed
        GetDepartureAction(DispatchState.Requested)();
        GetArrivalAction(DispatchState.Accepted)();
        GetDepartureAction(DispatchState.Departed)();

        // Beta controls the block signal
        var actions = _trainSection.GetBlockSignalActionsFor(_arrivalDispatcher);
        Assert.AreEqual(1, actions.Count());
    }

    [TestMethod]
    public void GetBlockSignalActionsFor_ReturnsEmpty_ForNonControllingDispatcher()
    {
        // Setup: Request -> Accept -> Departed
        GetDepartureAction(DispatchState.Requested)();
        GetArrivalAction(DispatchState.Accepted)();
        GetDepartureAction(DispatchState.Departed)();

        // Alpha doesn't control the block signal
        var actions = _trainSection.GetBlockSignalActionsFor(_departureDispatcher);
        Assert.IsFalse(actions.Any());
    }

    #endregion

    #region Helper Methods

    private Func<bool> GetDepartureAction(DispatchState state) =>
        _trainSection.DepartureActions.Single(a => a.State == state).Action;

    private Func<bool> GetArrivalAction(DispatchState state) =>
        _trainSection.ArrivalActions.Single(a => a.State == state).Action;

    private Func<bool> GetTrainAction(TrainState state) =>
        _trainSection.AvailableTrainActions.Single(a => a.State == state).Action;

    private bool HasDepartureAction(DispatchState state) =>
        _trainSection.DepartureActions.Any(a => a.State == state);

    private bool HasArrivalAction(DispatchState state) =>
        _trainSection.ArrivalActions.Any(a => a.State == state);

    // Access internal extension methods via reflection or direct property access
    private int GetCurrentBlockIndex() =>
        _trainSection.BlockSignalPassages.Count(p => p.State == BlockSignalPassageState.Passed);

    private bool GetHasPassedAllBlockSignals() =>
        _trainSection.BlockSignalPassages.Count == 0 ||
        _trainSection.BlockSignalPassages.All(p => p.State == BlockSignalPassageState.Passed ||
                                                    p.State == BlockSignalPassageState.Canceled);

    #endregion
}
