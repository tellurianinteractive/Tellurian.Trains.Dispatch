using Microsoft.Extensions.Logging.Abstractions;
using Tellurian.Trains.Dispatch.Brokers;

namespace Tellurian.Trains.Dispatch.Tests;

[TestClass]
public class SignalControlledPlaceTests
{
    private Broker _broker = null!;
    private SignalControlledPlaceTestDataProvider _dataProvider = null!;
    private InMemoryStateProvider _stateProvider = null!;
    private TestTimeProvider _timeProvider = null!;

    [TestInitialize]
    public async Task Setup()
    {
        _dataProvider = new SignalControlledPlaceTestDataProvider();
        _stateProvider = new InMemoryStateProvider();
        _timeProvider = new TestTimeProvider { CurrentTime = TimeSpan.FromHours(10) };
        _broker = new Broker(_dataProvider, _stateProvider, _timeProvider, NullLogger<Broker>.Instance);
        await _broker.InitAsync(isRestart: false);
    }

    #region Basic Setup Tests

    [TestMethod]
    public void BrokerCreatesTwoDispatchers()
    {
        // Only Station A and Station C are dispatchers (SignalControlledPlace B is not)
        var dispatchers = _broker.GetDispatchers().ToList();
        Assert.HasCount(2, dispatchers, "Should have 2 dispatchers (stations A and C)");
    }

    [TestMethod]
    public void DispatchersHaveCorrectSignatures()
    {
        var dispatchers = _broker.GetDispatchers().ToList();
        var signatures = dispatchers.Select(d => d.Signature).OrderBy(s => s).ToList();

        CollectionAssert.AreEqual(
            new[] { "A", "C" },
            signatures,
            "Dispatcher signatures should be A and C");
    }

    #endregion

    #region Pass Action Tests

    [TestMethod]
    public void AfterDepartArriveActionIsNotYetAvailable()
    {
        var dispatcherA = _broker.GetDispatchers().First(d => d.Signature == "A");
        var dispatcherC = _broker.GetDispatchers().First(d => d.Signature == "C");

        SetTrainToRunning(dispatcherA);
        RequestAndAccept(dispatcherA, dispatcherC);

        var departAction = _broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.Depart);
        departAction.Execute();

        // Train is on first track stretch (A->B), not yet at last stretch
        // So Arrive should NOT be available yet
        var arrivalActions = _broker.GetArrivalActionsFor(dispatcherC, 10).ToList();
        var arriveAction = arrivalActions.FirstOrDefault(a => a.Action == DispatchAction.Arrive);

