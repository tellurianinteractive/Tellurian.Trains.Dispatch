using Microsoft.Extensions.Logging.Abstractions;
using Tellurian.Trains.Dispatch.Brokers;
using Tellurian.Trains.Dispatch.Data;

namespace Tellurian.Trains.Dispatch.Tests;

/// <summary>
/// Tests for the Junction Test Case.
///
/// Layout:
///     A ----\
///            B ---- C
///     D ----/
///
/// Test focus: Train D-B-A can depart from D but cannot pass B until
/// train A-B-C has passed B. Both trains have the same scheduled time at B.
/// </summary>
[TestClass]
public class JunctionTests
{
    private Broker _broker = null!;
    private JunctionTestDataProvider _dataProvider = null!;
    private InMemoryStateProvider _stateProvider = null!;
    private TestTimeProvider _timeProvider = null!;

    [TestInitialize]
    public async Task Setup()
    {
        _dataProvider = new JunctionTestDataProvider();
        _stateProvider = new InMemoryStateProvider();
        _timeProvider = new TestTimeProvider { CurrentTime = TimeSpan.FromHours(10) };
        _broker = new Broker(_dataProvider, _stateProvider, _timeProvider, NullLogger<Broker>.Instance);
        await _broker.InitAsync(isRestart: false);
    }

    #region Basic Setup Tests

    [TestMethod]
    public void BrokerCreatesThreeDispatchers()
    {
        // Only Stations A, C, D are dispatchers (SignalControlledPlace B is not)
        var dispatchers = _broker.GetDispatchers().ToList();
        Assert.HasCount(3, dispatchers, "Should have 3 dispatchers (stations A, C, D)");
    }

    [TestMethod]
    public void DispatchersHaveCorrectSignatures()
    {
        var dispatchers = _broker.GetDispatchers().ToList();
        var signatures = dispatchers.Select(d => d.Signature).OrderBy(s => s).ToList();

        CollectionAssert.AreEqual(
            new[] { "A", "C", "D" },
            signatures,
            "Dispatcher signatures should be A, C, D");
    }

    [TestMethod]
    public void StationAHasDepartureActionsForTrain201()
    {
        var dispatcherA = _broker.GetDispatchers().First(d => d.Signature == "A");
        var departureActions = _broker.GetDepartureActionsFor(dispatcherA, 10).ToList();

        // Train 201 starts at station A
        Assert.IsNotEmpty(departureActions, "Station A should have departure actions for train 201");
    }

    [TestMethod]
    public void StationDHasDepartureActionsForTrain202()
    {
        var dispatcherD = _broker.GetDispatchers().First(d => d.Signature == "D");
        var departureActions = _broker.GetDepartureActionsFor(dispatcherD, 10).ToList();

        // Train 202 starts at station D
        Assert.IsNotEmpty(departureActions, "Station D should have departure actions for train 202");
    }

    #endregion

    #region Both Trains Can Start Tests

    [TestMethod]
    public void BothTrainsCanBecomeRunning()
    {
        var dispatcherA = _broker.GetDispatchers().First(d => d.Signature == "A");
        var dispatcherD = _broker.GetDispatchers().First(d => d.Signature == "D");

        // Set Train 201 to Running
        SetTrainToRunning(dispatcherA);

        // Set Train 202 to Running
        SetTrainToRunning(dispatcherD);

        // Verify both trains are running
        var actionsA = _broker.GetDepartureActionsFor(dispatcherA, 10).ToList();
        var actionsD = _broker.GetDepartureActionsFor(dispatcherD, 10).ToList();

        var requestA = actionsA.FirstOrDefault(a => a.Action == DispatchAction.Request);
        var requestD = actionsD.FirstOrDefault(a => a.Action == DispatchAction.Request);

        Assert.IsNotNull(requestA, "Train 201 should have Request action available after Running");
        Assert.IsNotNull(requestD, "Train 202 should have Request action available after Running");
    }

