namespace Tellurian.Trains.Dispatch;

public interface IBrokerStateProvider
{ 
    Task<bool> SaveDispatchCallsAsync(IEnumerable<TrainStretch> dispatchCalls, CancellationToken cancellationToken = default);
    Task<IEnumerable<TrainStretch>> ReadDispatchCallsAsync(CancellationToken cancellationToken = default);
}

