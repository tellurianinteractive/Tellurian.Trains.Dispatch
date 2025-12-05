using Microsoft.Extensions.Logging.Abstractions;
using Tellurian.Trains.Dispatch.Brokers;
using Tellurian.Trains.Dispatch.Trains;

namespace Tellurian.Trains.Dispatch.Tests;

/// <summary>
/// Tests for simple two-station dispatch scenario without block signals.
/// Verifies dispatcher actions and state transitions.
/// </summary>
[TestClass]
public class SimpleDispatchTests
{
    private Broker _broker = default!;
    private StationDispatcher _departureDispatcher = default!;
    private TrainSection _trainSection = default!;

    [TestInitialize]
    public async Task Setup()
    {
        var configuration = new SimpleDispatchConfiguration();
        var stateProvider = new InMemoryStateProvider();
        var timeProvider = new TestTimeProvider();
        var logger = NullLogger<Broker>.Instance;

        _broker = new Broker(configuration, stateProvider, timeProvider, logger);
        await _broker.InitAsync(isRestart: false);

        var dispatchers = _broker.GetDispatchers().Cast<StationDispatcher>().ToList();
        _departureDispatcher = dispatchers.Single(d => d.Name == "Alpha");
        _trainSection = _departureDispatcher.Departures.Single();
    }

    #region 1. Happy Path: Complete Dispatch Workflow

    [TestMethod]
    public void Request_Accept_Departed_Arrived_CompletesDispatch()
    {
        // Initial state
        Assert.AreEqual(DispatchState.None, _trainSection.State);
        Assert.AreEqual(TrainState.Planned, _trainSection.Departure.Train.State);

        // Departure dispatcher requests
        var requestAction = GetDepartureAction(DispatchState.Requested);
        Assert.IsTrue(requestAction());
        Assert.AreEqual(DispatchState.Requested, _trainSection.State);
        Assert.AreEqual(TrainState.Manned, _trainSection.Departure.Train.State);

        // Arrival dispatcher accepts
        var acceptAction = GetArrivalAction(DispatchState.Accepted);
        Assert.IsTrue(acceptAction());
        Assert.AreEqual(DispatchState.Accepted, _trainSection.State);

        // Departure dispatcher marks departed
        var departedAction = GetDepartureAction(DispatchState.Departed);
        Assert.IsTrue(departedAction());
        Assert.AreEqual(DispatchState.Departed, _trainSection.State);
        Assert.AreEqual(TrainState.Running, _trainSection.Departure.Train.State);

        // Arrival dispatcher marks arrived
        var arrivedAction = GetArrivalAction(DispatchState.Arrived);
        Assert.IsTrue(arrivedAction());
        Assert.AreEqual(DispatchState.Arrived, _trainSection.State);
        Assert.AreEqual(TrainState.Completed, _trainSection.Departure.Train.State);
    }

    #endregion

    #region 2. Request/Revoke Cycles

    [TestMethod]
    public void Request_ThenRevoke_ReturnsToRequestableState()
    {
        // Request
        var requestAction = GetDepartureAction(DispatchState.Requested);
        Assert.IsTrue(requestAction());
        Assert.AreEqual(DispatchState.Requested, _trainSection.State);

        // Revoke
        var revokeAction = GetDepartureAction(DispatchState.Revoked);
        Assert.IsTrue(revokeAction());
        Assert.AreEqual(DispatchState.Revoked, _trainSection.State);

        // Can request again
        Assert.IsTrue(HasDepartureAction(DispatchState.Requested));
    }

    [TestMethod]
    public void Request_Accept_Revoke_ReturnsToRequestableState()
    {
        // Request
        GetDepartureAction(DispatchState.Requested)();

        // Accept
        GetArrivalAction(DispatchState.Accepted)();
        Assert.AreEqual(DispatchState.Accepted, _trainSection.State);

        // Revoke (by departure dispatcher)
        var revokeAction = GetDepartureAction(DispatchState.Revoked);
        Assert.IsTrue(revokeAction());
        Assert.AreEqual(DispatchState.Revoked, _trainSection.State);

        // Can request again
        Assert.IsTrue(HasDepartureAction(DispatchState.Requested));
    }

    #endregion

    #region 3. Rejection Flow

    [TestMethod]
    public void Request_Reject_AllowsNewRequest()
    {
        // Request
        GetDepartureAction(DispatchState.Requested)();

        // Reject
        var rejectAction = GetArrivalAction(DispatchState.Rejected);
        Assert.IsTrue(rejectAction());
        Assert.AreEqual(DispatchState.Rejected, _trainSection.State);

        // Departure can request again
        Assert.IsTrue(HasDepartureAction(DispatchState.Requested));

        // Arrival has no actions
        Assert.IsFalse(_trainSection.ArrivalActions.Any());
    }

