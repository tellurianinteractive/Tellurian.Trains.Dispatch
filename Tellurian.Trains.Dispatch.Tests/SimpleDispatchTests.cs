using Microsoft.Extensions.Logging.Abstractions;
using Tellurian.Trains.Dispatch.Brokers;
using Tellurian.Trains.Dispatch.Data;

namespace Tellurian.Trains.Dispatch.Tests;

[TestClass]
public class SimpleDispatchTests
{
    private Broker _broker = null!;
    private SimpleTestDataProvider _dataProvider = null!;
    private InMemoryStateProvider _stateProvider = null!;
    private TestTimeProvider _timeProvider = null!;
    private static readonly string[] expected = ["A", "B", "C"];

    [TestInitialize]
    public async Task Setup()
    {
        _dataProvider = new SimpleTestDataProvider();
        _stateProvider = new InMemoryStateProvider();
        _timeProvider = new TestTimeProvider { CurrentTime = TimeSpan.FromHours(10) };
        _broker = new Broker(_dataProvider, _stateProvider, _timeProvider, NullLogger<Broker>.Instance);
        await _broker.InitAsync(isRestart: false);
    }

    [TestMethod]
    public void BrokerCreatesThreeDispatchers()
    {
        var dispatchers = _broker.GetDispatchers().ToList();
        Assert.HasCount(3, dispatchers, "Should have 3 dispatchers (one per station)");
    }

    [TestMethod]
    public void DispatchersHaveCorrectNames()
    {
        var dispatchers = _broker.GetDispatchers().ToList();
        var signatures = dispatchers.Select(d => d.Signature).OrderBy(s => s).ToList();

        CollectionAssert.AreEqual(
            expected,
            signatures,
            "Dispatcher signatures should be A, B, C");
    }

    [TestMethod]
    public void StationADispatcherHasDepartureActions()
    {
        var dispatcherA = _broker.GetDispatchers().First(d => d.Signature == "A");
        var departureActions = _broker.GetDepartureActionsFor(dispatcherA, 10).ToList();

        // Train starts at station A, so station A should have departure actions
        Assert.IsNotEmpty(departureActions, "Station A should have departure actions for train");
    }

    [TestMethod]
    public void StationBDispatcherHasNoInitialDepartureActions()
    {
        var dispatcherB = _broker.GetDispatchers().First(d => d.Signature == "B");
        var departureActions = _broker.GetDepartureActionsFor(dispatcherB, 10).ToList();

        // Train hasn't arrived at B yet, so no departure actions
        Assert.IsEmpty(departureActions, "Station B should have no departure actions initially");
    }

    [TestMethod]
    public void StationCDispatcherHasNoInitialActions()
    {
        var dispatcherC = _broker.GetDispatchers().First(d => d.Signature == "C");
        var departureActions = _broker.GetDepartureActionsFor(dispatcherC, 10).ToList();
        var arrivalActions = _broker.GetArrivalActionsFor(dispatcherC, 10).ToList();

        // Train ends at C (no departure), and hasn't been dispatched yet
        Assert.IsEmpty(departureActions, "Station C should have no departure actions");
        Assert.IsEmpty(arrivalActions, "Station C should have no arrival actions initially");
    }

    [TestMethod]
    public void InitialActionsIncludeMannedForPlannedTrain()
    {
        var dispatcherA = _broker.GetDispatchers().First(d => d.Signature == "A");
        var departureActions = _broker.GetDepartureActionsFor(dispatcherA, 10).ToList();

        var mannedAction = departureActions.FirstOrDefault(a => a.Action == DispatchAction.Manned);
        Assert.IsNotNull(mannedAction, "Should have Manned action for planned train");
    }

    [TestMethod]
    public void InitialActionsIncludeCanceledForPlannedTrain()
    {
        var dispatcherA = _broker.GetDispatchers().First(d => d.Signature == "A");
        var departureActions = _broker.GetDepartureActionsFor(dispatcherA, 10).ToList();

        var canceledAction = departureActions.FirstOrDefault(a => a.Action == DispatchAction.Canceled);
        Assert.IsNotNull(canceledAction, "Should have Canceled action for planned train");
    }

