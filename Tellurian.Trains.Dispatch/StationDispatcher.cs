using Tellurian.Trains.Dispatch.Brokers;
using Tellurian.Trains.Dispatch.Trains;

namespace Tellurian.Trains.Dispatch;

public record StationDispatcher(Station Station, IBroker Broker) : IDispatcher
{
    public int Id => Station.Id;
    public string Name => Station.Name;
    public string Signature => Station.Signature;
    public IEnumerable<ActionContext> ArrivalActions => Broker.GetArrivalActionsFor(this, 10);
    public IEnumerable<ActionContext> DepartureActions => Broker.GetDepartureActionsFor(this, 10);

    public IEnumerable<TrainSection> GetArrivals(int maxItems = 10) => throw new NotImplementedException();
    public IEnumerable<TrainSection> GetDepartures(int maxItems = 10) => throw new NotImplementedException();
}
