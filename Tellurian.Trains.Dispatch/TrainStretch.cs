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
    public IEnumerable<Func<bool>> ArrivalActions => DispatchActions
        .Where(k => k.Key.IsIn(State.NextArrivalStates))
        .Select(k => k.Value);
    public IEnumerable<Func<bool>> DepartureActions => DispatchActions
        .Where(k => k.Key.IsIn(State.NextDepartureStates))
        .Select(k => k.Value);

    private TrainStretch(DispatchStretch dispatchStretch, TrainStationCall departure, TrainStationCall arrival, ITimeProvider timeProvider)
    {
        TimeProvider = timeProvider;
        DispatchStretch = dispatchStretch;
        Departure = departure;
        Arrival = arrival;
        State = DispatchState.None;
        CreateActionDictionaries();
    }
    /// <summary>
    /// Creates a <see cref="TrainStretch"/>
    /// </summary>
    /// <param name="dispatchStretch">The tracksection </param>
    /// <param name="departure"></param>
    /// <param name="arrival"></param>
    /// <returns></returns>
    public static Option<TrainStretch> Create(DispatchStretch dispatchStretch, TrainStationCall departure, TrainStationCall arrival,  ITimeProvider timeProvider)
    {
        if (dispatchStretch.CanHave(departure, arrival))
        {
            var trainStretch = new TrainStretch(dispatchStretch, departure, arrival, timeProvider);
            trainStretch.BlockSignalPassages = [.. trainStretch.CreateBlockSignalPassages()];
            return Option<TrainStretch>.Success(trainStretch);
        }
        else if (dispatchStretch.Reverse.CanHave(departure, arrival))
        {
            var trainStretch = new TrainStretch(dispatchStretch.Reverse, departure, arrival, timeProvider);
            trainStretch.BlockSignalPassages = [.. trainStretch.CreateBlockSignalPassages()];
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
    }
}