        Assert.IsNull(arriveAction, "Arrive action should NOT be available before passing signal B");
    }

    [TestMethod]
    public void AfterDepartPassActionBecomesAvailable()
    {
        var dispatcherA = _broker.GetDispatchers().First(d => d.Signature == "A");
        var dispatcherC = _broker.GetDispatchers().First(d => d.Signature == "C");

        SetTrainToRunning(dispatcherA);
        RequestAndAccept(dispatcherA, dispatcherC);

        var departAction = _broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.Depart);
        departAction.Execute();

        // Pass should be available at Station A (which controls Signal B)
        var departureActions = _broker.GetDepartureActionsFor(dispatcherA, 10).ToList();
        var passAction = departureActions.FirstOrDefault(a => a.Action == DispatchAction.Pass);

        Assert.IsNotNull(passAction, "Pass action should be available at controlling dispatcher after Depart");
    }

    [TestMethod]
    public void PassActionHasCorrectPassTarget()
    {
        var dispatcherA = _broker.GetDispatchers().First(d => d.Signature == "A");
        var dispatcherC = _broker.GetDispatchers().First(d => d.Signature == "C");

        SetTrainToRunning(dispatcherA);
        RequestAndAccept(dispatcherA, dispatcherC);

        var departAction = _broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.Depart);
        departAction.Execute();

        var passAction = _broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.Pass);

        Assert.IsNotNull(passAction.PassTarget, "Pass action should have a PassTarget");
        Assert.AreEqual("B", passAction.PassTarget.Signature, "PassTarget should be Signal B");
    }

    [TestMethod]
    public void ExecutePassAdvancesToNextTrackStretch()
    {
        var dispatcherA = _broker.GetDispatchers().First(d => d.Signature == "A");
        var dispatcherC = _broker.GetDispatchers().First(d => d.Signature == "C");

        SetTrainToRunning(dispatcherA);
        RequestAndAccept(dispatcherA, dispatcherC);

        var departAction = _broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.Depart);
        departAction.Execute();

        Assert.AreEqual(0, departAction.Section.CurrentTrackStretchIndex, "Should start on first track stretch");

        var passAction = _broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.Pass);
        var result = passAction.Execute();

        Assert.IsTrue(result.HasValue, "Pass action should succeed");
        Assert.AreEqual(1, passAction.Section.CurrentTrackStretchIndex, "Should now be on second track stretch");
    }

    [TestMethod]
    public void AfterPassTrainIsOnLastTrackStretch()
    {
        var dispatcherA = _broker.GetDispatchers().First(d => d.Signature == "A");
        var dispatcherC = _broker.GetDispatchers().First(d => d.Signature == "C");

        SetTrainToRunning(dispatcherA);
        RequestAndAccept(dispatcherA, dispatcherC);

        var departAction = _broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.Depart);
        departAction.Execute();

        var passAction = _broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.Pass);
        passAction.Execute();

        Assert.IsTrue(passAction.Section.IsOnLastTrackStretch, "After Pass, train should be on last track stretch");
    }

    [TestMethod]
    public void AfterPassArriveActionBecomesAvailable()
    {
        var dispatcherA = _broker.GetDispatchers().First(d => d.Signature == "A");
        var dispatcherC = _broker.GetDispatchers().First(d => d.Signature == "C");

        SetTrainToRunning(dispatcherA);
        RequestAndAccept(dispatcherA, dispatcherC);

        var departAction = _broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.Depart);
        departAction.Execute();

        var passAction = _broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.Pass);
        passAction.Execute();

        // Now Arrive should be available at Station C
        var arrivalActions = _broker.GetArrivalActionsFor(dispatcherC, 10).ToList();
        var arriveAction = arrivalActions.FirstOrDefault(a => a.Action == DispatchAction.Arrive);

        Assert.IsNotNull(arriveAction, "Arrive action should be available after Pass");
    }

    [TestMethod]
    public void AfterPassNoMorePassActionsAvailable()
    {
        var dispatcherA = _broker.GetDispatchers().First(d => d.Signature == "A");
        var dispatcherC = _broker.GetDispatchers().First(d => d.Signature == "C");

        SetTrainToRunning(dispatcherA);
        RequestAndAccept(dispatcherA, dispatcherC);

        var departAction = _broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.Depart);
        departAction.Execute();

        var passAction = _broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.Pass);
        passAction.Execute();

        // No more Pass actions should be available (only one signal in this layout)
        var departureActions = _broker.GetDepartureActionsFor(dispatcherA, 10).ToList();
        var anotherPassAction = departureActions.FirstOrDefault(a => a.Action == DispatchAction.Pass);

        Assert.IsNull(anotherPassAction, "No more Pass actions should be available after passing the only signal");
    }

    #endregion

    #region Full Workflow Test

    [TestMethod]
    public void FullDispatchWorkflowFromAToCWithPassAction()
    {
        var dispatcherA = _broker.GetDispatchers().First(d => d.Signature == "A");
        var dispatcherC = _broker.GetDispatchers().First(d => d.Signature == "C");

        // 1. Set train to Manned
        var mannedAction = _broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.Manned);
        mannedAction.Execute();

        // 2. Set train to Running
        var runningAction = _broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.Running);
        runningAction.Execute();

        // 3. Request departure from A to C
        var requestAction = _broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.Request);
        requestAction.Execute();

        // 4. Accept at C
        var acceptAction = _broker.GetArrivalActionsFor(dispatcherC, 10)
            .First(a => a.Action == DispatchAction.Accept);
        acceptAction.Execute();

        // 5. Depart from A
        var departAction = _broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.Depart);
        departAction.Execute();

        // Verify: Train is on track stretch A->B
        Assert.AreEqual(0, departAction.Section.CurrentTrackStretchIndex);
        Assert.IsFalse(departAction.Section.IsOnLastTrackStretch);

        // 6. Pass Signal B (controlled by A)
        var passAction = _broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.Pass);
        passAction.Execute();

        // Verify: Train is now on track stretch B->C
        Assert.AreEqual(1, passAction.Section.CurrentTrackStretchIndex);
        Assert.IsTrue(passAction.Section.IsOnLastTrackStretch);

        // 7. Arrive at C
        var arriveAction = _broker.GetArrivalActionsFor(dispatcherC, 10)
            .First(a => a.Action == DispatchAction.Arrive);
        var result = arriveAction.Execute();

        Assert.IsTrue(result.HasValue, "Full workflow with Pass should complete successfully");
        Assert.AreEqual(DispatchState.Arrived, arriveAction.Section.State);
    }

    #endregion

    #region Helper Methods

    private void SetTrainToRunning(IDispatcher departureDispatcher)
    {
        var mannedAction = _broker.GetDepartureActionsFor(departureDispatcher, 10)
            .First(a => a.Action == DispatchAction.Manned);
        mannedAction.Execute();

        var runningAction = _broker.GetDepartureActionsFor(departureDispatcher, 10)
            .First(a => a.Action == DispatchAction.Running);
        runningAction.Execute();
    }

    private void RequestAndAccept(IDispatcher departureDispatcher, IDispatcher arrivalDispatcher)
    {
        var requestAction = _broker.GetDepartureActionsFor(departureDispatcher, 10)
            .First(a => a.Action == DispatchAction.Request);
        requestAction.Execute();

        var acceptAction = _broker.GetArrivalActionsFor(arrivalDispatcher, 10)
            .First(a => a.Action == DispatchAction.Accept);
        acceptAction.Execute();
    }

    #endregion
}