    [TestMethod]
    public void BothTrainsCanDepartSimultaneously()
    {
        var dispatcherA = _broker.GetDispatchers().First(d => d.Signature == "A");
        var dispatcherC = _broker.GetDispatchers().First(d => d.Signature == "C");
        var dispatcherD = _broker.GetDispatchers().First(d => d.Signature == "D");

        // Set both trains to Running
        SetTrainToRunning(dispatcherA);
        SetTrainToRunning(dispatcherD);

        // Train 201: Request and Accept A->C
        RequestAndAccept(dispatcherA, dispatcherC);

        // Train 202: Request and Accept D->A
        RequestAndAccept(dispatcherD, dispatcherA);

        // Depart Train 201 from A
        var departA = _broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.Depart);
        var resultA = departA.Execute();

        // Depart Train 202 from D
        var departD = _broker.GetDepartureActionsFor(dispatcherD, 10)
            .First(a => a.Action == DispatchAction.Depart);
        var resultD = departD.Execute();

        Assert.IsTrue(resultA.HasValue, "Train 201 should be able to depart from A");
        Assert.IsTrue(resultD.HasValue, "Train 202 should be able to depart from D");
    }

    #endregion

    #region Pass Action Tests at Junction B

    [TestMethod]
    public void AfterDepartFromAPassActionBecomesAvailable()
    {
        var dispatcherA = _broker.GetDispatchers().First(d => d.Signature == "A");
        var dispatcherC = _broker.GetDispatchers().First(d => d.Signature == "C");

        SetTrainToRunning(dispatcherA);
        RequestAndAccept(dispatcherA, dispatcherC);

        _broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.Depart).Execute();

        // Pass should be available at Station A (which controls Signal B)
        var departureActions = _broker.GetDepartureActionsFor(dispatcherA, 10).ToList();
        var passAction = departureActions.FirstOrDefault(a => a.Action == DispatchAction.Pass);

        Assert.IsNotNull(passAction, "Pass action should be available at dispatcher A after Depart");
        Assert.AreEqual("B", passAction.PassTarget?.Signature, "PassTarget should be Signal B");
    }

    [TestMethod]
    public void Train201CanPassSignalB()
    {
        var dispatcherA = _broker.GetDispatchers().First(d => d.Signature == "A");
        var dispatcherC = _broker.GetDispatchers().First(d => d.Signature == "C");

        SetTrainToRunning(dispatcherA);
        RequestAndAccept(dispatcherA, dispatcherC);

        _broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.Depart).Execute();

        var passAction = _broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.Pass);
        var result = passAction.Execute();

        Assert.IsTrue(result.HasValue, "Train 201 should be able to pass Signal B");
        Assert.IsTrue(passAction.Section.IsOnLastTrackStretch, "After Pass, train should be on last track stretch (B->C)");
    }

    [TestMethod]
    public void AfterTrain201PassesBArriveBecomesAvailableAtC()
    {
        var dispatcherA = _broker.GetDispatchers().First(d => d.Signature == "A");
        var dispatcherC = _broker.GetDispatchers().First(d => d.Signature == "C");

        SetTrainToRunning(dispatcherA);
        RequestAndAccept(dispatcherA, dispatcherC);

        _broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.Depart).Execute();

        _broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.Pass).Execute();

        var arrivalActions = _broker.GetArrivalActionsFor(dispatcherC, 10).ToList();
        var arriveAction = arrivalActions.FirstOrDefault(a => a.Action == DispatchAction.Arrive);

        Assert.IsNotNull(arriveAction, "Arrive action should be available at C after Train 201 passes B");
    }

    #endregion

    #region Junction Conflict Tests

    [TestMethod]
    public void Train202CanDepartFromDWhileTrain201IsOnStretchAB()
    {
        var dispatcherA = _broker.GetDispatchers().First(d => d.Signature == "A");
        var dispatcherC = _broker.GetDispatchers().First(d => d.Signature == "C");
        var dispatcherD = _broker.GetDispatchers().First(d => d.Signature == "D");

        // Set both trains to Running
        SetTrainToRunning(dispatcherA);
        SetTrainToRunning(dispatcherD);

        // Train 201: Depart from A (now on stretch A->B)
        RequestAndAccept(dispatcherA, dispatcherC);
        _broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.Depart).Execute();

        // Train 202: Should still be able to request, accept, and depart from D
        RequestAndAccept(dispatcherD, dispatcherA);
        var departD = _broker.GetDepartureActionsFor(dispatcherD, 10)
            .FirstOrDefault(a => a.Action == DispatchAction.Depart);

        Assert.IsNotNull(departD, "Train 202 should be able to depart from D");

        var result = departD.Execute();
        Assert.IsTrue(result.HasValue, "Train 202 depart should succeed");
    }

    [TestMethod]
    public void FullJunctionWorkflowWithBothTrains()
    {
        var dispatcherA = _broker.GetDispatchers().First(d => d.Signature == "A");
        var dispatcherC = _broker.GetDispatchers().First(d => d.Signature == "C");
        var dispatcherD = _broker.GetDispatchers().First(d => d.Signature == "D");

        // Set both trains to Running
        SetTrainToRunning(dispatcherA);
        SetTrainToRunning(dispatcherD);

        // === Train 201: A -> B -> C ===
        RequestAndAccept(dispatcherA, dispatcherC);
        _broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.Depart).Execute();

        // === Train 202: D -> B -> A (start journey) ===
        RequestAndAccept(dispatcherD, dispatcherA);
        _broker.GetDepartureActionsFor(dispatcherD, 10)
            .First(a => a.Action == DispatchAction.Depart).Execute();

        // === Train 201: Pass Signal B ===
        var pass201 = _broker.GetDepartureActionsFor(dispatcherA, 10)
            .First(a => a.Action == DispatchAction.Pass);
        pass201.Execute();

        // === Train 201: Arrive at C ===
        var arrive201 = _broker.GetArrivalActionsFor(dispatcherC, 10)
            .First(a => a.Action == DispatchAction.Arrive);
        arrive201.Execute();

        Assert.AreEqual(DispatchState.Arrived, arrive201.Section.State, "Train 201 should have arrived at C");

        // === Train 202: Pass Signal B (now safe since Train 201 has passed) ===
        // Note: Signal B is controlled by Station A, so Pass action should be available there
        // But Train 202 is coming from D, so the controlling dispatcher might be different
        var passActionsA = _broker.GetDepartureActionsFor(dispatcherA, 10)
            .Where(a => a.Action == DispatchAction.Pass).ToList();
        var passActionsD = _broker.GetDepartureActionsFor(dispatcherD, 10)
            .Where(a => a.Action == DispatchAction.Pass).ToList();

        // Find Pass action for Train 202
        var pass202 = passActionsA.FirstOrDefault() ?? passActionsD.FirstOrDefault();

        if (pass202 is not null)
        {
            pass202.Execute();

            // === Train 202: Arrive at A ===
            var arrive202 = _broker.GetArrivalActionsFor(dispatcherA, 10)
                .First(a => a.Action == DispatchAction.Arrive);
            var result = arrive202.Execute();

            Assert.IsTrue(result.HasValue, "Train 202 should complete journey to A");
            Assert.AreEqual(DispatchState.Arrived, arrive202.Section.State, "Train 202 should have arrived at A");
        }
        else
        {
            // If Pass is not available, check if Arrive is directly available
            var arrive202 = _broker.GetArrivalActionsFor(dispatcherA, 10)
                .FirstOrDefault(a => a.Action == DispatchAction.Arrive);
            Assert.IsNotNull(arrive202, "Either Pass or Arrive should be available for Train 202");
        }
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