    [TestMethod]
    public void NoRequestActionAvailableForUnmannedTrain()
    {
        var dispatcherA = _broker.GetDispatchers().First(d => d.Signature == "A");
        var departureActions = _broker.GetDepartureActionsFor(dispatcherA, 10).ToList();

        var requestAction = departureActions.FirstOrDefault(a => a.Action == DispatchAction.Request);
        Assert.IsNull(requestAction, "Request action should not be available for unmanned train");
    }

    #region Train State Transition Tests

    [TestMethod]
    public void ExecuteMannedChangesTrainStateToManned()
    {
        var dispatcherA = _broker.GetDispatchers().First(d => d.Signature == "A");
        var mannedAction = _broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.Manned);

        var result = mannedAction.Execute();

        Assert.IsTrue(result.HasValue, "Manned action should succeed");
        Assert.AreEqual(Trains.TrainState.Manned, mannedAction.Section.Departure.Train.State);
    }

    [TestMethod]
    public void AfterMannedRunningActionBecomesAvailable()
    {
        var dispatcherA = _broker.GetDispatchers().First(d => d.Signature == "A");
        var mannedAction = _broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.Manned);
        mannedAction.Execute();

        var departureActions = _broker.GetDepartureActionsFor(dispatcherA, 10).ToList();
        var runningAction = departureActions.FirstOrDefault(a => a.Action == DispatchAction.Running);

        Assert.IsNotNull(runningAction, "Running action should be available after Manned");
    }

    [TestMethod]
    public void ExecuteRunningChangesTrainStateToRunning()
    {
        var dispatcherA = _broker.GetDispatchers().First(d => d.Signature == "A");

        // First set train to Manned
        var mannedAction = _broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.Manned);
        mannedAction.Execute();

        // Then set to Running
        var runningAction = _broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.Running);
        var result = runningAction.Execute();

        Assert.IsTrue(result.HasValue, "Running action should succeed");
        Assert.AreEqual(Trains.TrainState.Running, runningAction.Section.Departure.Train.State);
    }

    [TestMethod]
    public void ExecuteCanceledChangesTrainStateToCanceled()
    {
        var dispatcherA = _broker.GetDispatchers().First(d => d.Signature == "A");

        // Cancel a planned train before it starts
        var canceledAction = _broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.Canceled);
        var result = canceledAction.Execute();

        Assert.IsTrue(result.HasValue, "Canceled action should succeed");
        Assert.AreEqual(Trains.TrainState.Canceled, canceledAction.Section.Departure.Train.State);
    }

    [TestMethod]
    public void ExecuteAbortedChangesTrainStateToAborted()
    {
        // Aborted is only available on non-first sections
        var dispatcherA = _broker.GetDispatchers().First(d => d.Signature == "A");
        var dispatcherB = _broker.GetDispatchers().First(d => d.Signature == "B");
        SetTrainToRunning(dispatcherA);

        // Complete A -> B journey
        RequestAndAccept(dispatcherA, dispatcherB);
        _broker.GetDepartureActionsFor(dispatcherA, 10).First(a => a.Action == DispatchAction.Depart).Execute();
        _broker.GetArrivalActionsFor(dispatcherB, 10).First(a => a.Action == DispatchAction.Arrive).Execute();

        // Now Aborted should be available on the second section (B->C)
        var abortedAction = _broker.GetDepartureActionsFor(dispatcherB, 10)
            .First(a => a.Action == DispatchAction.Aborted);
        var result = abortedAction.Execute();

        Assert.IsTrue(result.HasValue, "Aborted action should succeed");
        Assert.AreEqual(Trains.TrainState.Aborted, abortedAction.Section.Departure.Train.State);
    }

    #endregion

    #region Dispatch State Transition Tests

    [TestMethod]
    public void AfterRunningRequestActionBecomesAvailable()
    {
        var dispatcherA = _broker.GetDispatchers().First(d => d.Signature == "A");
        SetTrainToRunning(dispatcherA);

        var departureActions = _broker.GetDepartureActionsFor(dispatcherA, 10).ToList();
        var requestAction = departureActions.FirstOrDefault(a => a.Action == DispatchAction.Request);

        Assert.IsNotNull(requestAction, "Request action should be available after train is Running");
    }

    [TestMethod]
    public void ExecuteRequestChangesStateToRequested()
    {
        var dispatcherA = _broker.GetDispatchers().First(d => d.Signature == "A");
        SetTrainToRunning(dispatcherA);

        var requestAction = _broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.Request);
        var result = requestAction.Execute();

        Assert.IsTrue(result.HasValue, "Request action should succeed");
        Assert.AreEqual(DispatchState.Requested, requestAction.Section.State);
    }

    [TestMethod]
    public void AfterRequestAcceptActionBecomesAvailableAtArrivalStation()
    {
        var dispatcherA = _broker.GetDispatchers().First(d => d.Signature == "A");
        var dispatcherB = _broker.GetDispatchers().First(d => d.Signature == "B");
        SetTrainToRunning(dispatcherA);

        var requestAction = _broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.Request);
        requestAction.Execute();

        // Accept should be available at station B (arrival station for A->B stretch)
        var arrivalActions = _broker.GetArrivalActionsFor(dispatcherB, 10).ToList();
        var acceptAction = arrivalActions.FirstOrDefault(a => a.Action == DispatchAction.Accept);

        Assert.IsNotNull(acceptAction, "Accept action should be available at arrival station after Request");
    }

    [TestMethod]
    public void ExecuteAcceptChangesStateToAccepted()
    {
        var dispatcherA = _broker.GetDispatchers().First(d => d.Signature == "A");
        var dispatcherB = _broker.GetDispatchers().First(d => d.Signature == "B");
        SetTrainToRunning(dispatcherA);

        var requestAction = _broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.Request);
        requestAction.Execute();

        var acceptAction = _broker.GetArrivalActionsFor(dispatcherB, 10)
            .First(a => a.Action == DispatchAction.Accept);
        var result = acceptAction.Execute();

        Assert.IsTrue(result.HasValue, "Accept action should succeed");
        Assert.AreEqual(DispatchState.Accepted, acceptAction.Section.State);
    }

    [TestMethod]
    public void AfterAcceptDepartActionBecomesAvailable()
    {
        var dispatcherA = _broker.GetDispatchers().First(d => d.Signature == "A");
        var dispatcherB = _broker.GetDispatchers().First(d => d.Signature == "B");
        SetTrainToRunning(dispatcherA);
        RequestAndAccept(dispatcherA, dispatcherB);

        var departureActions = _broker.GetDepartureActionsFor(dispatcherA, 10).ToList();
        var departAction = departureActions.FirstOrDefault(a => a.Action == DispatchAction.Depart);

        Assert.IsNotNull(departAction, "Depart action should be available after Accept");
    }

    [TestMethod]
    public void ExecuteDepartChangesStateToDeparted()
    {
        var dispatcherA = _broker.GetDispatchers().First(d => d.Signature == "A");
        var dispatcherB = _broker.GetDispatchers().First(d => d.Signature == "B");
        SetTrainToRunning(dispatcherA);
        RequestAndAccept(dispatcherA, dispatcherB);

        var departAction = _broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.Depart);
        var result = departAction.Execute();

        Assert.IsTrue(result.HasValue, "Depart action should succeed");
        Assert.AreEqual(DispatchState.Departed, departAction.Section.State);
    }

    [TestMethod]
    public void AfterDepartArriveActionBecomesAvailableAtArrivalStation()
    {
        var dispatcherA = _broker.GetDispatchers().First(d => d.Signature == "A");
        var dispatcherB = _broker.GetDispatchers().First(d => d.Signature == "B");
        SetTrainToRunning(dispatcherA);
        RequestAndAccept(dispatcherA, dispatcherB);

        var departAction = _broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.Depart);
        departAction.Execute();

        var arrivalActions = _broker.GetArrivalActionsFor(dispatcherB, 10).ToList();
        var arriveAction = arrivalActions.FirstOrDefault(a => a.Action == DispatchAction.Arrive);

        Assert.IsNotNull(arriveAction, "Arrive action should be available at arrival station after Depart");
    }

    [TestMethod]
    public void ExecuteArriveChangesStateToArrived()
    {
        var dispatcherA = _broker.GetDispatchers().First(d => d.Signature == "A");
        var dispatcherB = _broker.GetDispatchers().First(d => d.Signature == "B");
        SetTrainToRunning(dispatcherA);
        RequestAndAccept(dispatcherA, dispatcherB);

        var departAction = _broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.Depart);
        departAction.Execute();

        var arriveAction = _broker.GetArrivalActionsFor(dispatcherB, 10)
            .First(a => a.Action == DispatchAction.Arrive);
        var result = arriveAction.Execute();

        Assert.IsTrue(result.HasValue, "Arrive action should succeed");
        Assert.AreEqual(DispatchState.Arrived, arriveAction.Section.State);
    }

    [TestMethod]
    public void FullDispatchWorkflowFromAToB()
    {
        var dispatcherA = _broker.GetDispatchers().First(d => d.Signature == "A");
        var dispatcherB = _broker.GetDispatchers().First(d => d.Signature == "B");

        // 1. Set train to Manned
        var mannedAction = _broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.Manned);
        mannedAction.Execute();

        // 2. Set train to Running
        var runningAction = _broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.Running);
        runningAction.Execute();

        // 3. Request departure from A
        var requestAction = _broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.Request);
        requestAction.Execute();

        // 4. Accept at B
        var acceptAction = _broker.GetArrivalActionsFor(dispatcherB, 10)
            .First(a => a.Action == DispatchAction.Accept);
        acceptAction.Execute();

        // 5. Depart from A
        var departAction = _broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.Depart);
        departAction.Execute();

        // 6. Arrive at B
        var arriveAction = _broker.GetArrivalActionsFor(dispatcherB, 10)
            .First(a => a.Action == DispatchAction.Arrive);
        var result = arriveAction.Execute();

        Assert.IsTrue(result.HasValue, "Full workflow should complete successfully");
        Assert.AreEqual(DispatchState.Arrived, arriveAction.Section.State);
    }

    [TestMethod]
    public void FullJourneyFromAToCCompletesSuccessfully()
    {
        var dispatcherA = _broker.GetDispatchers().First(d => d.Signature == "A");
        var dispatcherB = _broker.GetDispatchers().First(d => d.Signature == "B");
        var dispatcherC = _broker.GetDispatchers().First(d => d.Signature == "C");

        // Complete full journey A -> B -> C
        SetTrainToRunning(dispatcherA);

        // A -> B
        RequestAndAccept(dispatcherA, dispatcherB);
        _broker.GetDepartureActionsFor(dispatcherA, 10).First(a => a.Action == DispatchAction.Depart).Execute();
        _broker.GetArrivalActionsFor(dispatcherB, 10).First(a => a.Action == DispatchAction.Arrive).Execute();

        // B -> C
        var requestBC = _broker.GetDepartureActionsFor(dispatcherB, 10).First(a => a.Action == DispatchAction.Request);
        requestBC.Execute();
        _broker.GetArrivalActionsFor(dispatcherC, 10).First(a => a.Action == DispatchAction.Accept).Execute();
        _broker.GetDepartureActionsFor(dispatcherB, 10).First(a => a.Action == DispatchAction.Depart).Execute();
        var arriveAction = _broker.GetArrivalActionsFor(dispatcherC, 10).First(a => a.Action == DispatchAction.Arrive);
        var result = arriveAction.Execute();

        Assert.IsTrue(result.HasValue, "Full journey should complete successfully");
        Assert.AreEqual(DispatchState.Arrived, arriveAction.Section.State);
        Assert.IsTrue(arriveAction.Section.IsLast, "Final section should be marked as last");
    }

    [TestMethod]
    public void ExecuteClearChangesDispatchStateToCanceled()
    {
        // Clear is available when train is Canceled/Aborted AND section is Departed
        var dispatcherA = _broker.GetDispatchers().First(d => d.Signature == "A");
        var dispatcherB = _broker.GetDispatchers().First(d => d.Signature == "B");
        var dispatcherC = _broker.GetDispatchers().First(d => d.Signature == "C");
        SetTrainToRunning(dispatcherA);

        // Complete A -> B
        RequestAndAccept(dispatcherA, dispatcherB);
        _broker.GetDepartureActionsFor(dispatcherA, 10).First(a => a.Action == DispatchAction.Depart).Execute();
        _broker.GetArrivalActionsFor(dispatcherB, 10).First(a => a.Action == DispatchAction.Arrive).Execute();

        // Start B -> C (depart but don't arrive)
        var requestBC = _broker.GetDepartureActionsFor(dispatcherB, 10).First(a => a.Action == DispatchAction.Request);
        requestBC.Execute();
        _broker.GetArrivalActionsFor(dispatcherC, 10).First(a => a.Action == DispatchAction.Accept).Execute();
        _broker.GetDepartureActionsFor(dispatcherB, 10).First(a => a.Action == DispatchAction.Depart).Execute();

        // Abort the train (available on non-first sections when Running)
        var abortedAction = _broker.GetDepartureActionsFor(dispatcherB, 10)
            .First(a => a.Action == DispatchAction.Aborted);
        abortedAction.Execute();

        // Now Clear should be available on the B->C section (Departed + Aborted)
        var clearAction = _broker.GetDepartureActionsFor(dispatcherB, 10)
            .First(a => a.Action == DispatchAction.Clear);
        var result = clearAction.Execute();

        Assert.IsTrue(result.HasValue, "Clear action should succeed");
        Assert.AreEqual(DispatchState.Canceled, clearAction.Section.State);
    }

    #endregion

    #region Reject Scenario Tests

    [TestMethod]
    public void AfterRequestRejectActionBecomesAvailableAtArrivalStation()
    {
        var dispatcherA = _broker.GetDispatchers().First(d => d.Signature == "A");
        var dispatcherB = _broker.GetDispatchers().First(d => d.Signature == "B");
        SetTrainToRunning(dispatcherA);

        var requestAction = _broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.Request);
        requestAction.Execute();

        var arrivalActions = _broker.GetArrivalActionsFor(dispatcherB, 10).ToList();
        var rejectAction = arrivalActions.FirstOrDefault(a => a.Action == DispatchAction.Reject);

        Assert.IsNotNull(rejectAction, "Reject action should be available at arrival station after Request");
    }

    [TestMethod]
    public void ExecuteRejectChangesStateToRejected()
    {
        var dispatcherA = _broker.GetDispatchers().First(d => d.Signature == "A");
        var dispatcherB = _broker.GetDispatchers().First(d => d.Signature == "B");
        SetTrainToRunning(dispatcherA);

        var requestAction = _broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.Request);
        requestAction.Execute();

        var rejectAction = _broker.GetArrivalActionsFor(dispatcherB, 10)
            .First(a => a.Action == DispatchAction.Reject);
        var result = rejectAction.Execute();

        Assert.IsTrue(result.HasValue, "Reject action should succeed");
        Assert.AreEqual(DispatchState.Rejected, rejectAction.Section.State);
    }

    [TestMethod]
    public void AfterRejectRequestActionBecomesAvailableAgain()
    {
        var dispatcherA = _broker.GetDispatchers().First(d => d.Signature == "A");
        var dispatcherB = _broker.GetDispatchers().First(d => d.Signature == "B");
        SetTrainToRunning(dispatcherA);

        // Request
        var requestAction = _broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.Request);
        requestAction.Execute();

        // Reject
        var rejectAction = _broker.GetArrivalActionsFor(dispatcherB, 10)
            .First(a => a.Action == DispatchAction.Reject);
        rejectAction.Execute();

        // Request should be available again
        var departureActions = _broker.GetDepartureActionsFor(dispatcherA, 10).ToList();
        var newRequestAction = departureActions.FirstOrDefault(a => a.Action == DispatchAction.Request);

        Assert.IsNotNull(newRequestAction, "Request action should be available again after Reject");
    }

    #endregion

    #region Revoke Scenario Tests

    [TestMethod]
    public void AfterRequestRevokeActionBecomesAvailableAtDepartureStation()
    {
        var dispatcherA = _broker.GetDispatchers().First(d => d.Signature == "A");
        SetTrainToRunning(dispatcherA);

        var requestAction = _broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.Request);
        requestAction.Execute();

        var departureActions = _broker.GetDepartureActionsFor(dispatcherA, 10).ToList();
        var revokeAction = departureActions.FirstOrDefault(a => a.Action == DispatchAction.Revoke);

        Assert.IsNotNull(revokeAction, "Revoke action should be available at departure station after Request");
    }

    [TestMethod]
    public void ExecuteRevokeFromRequestedChangesStateToRevoked()
    {
        var dispatcherA = _broker.GetDispatchers().First(d => d.Signature == "A");
        SetTrainToRunning(dispatcherA);

        var requestAction = _broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.Request);
        requestAction.Execute();

        var revokeAction = _broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.Revoke);
        var result = revokeAction.Execute();

        Assert.IsTrue(result.HasValue, "Revoke action should succeed");
        Assert.AreEqual(DispatchState.Revoked, revokeAction.Section.State);
    }

    [TestMethod]
    public void AfterRevokeFromRequestedRequestActionBecomesAvailableAgain()
    {
        var dispatcherA = _broker.GetDispatchers().First(d => d.Signature == "A");
        SetTrainToRunning(dispatcherA);

        // Request
        var requestAction = _broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.Request);
        requestAction.Execute();

        // Revoke
        var revokeAction = _broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.Revoke);
        revokeAction.Execute();

        // Request should be available again
        var departureActions = _broker.GetDepartureActionsFor(dispatcherA, 10).ToList();
        var newRequestAction = departureActions.FirstOrDefault(a => a.Action == DispatchAction.Request);

        Assert.IsNotNull(newRequestAction, "Request action should be available again after Revoke");
    }

    [TestMethod]
    public void AfterAcceptRevokeActionBecomesAvailableAtDepartureStation()
    {
        var dispatcherA = _broker.GetDispatchers().First(d => d.Signature == "A");
        var dispatcherB = _broker.GetDispatchers().First(d => d.Signature == "B");
        SetTrainToRunning(dispatcherA);
        RequestAndAccept(dispatcherA, dispatcherB);

        var departureActions = _broker.GetDepartureActionsFor(dispatcherA, 10).ToList();
        var revokeAction = departureActions.FirstOrDefault(a => a.Action == DispatchAction.Revoke);

        Assert.IsNotNull(revokeAction, "Revoke action should be available at departure station after Accept");
    }

    [TestMethod]
    public void AfterAcceptRevokeActionBecomesAvailableAtArrivalStation()
    {
        var dispatcherA = _broker.GetDispatchers().First(d => d.Signature == "A");
        var dispatcherB = _broker.GetDispatchers().First(d => d.Signature == "B");
        SetTrainToRunning(dispatcherA);
        RequestAndAccept(dispatcherA, dispatcherB);

        var arrivalActions = _broker.GetArrivalActionsFor(dispatcherB, 10).ToList();
        var revokeAction = arrivalActions.FirstOrDefault(a => a.Action == DispatchAction.Revoke);

        Assert.IsNotNull(revokeAction, "Revoke action should be available at arrival station after Accept");
    }

    [TestMethod]
    public void ExecuteRevokeFromAcceptedChangesStateToRevoked()
    {
        var dispatcherA = _broker.GetDispatchers().First(d => d.Signature == "A");
        var dispatcherB = _broker.GetDispatchers().First(d => d.Signature == "B");
        SetTrainToRunning(dispatcherA);
        RequestAndAccept(dispatcherA, dispatcherB);

        var revokeAction = _broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.Revoke);
        var result = revokeAction.Execute();

        Assert.IsTrue(result.HasValue, "Revoke action should succeed");
        Assert.AreEqual(DispatchState.Revoked, revokeAction.Section.State);
    }

    [TestMethod]
    public void AfterRevokeFromAcceptedRequestActionBecomesAvailableAgain()
    {
        var dispatcherA = _broker.GetDispatchers().First(d => d.Signature == "A");
        var dispatcherB = _broker.GetDispatchers().First(d => d.Signature == "B");
        SetTrainToRunning(dispatcherA);
        RequestAndAccept(dispatcherA, dispatcherB);

        // Revoke
        var revokeAction = _broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.Revoke);
        revokeAction.Execute();

        // Request should be available again
        var departureActions = _broker.GetDepartureActionsFor(dispatcherA, 10).ToList();
        var newRequestAction = departureActions.FirstOrDefault(a => a.Action == DispatchAction.Request);

        Assert.IsNotNull(newRequestAction, "Request action should be available again after Revoke from Accepted");
    }

    #endregion

    #region UndoTrainState Tests

    [TestMethod]
    public void PlannedTrainHasNoPreviousState()
    {
        var dispatcherA = _broker.GetDispatchers().First(d => d.Signature == "A");
        var departureActions = _broker.GetDepartureActionsFor(dispatcherA, 10).ToList();

        var undoAction = departureActions.FirstOrDefault(a => a.Action == DispatchAction.UndoTrainState);
        Assert.IsNull(undoAction, "Planned train should have no UndoTrainState action");
    }

    [TestMethod]
    public void AfterMannedUndoTrainStateActionBecomesAvailable()
    {
        var dispatcherA = _broker.GetDispatchers().First(d => d.Signature == "A");
        var mannedAction = _broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.Manned);
        mannedAction.Execute();

        var departureActions = _broker.GetDepartureActionsFor(dispatcherA, 10).ToList();
        var undoAction = departureActions.FirstOrDefault(a => a.Action == DispatchAction.UndoTrainState);

        Assert.IsNotNull(undoAction, "UndoTrainState action should be available after Manned");
    }

    [TestMethod]
    public void ExecuteUndoTrainStateRevertsFromMannedToPlanned()
    {
        var dispatcherA = _broker.GetDispatchers().First(d => d.Signature == "A");
        var mannedAction = _broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.Manned);
        mannedAction.Execute();

        var undoAction = _broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.UndoTrainState);
        var result = undoAction.Execute();

        Assert.IsTrue(result.HasValue, "UndoTrainState action should succeed");
        Assert.AreEqual(Trains.TrainState.Planned, undoAction.Section.Departure.Train.State);
    }

    [TestMethod]
    public void RunningTrainHasNoUndoAction()
    {
        var dispatcherA = _broker.GetDispatchers().First(d => d.Signature == "A");
        SetTrainToRunning(dispatcherA);

        var departureActions = _broker.GetDepartureActionsFor(dispatcherA, 10).ToList();
        var undoAction = departureActions.FirstOrDefault(a => a.Action == DispatchAction.UndoTrainState);

        Assert.IsNull(undoAction, "Running train should not have UndoTrainState action");
    }

    [TestMethod]
    public void ExecuteUndoTrainStateRevertsFromCanceledToPlanned()
    {
        var dispatcherA = _broker.GetDispatchers().First(d => d.Signature == "A");

        // Cancel a planned train
        var canceledAction = _broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.Canceled);
        canceledAction.Execute();

        // Undo the cancellation
        var undoAction = _broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.UndoTrainState);
        var result = undoAction.Execute();

        Assert.IsTrue(result.HasValue, "UndoTrainState action should succeed");
        Assert.AreEqual(Trains.TrainState.Planned, undoAction.Section.Departure.Train.State);
    }

    [TestMethod]
    public void ExecuteUndoTrainStateRevertsFromAbortedToRunning()
    {
        var dispatcherA = _broker.GetDispatchers().First(d => d.Signature == "A");
        var dispatcherB = _broker.GetDispatchers().First(d => d.Signature == "B");
        SetTrainToRunning(dispatcherA);

        // Complete A -> B journey
        RequestAndAccept(dispatcherA, dispatcherB);
        _broker.GetDepartureActionsFor(dispatcherA, 10).First(a => a.Action == DispatchAction.Depart).Execute();
        _broker.GetArrivalActionsFor(dispatcherB, 10).First(a => a.Action == DispatchAction.Arrive).Execute();

        // Abort the train on the second section
        var abortedAction = _broker.GetDepartureActionsFor(dispatcherB, 10)
            .First(a => a.Action == DispatchAction.Aborted);
        abortedAction.Execute();

        // Undo the abort
        var undoAction = _broker.GetDepartureActionsFor(dispatcherB, 10)
            .First(a => a.Action == DispatchAction.UndoTrainState);
        var result = undoAction.Execute();

        Assert.IsTrue(result.HasValue, "UndoTrainState action should succeed");
        Assert.AreEqual(Trains.TrainState.Running, undoAction.Section.Departure.Train.State);
    }

    [TestMethod]
    public void AfterUndoTrainStateNoPreviousStateRemains()
    {
        var dispatcherA = _broker.GetDispatchers().First(d => d.Signature == "A");

        // Manned -> has PreviousState
        var mannedAction = _broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.Manned);
        mannedAction.Execute();

        // Undo -> clears PreviousState
        var undoAction = _broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.UndoTrainState);
        undoAction.Execute();

        // UndoTrainState should no longer be available
        var departureActions = _broker.GetDepartureActionsFor(dispatcherA, 10).ToList();
        var undoActionAfter = departureActions.FirstOrDefault(a => a.Action == DispatchAction.UndoTrainState);

        Assert.IsNull(undoActionAfter, "UndoTrainState action should not be available after undo (no previous state)");
    }

    [TestMethod]
    public void UndoTrainStateIsTrainAction()
    {
        var dispatcherA = _broker.GetDispatchers().First(d => d.Signature == "A");

        var mannedAction = _broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.Manned);
        mannedAction.Execute();

        var undoAction = _broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.UndoTrainState);

        Assert.IsTrue(undoAction.IsTrainAction, "UndoTrainState should be classified as a train action");
    }

    [TestMethod]
    public void UndoMannedDisplayNameIsCorrect()
    {
        var dispatcherA = _broker.GetDispatchers().First(d => d.Signature == "A");

        var mannedAction = _broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.Manned);
        mannedAction.Execute();

        var undoAction = _broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.UndoTrainState);

        Assert.AreEqual("Undo Manned", undoAction.DisplayName);
    }

    [TestMethod]
    public void UndoCanceledDisplayNameIsCorrect()
    {
        var dispatcherA = _broker.GetDispatchers().First(d => d.Signature == "A");

        var canceledAction = _broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.Canceled);
        canceledAction.Execute();

        var undoAction = _broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.UndoTrainState);

        Assert.AreEqual("Undo Canceled", undoAction.DisplayName);
    }

    [TestMethod]
    public void UndoAbortedDisplayNameIsCorrect()
    {
        var dispatcherA = _broker.GetDispatchers().First(d => d.Signature == "A");
        var dispatcherB = _broker.GetDispatchers().First(d => d.Signature == "B");
        SetTrainToRunning(dispatcherA);

        // Complete A -> B journey
        RequestAndAccept(dispatcherA, dispatcherB);
        _broker.GetDepartureActionsFor(dispatcherA, 10).First(a => a.Action == DispatchAction.Depart).Execute();
        _broker.GetArrivalActionsFor(dispatcherB, 10).First(a => a.Action == DispatchAction.Arrive).Execute();

        // Abort the train on the second section
        var abortedAction = _broker.GetDepartureActionsFor(dispatcherB, 10)
            .First(a => a.Action == DispatchAction.Aborted);
        abortedAction.Execute();

        var undoAction = _broker.GetDepartureActionsFor(dispatcherB, 10)
            .First(a => a.Action == DispatchAction.UndoTrainState);

        Assert.AreEqual("Undo Aborted", undoAction.DisplayName);
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
