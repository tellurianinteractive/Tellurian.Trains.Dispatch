using Tellurian.Trains.Dispatch.Brokers;
using Tellurian.Trains.Dispatch.Layout;
using Tellurian.Trains.Dispatch.Trains;

namespace Tellurian.Trains.Dispatch.Data;
/// <summary>
/// Reads data from the timetable database in Microsoft Access .accdb format.
/// </summary>
/// <param name="layoutId">The layout id to read.</param>
/// <param name="pathToAccessDatabaseFile"></param>
/// <remarks>
/// This <see cref="IBrokerDataProvider"/> uses ODBC with raw SQL statements for retriving data.
/// </remarks>
public class AccessBrokerConfiguration(int layoutId, string pathToAccessDatabaseFile) : IBrokerDataProvider
{
    private readonly int _layoutId = layoutId;
    private readonly string _pathToAccessDatabaseFile = pathToAccessDatabaseFile;



    public Task<IEnumerable<OperationPlace>> GetOperationPlacesAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<IEnumerable<TrackStretch>> GetTrackStretchesAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<IEnumerable<DispatchStretch>> GetDispatchStretchesAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<IEnumerable<Train>> GetTrainsAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<IEnumerable<TrainStationCall>> GetTrainStationCallsAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}

