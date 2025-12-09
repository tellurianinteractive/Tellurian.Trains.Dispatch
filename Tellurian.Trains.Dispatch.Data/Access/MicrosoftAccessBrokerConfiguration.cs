using System.Data;
using System.Data.Odbc;
using System.Diagnostics;
using Tellurian.Trains.Dispatch.Brokers;
using Tellurian.Trains.Dispatch.Layout;
using Tellurian.Trains.Dispatch.Trains;

namespace Tellurian.Trains.Dispatch.Data.Access;
/// <summary>
/// Reads data from the timetable database in Microsoft Access .accdb format.
/// </summary>
/// <param name="layoutId">The layout id to read.</param>
/// <param name="sessionId">The session id to read, can also means operation weekday id.</param>
/// <param name="pathToAccessDatabaseFile"></param>
/// <remarks>
/// This <see cref="IBrokerDataProvider"/> uses ODBC with raw SQL statements for retriving data.
/// </remarks>
public class MicrosoftAccessBrokerConfiguration(int layoutId, int sessionId, string pathToAccessDatabaseFile) : IBrokerDataProvider
{
    private readonly int _layoutId = layoutId;
    private readonly int _sessionId = sessionId;
    private readonly string _pathToAccessDatabaseFile = pathToAccessDatabaseFile;

    private readonly Dictionary<int, OperationPlace> _operationPlaces = [];
    private readonly IEnumerable<TrackStretch> _trackStretches = [];
    private readonly Dictionary<int, Train> _trains = [];

    private string ConnectionString => $"Driver={{Microsoft Access Driver (*.mdb, *.accdb)}};Dbq={_pathToAccessDatabaseFile};";

    public Task<IEnumerable<OperationPlace>> GetOperationPlacesAsync(CancellationToken cancellationToken = default)
    {
        var placeSql = $"SELECT * FROM OperationPlaces WHERE LayoutId={_layoutId}";
        var tracksSql = $"SELECT * FROM StationTracks WHERE LayoutId={_layoutId}";
        var _operationalPlaces =
            ReadEntities(placeSql, reader => reader.ToOperationalPlaceData()).
            Select(op => op.Map())
            .ToDictionary(p => p.Id);

        var stationTracks = ReadEntities(tracksSql, reader => reader.ToStationTrackData()).ToList();
        foreach (var track in stationTracks)
        {
            if (_operationalPlaces.TryGetValue(track.OperationPlaceId, out var operationPlace))
            {
                operationPlace.Tracks.Add(track.Map());
            }
        }
        return Task.FromResult(_operationalPlaces.Values.AsEnumerable());
    }

    public Task<IEnumerable<TrackStretch>> GetTrackStretchesAsync(CancellationToken cancellationToken = default)
    {
        var stretchSql = $"SELECT * FROM TrackStretches WHERE LayoutId={_layoutId}";
        var trackStretches =
            ReadEntities(stretchSql, reader => reader.ToTrackStretchData()).
            Select(ts => ts.Map(_operationPlaces));
        return Task.FromResult(_trackStretches);
    }

    public Task<IEnumerable<DispatchStretch>> GetDispatchStretchesAsync(CancellationToken cancellationToken = default)
    {
        var dispatchStretchSql = $"SELECT * FROM DispatchStretches WHERE LayoutId={_layoutId}";
        var dispatchStretches =
            ReadEntities(dispatchStretchSql, reader => reader.ToDispatchStretchData()).
            Select(ds => ds.Map(_operationPlaces, _trackStretches));
        return Task.FromResult(dispatchStretches);
    }

    public Task<IEnumerable<Train>> GetTrainsAsync(CancellationToken cancellationToken = default)
    {
        var trainSql = $"SELECT * FROM Trains WHERE LayoutId={_layoutId} AND SessionId={_sessionId}";
        var _trains =
            ReadEntities(trainSql, reader => reader.ToTrainData())
            .Select(t => t.Map())
            .ToDictionary(t => t.Id);
        return Task.FromResult(_trains.Values.AsEnumerable());
    }

    public Task<IEnumerable<TrainStationCall>> GetTrainStationCallsAsync(CancellationToken cancellationToken = default)
    {
        var callsSql = $"SELECT * FROM TrainStationCalls WHERE LayoutId={_layoutId} AND SessionId={_sessionId}";
        var calls =
            ReadEntities(callsSql, reader => reader.ToTrainStationCallData())
            .Select(c => c.Map(_operationPlaces, _trains));
        return Task.FromResult(calls);
    }

    private IEnumerable<T> ReadEntities<T>(string sql, Func<IDataRecord, T> readEntity)
    {
        using var connection = new OdbcConnection(ConnectionString);
        using var reader = ExecuteReader(connection, sql);
        while (reader.Read())
        {
            yield return readEntity(reader);
        }
    }

    private static OdbcDataReader ExecuteReader(OdbcConnection connection, string sql)
    {
        var command = new OdbcCommand(sql, connection);
        try
        {
            connection.Open();
            return command.ExecuteReader(CommandBehavior.CloseConnection);

        }
        catch (Exception ex)
        {
            _ = ex.Message;
            Debugger.Break();
            throw;
        }
    }
}