    #endregion

    #region 4. Available Actions Verification

    [TestMethod]
    public void InitialState_DepartureHasRequestAction_ArrivalHasNoActions()
    {
        Assert.AreEqual(DispatchState.None, _trainSection.State);

        // Departure has Request action
        Assert.IsTrue(HasDepartureAction(DispatchState.Requested));

        // Arrival has no actions
        Assert.IsFalse(_trainSection.ArrivalActions.Any());
    }

    [TestMethod]
    public void AfterRequest_DepartureHasRevoke_ArrivalHasAcceptReject()
    {
        GetDepartureAction(DispatchState.Requested)();

        // Departure has Revoke
        Assert.IsTrue(HasDepartureAction(DispatchState.Revoked));
        Assert.IsFalse(HasDepartureAction(DispatchState.Requested));

        // Arrival has Accept and Reject
        Assert.IsTrue(HasArrivalAction(DispatchState.Accepted));
        Assert.IsTrue(HasArrivalAction(DispatchState.Rejected));
    }

    [TestMethod]
    public void AfterAccept_DepartureHasDepartedAndRevoke()
    {
        GetDepartureAction(DispatchState.Requested)();
        GetArrivalAction(DispatchState.Accepted)();

        // Departure has Departed and Revoke
        Assert.IsTrue(HasDepartureAction(DispatchState.Departed));
        Assert.IsTrue(HasDepartureAction(DispatchState.Revoked));

        // Arrival has no actions
        Assert.IsFalse(_trainSection.ArrivalActions.Any());
    }

    [TestMethod]
    public void AfterDeparted_OnlyArrivalHasArrivedAction()
    {
        GetDepartureAction(DispatchState.Requested)();
        GetArrivalAction(DispatchState.Accepted)();
        GetDepartureAction(DispatchState.Departed)();

        // Departure has no actions
        Assert.IsFalse(_trainSection.DepartureActions.Any());

        // Arrival has only Arrived
        Assert.IsTrue(HasArrivalAction(DispatchState.Arrived));
        Assert.AreEqual(1, _trainSection.ArrivalActions.Count());
    }

    [TestMethod]
    public void AfterArrived_NoMoreActionsAvailable()
    {
        GetDepartureAction(DispatchState.Requested)();
        GetArrivalAction(DispatchState.Accepted)();
        GetDepartureAction(DispatchState.Departed)();
        GetArrivalAction(DispatchState.Arrived)();

        // No more actions
        Assert.IsFalse(_trainSection.DepartureActions.Any());
        Assert.IsFalse(_trainSection.ArrivalActions.Any());

        // TrainSection is completed
        Assert.AreEqual(DispatchState.Arrived, _trainSection.State);
    }

    #endregion

    #region 5. Invalid State Transitions

    [TestMethod]
    public void CannotAccept_FromNoneState()
    {
        Assert.AreEqual(DispatchState.None, _trainSection.State);

        // Accept action should not be available
        Assert.IsFalse(HasArrivalAction(DispatchState.Accepted));
    }

    [TestMethod]
    public void CannotDepart_BeforeAccepted()
    {
        GetDepartureAction(DispatchState.Requested)();

        // Departed action should not be available
        Assert.IsFalse(HasDepartureAction(DispatchState.Departed));
    }

    [TestMethod]
    public void CannotArrive_BeforeDeparted()
    {
        GetDepartureAction(DispatchState.Requested)();
        GetArrivalAction(DispatchState.Accepted)();

        // Arrived action should not be available
        Assert.IsFalse(HasArrivalAction(DispatchState.Arrived));
    }

    [TestMethod]
    public void CannotRequest_WhenAlreadyRequested()
    {
        GetDepartureAction(DispatchState.Requested)();

        // Request action should not be available
        Assert.IsFalse(HasDepartureAction(DispatchState.Requested));
    }

    #endregion

    #region 6. Train State Synchronization

    [TestMethod]
    public void Request_SetsTrainToManned()
    {
        Assert.AreEqual(TrainState.Planned, _trainSection.Departure.Train.State);

        GetDepartureAction(DispatchState.Requested)();

        Assert.AreEqual(TrainState.Manned, _trainSection.Departure.Train.State);
    }

    [TestMethod]
    public void Departed_SetsTrainToRunning_OnFirstSection()
    {
        GetDepartureAction(DispatchState.Requested)();
        GetArrivalAction(DispatchState.Accepted)();

        GetDepartureAction(DispatchState.Departed)();

        Assert.AreEqual(TrainState.Running, _trainSection.Departure.Train.State);
    }

