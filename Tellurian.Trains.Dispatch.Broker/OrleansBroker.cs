using Tellurian.Trains.Dispatch.Configurations;
using Tellurian.Trains.Dispatch.Trains;


namespace Tellurian.Trains.Dispatch.Broker;

public class OrleansBroker(IDispatcherConfiguration configuration) : IBroker, IGrainWithStringKey
{
    override 
    private readonly Dictionary<string, Station> _stations = [];
    private readonly Dictionary<string, DispatchStretch> _dispatchStretches = [];

    public IEnumerable<Station> Stations => _stations.Values;
    public IEnumerable<DispatchStretch> DispatchStretches => _dispatchStretches.Values;

    public Task CloseAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();

       public Task InitAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task NotifyAsync(Message message, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
    public Task<Message> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
