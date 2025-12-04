using System.Text.Json.Serialization;
using Tellurian.Trains.Dispatch;
using Tellurian.Trains.Dispatch.Trains;
using Tellurian.Trains.Dispatch.Utilities;

namespace Tellurian.Trains.Dispatch;

/// <summary>
/// Represents a <see cref="Train">train's</see> movement between adjacents <see cref="Station">stations</see> that
/// is controlled by two  <see cref="IDispatcher"/>dispatchers.</see>
/// </summary>
public class TrainStretch
{

    internal int Id { get; set { field = value.OrNextId; } }
    public DispatchStretch DispatchStretch { get; }
    public TrainStationCall Departure { get; }
    public TrainStationCall Arrival { get; }
    public DispatchState State { get; internal set; }
    public IList<BlockSignalPassage> BlockSignalPassages { get; internal set; } = [];

    public override int GetHashCode() => Id.GetHashCode();
    public override bool Equals(object? obj) => obj is TrainStretch section && section.Id == Id;
    public override string ToString() => $"";

    [JsonIgnore]
    public ITimeProvider TimeProvider { get; }

    [JsonIgnore]
    private readonly Dictionary<TrainState, Func<bool>> TrainActions = [];
    [JsonIgnore]
    private readonly Dictionary<DispatchState, Func<bool>> DispatchActions = [];
    [JsonIgnore]
    private readonly Dictionary<int, Func<bool>> BlockSignalPassageActions = [];

    public IEnumerable<Func<bool>> ArrivalActions => DispatchActions
        .Where(k => k.Key.IsIn(this.ArrivalStates))
        .Select(k => k.Value);
    public IEnumerable<Func<bool>> DepartureActions => DispatchActions
        .Where(k => k.Key.IsIn(this.DepartureStates))
        .Select(k => k.Value);

    /// <summary>
    /// Gets block signal passage actions for the specified dispatcher.
    /// Only returns actions for block signals controlled by this dispatcher
    /// that are the next in sequence to be passed.
    /// </summary>
    public IEnumerable<Func<bool>> GetBlockSignalActionsFor(IDispatcher dispatcher) =>
        BlockSignalPassageActions
            .Where(kvp =>
                kvp.Key == this.CurrentBlockIndex &&
                kvp.Key < BlockSignalPassages.Count &&
                BlockSignalPassages[kvp.Key].IsExpected &&
                BlockSignalPassages[kvp.Key].BlockSignal.ControlledBy.Name == dispatcher.Name)
            .Select(kvp => kvp.Value);

    /// <summary>
    /// Gets the clear action if available.
    /// Only available when the train is canceled/aborted and on the stretch (Departed state).
    /// </summary>
    public Func<bool>? ClearCanceledOrAbortedTrain =>
        this.Train.IsCanceledOrAborted && State == DispatchState.Departed
            ? this.ClearFromStretch
            : null;

    private TrainStretch(DispatchStretch dispatchStretch, TrainStationCall departure, TrainStationCall arrival, ITimeProvider timeProvider)
    {
        TimeProvider = timeProvider;
        DispatchStretch = dispatchStretch;
        Departure = departure;
        Arrival = arrival;
        State = DispatchState.None;
        BlockSignalPassages = [.. this.CreateBlockSignalPassages()];
        CreateActionDictionaries();
    }
    /// <summary>
    /// Creates a <see cref="TrainStretch"/>
    /// </summary>
    /// <param name="dispatchStretch">The tracksection </param>
    /// <param name="departure"></param>
    /// <param name="arrival"></param>
    /// <returns></returns>
    public static Option<TrainStretch> Create(DispatchStretch dispatchStretch, TrainStationCall departure, TrainStationCall arrival, ITimeProvider timeProvider)
    {
        if (dispatchStretch.CanHave(departure, arrival))
        {
            var trainStretch = new TrainStretch(dispatchStretch, departure, arrival, timeProvider);
            return Option<TrainStretch>.Success(trainStretch);
        }
        else if (dispatchStretch.Reverse.CanHave(departure, arrival))
        {
            var trainStretch = new TrainStretch(dispatchStretch.Reverse, departure, arrival, timeProvider);
            return Option<TrainStretch>.Success(trainStretch);
        }
        else
        {
            return Option<TrainStretch>.Fail("Departure or arrival not on track stretch or they are not on the same train.");
        }
    }


    private void CreateActionDictionaries()
    {
        TrainActions.Add(TrainState.Canceled, this.SetCanceled);
        TrainActions.Add(TrainState.Aborted, this.SetAborted);

        DispatchActions.Add(DispatchState.Requested, this.Request);
        DispatchActions.Add(DispatchState.Accepted, this.Accept);
        DispatchActions.Add(DispatchState.Rejected, this.Reject);
        DispatchActions.Add(DispatchState.Revoked, this.Revoke);
        DispatchActions.Add(DispatchState.Departed, this.Departed);
        DispatchActions.Add(DispatchState.Arrived, this.Arrived);

        // Add block signal passage actions
        for (int i = 0; i < BlockSignalPassages.Count; i++)
        {
            var index = i; // capture for closure
            BlockSignalPassageActions.Add(index, () => this.PassBlockSignal(index));
        }
    }
}