    [TestMethod]
    public void Arrived_SetsTrainToCompleted_OnLastSection()
    {
        GetDepartureAction(DispatchState.Requested)();
        GetArrivalAction(DispatchState.Accepted)();
        GetDepartureAction(DispatchState.Departed)();

        GetArrivalAction(DispatchState.Arrived)();

        Assert.AreEqual(TrainState.Completed, _trainSection.Departure.Train.State);
    }

    [TestMethod]
    public void Departed_SetsDepartureTime()
    {
        GetDepartureAction(DispatchState.Requested)();
        GetArrivalAction(DispatchState.Accepted)();

        // Observed is a struct with default TimeSpan values (00:00:00)
        Assert.AreEqual(TimeSpan.Zero, _trainSection.Departure.Observed.DepartureTime);

        GetDepartureAction(DispatchState.Departed)();

        // After departure, observed time should match scheduled (10:00)
        Assert.AreEqual(TimeSpan.FromHours(10), _trainSection.Departure.Observed.DepartureTime);
    }

    [TestMethod]
    public void Arrived_SetsArrivalTime()
    {
        GetDepartureAction(DispatchState.Requested)();
        GetArrivalAction(DispatchState.Accepted)();
        GetDepartureAction(DispatchState.Departed)();

        Assert.AreEqual(TimeSpan.Zero, _trainSection.Arrival.Observed.ArrivalTime);

        GetArrivalAction(DispatchState.Arrived)();

        // After arrival, observed time should be set (10:30)
        Assert.AreEqual(TimeSpan.FromHours(10.5), _trainSection.Arrival.Observed.ArrivalTime);
    }

    #endregion

    #region 7. Canceled/Aborted Train Handling

    [TestMethod]
    public void CanceledTrain_HasNoDispatchActions()
    {
        GetDepartureAction(DispatchState.Requested)();

        // Cancel the train
        _trainSection.Departure.Train.State = TrainState.Canceled;

        // No dispatch actions available
        Assert.IsFalse(_trainSection.DepartureActions.Any());
        Assert.IsFalse(_trainSection.ArrivalActions.Any());
    }

    [TestMethod]
    public void AbortedTrain_AfterDeparted_HasClearAction()
    {
        GetDepartureAction(DispatchState.Requested)();
        GetArrivalAction(DispatchState.Accepted)();
        GetDepartureAction(DispatchState.Departed)();

        // Abort the train
        _trainSection.Departure.Train.State = TrainState.Aborted;

        // Clear action should be available
        var clearAction = _trainSection.ClearCanceledOrAbortedTrain;
        Assert.IsNotNull(clearAction);
    }

    [TestMethod]
    public void ClearCanceledTrain_RemovesFromActiveTrains()
    {
        GetDepartureAction(DispatchState.Requested)();
        GetArrivalAction(DispatchState.Accepted)();
        GetDepartureAction(DispatchState.Departed)();

        // Train should be on the stretch
        Assert.Contains(_trainSection, _trainSection.DispatchStretch.ActiveTrains);

        // Cancel and clear
        _trainSection.Departure.Train.State = TrainState.Canceled;
        var clearAction = _trainSection.ClearCanceledOrAbortedTrain;
        Assert.IsNotNull(clearAction);
        Assert.IsTrue(clearAction());

        // Train should be removed
        Assert.DoesNotContain(_trainSection, _trainSection.DispatchStretch.ActiveTrains);
        Assert.AreEqual(DispatchState.Canceled, _trainSection.State);
    }

    [TestMethod]
    public void CanceledTrain_BeforeDeparted_HasNoClearAction()
    {
        GetDepartureAction(DispatchState.Requested)();
        GetArrivalAction(DispatchState.Accepted)();

        // Cancel before departing
        _trainSection.Departure.Train.State = TrainState.Canceled;

        // No clear action (train is not on the stretch)
        Assert.IsNull(_trainSection.ClearCanceledOrAbortedTrain);
    }

    #endregion

    #region Helper Methods

    private Func<bool> GetDepartureAction(DispatchState state) =>
        _trainSection.DepartureActions.Single(a => a.State == state).Action;

    private Func<bool> GetArrivalAction(DispatchState state) =>
        _trainSection.ArrivalActions.Single(a => a.State == state).Action;

    private bool HasDepartureAction(DispatchState state) =>
        _trainSection.DepartureActions.Any(a => a.State == state);

    private bool HasArrivalAction(DispatchState state) =>
        _trainSection.ArrivalActions.Any(a => a.State == state);

    #endregion
}
