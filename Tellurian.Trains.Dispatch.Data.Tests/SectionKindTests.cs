using Microsoft.Extensions.Logging.Abstractions;
using Tellurian.Trains.Dispatch.Brokers;
using Tellurian.Trains.Dispatch.Data.Timetable;
using Tellurian.Trains.Schedules.Model.PlanFiles;

namespace Tellurian.Trains.Dispatch.Data.Tests;

/// <summary>
/// How trains are cleared when they start, end or leave the route away from manned stations,
/// with trains from the Värnamo plan. Day 2 is used, since day 1 excludes trains whose duty starts before 05:00.
/// </summary>
[TestClass]
public sealed class SectionKindTests
{
    private const string VarnamoPlanFile = "TestData/Varnamo-Moduldagar.json";
    private const int Day = 2;

    private Broker _broker = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public async Task TestInitialize()
    {
        var plan = PlanFile.Read(await File.ReadAllTextAsync(VarnamoPlanFile, TestContext.CancellationToken));
        _broker = new Broker(new TimetableBrokerDataProvider(plan), new InMemoryCompositeStateProvider(), new TestTimeProvider(), NullLogger<Broker>.Instance);
        await _broker.InitAsync(isRestart: false, TestContext.CancellationToken);
    }

    [TestMethod]
    public void TrainToUnmannedStationAlongDispatchStretch_IsClearedByBothEndsOfTheStretch()
    {
        // Kp 5624 Rise → Tremetsrud, which is along the dispatch stretch Rise–Spiten.
        var section = SectionsOf(5624).Single();

        Assert.AreEqual(TrainSectionKind.Leaving, section.Kind);
        Assert.AreEqual("Rse", section.From.Signature);
        Assert.AreEqual("Tre", section.To.Signature);
        Assert.AreEqual("Spn", section.ArrivalDispatcher!.Signature, "Spiten sees the train among its arrivals.");

        Do(section, "Rse", DispatchAction.AssignDriver);
        Do(section, "Rse", DispatchAction.Request);
        Do(section, "Spn", DispatchAction.Accept);
        Do(section, "Rse", DispatchAction.Depart);
        Assert.IsFalse(Can(section, "Spn", DispatchAction.Arrive));
        Do(section, "Rse", DispatchAction.Arrive);
        Assert.AreEqual(DispatchState.Arrived, section.State);
    }

    [TestMethod]
    public void TrainFromUnmannedStationAlongDispatchStretch_IsClearedByBothEndsOfTheStretch()
    {
        // Kp 5625 Tremetsrud → Rise.
        var section = SectionsOf(5625).Single();

        Assert.AreEqual(TrainSectionKind.Entering, section.Kind);
        Assert.AreEqual("Tre", section.From.Signature);
        Assert.AreEqual("Spn", section.DepartureDispatcher!.Signature, "Spiten sees the train among its departures.");

        Do(section, "Rse", DispatchAction.AssignDriver);
        Do(section, "Rse", DispatchAction.Request);
        Do(section, "Spn", DispatchAction.Accept);
        Do(section, "Rse", DispatchAction.Depart);
        Do(section, "Rse", DispatchAction.Arrive);
        Assert.AreEqual(DispatchState.Arrived, section.State);
    }

    [TestMethod]
    public void TrainToBranch_IsDoneWhenDepartedFromTheMannedStation()
    {
        // G 4126 Nättelund → Mohult → Sura by, an industrial area on no dispatch stretch.
        var sections = SectionsOf(4126);

        CollectionAssert.AreEqual(new[] { TrainSectionKind.Normal, TrainSectionKind.OneDispatcherOutbound }, sections.Select(s => s.Kind).ToArray());
        var branch = sections[1];
        Assert.AreEqual("Sub", branch.To.Signature);
        Assert.HasCount(1, branch.Dispatchers);

        Do(sections[0], "Nlu", DispatchAction.AssignDriver);
        Do(sections[0], "Nlu", DispatchAction.Request);
        Do(sections[0], "Mot", DispatchAction.Accept);
        Do(sections[0], "Nlu", DispatchAction.Depart);
        Do(sections[0], "Mot", DispatchAction.Arrive);
        Do(branch, "Mot", DispatchAction.Depart);
        Assert.AreEqual(DispatchState.Arrived, branch.State);
        Assert.IsFalse(branch.IsVisibleForDispatcher);
    }

    [TestMethod]
    public void TrainFromBranch_IsOnItsWayWhenTheMannedStationAccepts()
    {
        // G 4121 Sura by → Mohult → Nättelund.
        var sections = SectionsOf(4121);

        CollectionAssert.AreEqual(new[] { TrainSectionKind.OneDispatcherInbound, TrainSectionKind.Normal }, sections.Select(s => s.Kind).ToArray());
        var branch = sections[0];
        Assert.AreEqual("Mot", branch.ArrivalDispatcher!.Signature);

        Do(branch, "Mot", DispatchAction.AssignDriver);
        Assert.IsFalse(Can(branch, "Mot", DispatchAction.Request), "The driver asks by phone.");
        Do(branch, "Mot", DispatchAction.Accept);
        Assert.AreEqual(DispatchState.Departed, branch.State);
        Do(branch, "Mot", DispatchAction.Arrive);
        Assert.IsTrue(Can(sections[1], "Mot", DispatchAction.Request));
    }

