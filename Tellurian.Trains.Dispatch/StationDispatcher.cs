using Tellurian.Trains.Dispatch.Brokers;
using Tellurian.Trains.Dispatch.Trains;

namespace Tellurian.Trains.Dispatch;

public record StationDispatcher(Station Station, IBroker Broker) : IDispatcher
{
    public string Name => Station.Name;
    public IEnumerable<TrainSection> Arrivals => Broker.GetArrivalsFor(Station, 10);
    public IEnumerable<TrainSection> Departures => Broker.GetDeparturesFor(Station, 10);
}