    [TestMethod]
    public void TrainTowardsDriverWorkedEnd_IsClearedBySteinsnesAlone()
    {
        // L 406 Steinsnes → Steinsnes vest → Triangeln → Tveiten. Steinsnes works the junction Steinsnes vest, so
        // Skuggevik is not involved; Triangeln–Steinsnes vest has the driver working the points at Triangeln.
        var section = SectionsOf(406).Single();

        Assert.AreEqual(TrainSectionKind.OneDispatcherOutbound, section.Kind);
        Assert.AreEqual("Ste", section.From.Signature);
        Assert.AreEqual("Tri", section.To.Signature);
        Assert.AreEqual("Ste", section.Dispatchers.Single().Signature);

        Do(section, "Ste", DispatchAction.AssignDriver);
        Do(section, "Ste", DispatchAction.Depart);
        Assert.AreEqual(DispatchState.Arrived, section.State);
    }

    [TestMethod]
    public void TrainFromDriverWorkedEnd_DriverCallsSteinsnes()
    {
        // L 403 Tveiten → Triangeln → Steinsnes vest → Steinsnes: the driver calls Steinsnes for permission to enter
        // Triangeln–Steinsnes vest, and is given the go all the way to Steinsnes.
        var section = SectionsOf(403).Single();

        Assert.AreEqual(TrainSectionKind.OneDispatcherInbound, section.Kind);
        Assert.AreEqual("Tri", section.From.Signature);
        Assert.AreEqual("Ste", section.To.Signature);

        Do(section, "Ste", DispatchAction.AssignDriver);
        Do(section, "Ste", DispatchAction.Accept);
        Assert.AreEqual("Stv", section.NextControlPoint?.Signature, "Steinsnes works the signals at Steinsnes vest.");
        Do(section, "Ste", DispatchAction.Pass);
        Do(section, "Ste", DispatchAction.Arrive);
        Assert.AreEqual(DispatchState.Arrived, section.State);
    }

    [TestMethod]
    public void TrainThroughJunctionWorkedFromAfar_IsAskedForBeforeLeaving()
    {
        // A train Gårdsjö → Tjärby → Rotebro: Gårdsjö works the junction Tjärby and asks Rotebro before sending it out.
        var section = _broker.TrainSections.First(s =>
            s.Departure.Train.OperatingDay == Day && s.From.Signature == "Gdö" && s.To.Signature == "R");

        Assert.AreEqual(TrainSectionKind.Normal, section.Kind);
        Assert.AreEqual("Gdö", section.RequestingDispatcher!.Signature);
        Assert.AreEqual("R", section.AcceptingDispatcher!.Signature);
        Assert.AreEqual("Tby", string.Join(",", section.IntermediateControlPoints.Select(p => p.Signature)));
    }

    [TestMethod]
    public void TrainPassingPlacesWithoutDispatcher_OnlyStopsForControlledSignals()
    {
        // 3133 Ebbehøj → Rytterhagen → Holmlid → Tjärby → Gårdsjö: the industrial areas Rytterhagen and Holmlid are
        // passed without any dispatcher; Tjärby is a signal controlled by Gårdsjö.
        var section = SectionsOf(3133).Single(s => s.From.Signature == "Ebh");
        Assert.AreEqual("Gdö", section.To.Signature);

        Do(SectionsOf(3133)[0], "Gbg", DispatchAction.AssignDriver);
        Do(SectionsOf(3133)[0], "Gbg", DispatchAction.Request);
        Do(SectionsOf(3133)[0], "Ebh", DispatchAction.Accept);
        Do(SectionsOf(3133)[0], "Gbg", DispatchAction.Depart);
        Do(SectionsOf(3133)[0], "Ebh", DispatchAction.Arrive);
        Do(section, "Ebh", DispatchAction.Request);
        Do(section, "Gdö", DispatchAction.Accept);
        Do(section, "Ebh", DispatchAction.Depart);

        Assert.AreEqual("Tby", section.NextControlPoint?.Signature);
        Do(section, "Gdö", DispatchAction.Pass);
        Do(section, "Gdö", DispatchAction.Arrive);
        Assert.AreEqual(DispatchState.Arrived, section.State);
    }

    private List<TrainSection> SectionsOf(int trainNumber) =>
        [.. _broker.TrainSections
            .Where(s => s.Departure.Train.Identity.Number == trainNumber && s.Departure.Train.OperatingDay == Day)
            .OrderBy(s => s.Departure.SequenceNumber)];

    private IDispatcher Dispatcher(string signature) => _broker.GetDispatchers().Single(d => d.Signature == signature);

    private IEnumerable<ActionContext> ActionsFor(TrainSection section, string signature)
    {
        var dispatcher = Dispatcher(signature);
        return _broker.GetDepartureActionsFor(dispatcher, int.MaxValue)
            .Concat(_broker.GetArrivalActionsFor(dispatcher, int.MaxValue))
            .Where(a => a.Section.Id == section.Id);
    }

    private bool Can(TrainSection section, string signature, DispatchAction action) =>
        ActionsFor(section, signature).Any(a => a.Action == action);

    private void Do(TrainSection section, string signature, DispatchAction action)
    {
        var context = ActionsFor(section, signature).FirstOrDefault(a => a.Action == action);
        Assert.IsNotNull(context, $"{signature} cannot {action} {section} in state {section.State}.");
        var result = context.Execute();
        Assert.IsTrue(result.HasValue, result.Error);
    }
}
