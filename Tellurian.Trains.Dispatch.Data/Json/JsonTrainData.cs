using Tellurian.Trains.Dispatch.Trains;

namespace Tellurian.Trains.Dispatch.Data.Json;

/// <summary>
/// Root container for train data serialized to/from JSON.
/// Train data includes trains and their scheduled station calls.
/// </summary>
public class JsonTrainData
{
    public List<JsonCompany> Companies { get; set; } = [];
    public List<JsonTrain> Trains { get; set; } = [];
    public List<JsonTrainStationCall> TrainStationCalls { get; set; } = [];
}

/// <summary>
/// JSON representation of a train company.
/// </summary>
public class JsonCompany
{
    public required string Name { get; set; }
    public required string Signature { get; set; }

    public Company ToDomain() => new(Name, Signature);
}

/// <summary>
/// JSON representation of a train.
/// </summary>
public class JsonTrain
{
    public int Id { get; set; }

    /// <summary>
    /// Signature of the company operating this train.
    /// </summary>
    public required string CompanySignature { get; set; }

    /// <summary>
    /// Train identity prefix (e.g., "P" for passenger, "G" for freight).
    /// </summary>
    public required string IdentityPrefix { get; set; }

    /// <summary>
    /// Train number.
    /// </summary>
    public int IdentityNumber { get; set; }

    /// <summary>
    /// Maximum train length in meters. Null means no constraint.
    /// </summary>
    public int? MaxLength { get; set; }
}

/// <summary>
/// JSON representation of a train's scheduled call at a station.
/// </summary>
public class JsonTrainStationCall
{
    public int Id { get; set; }

    /// <summary>
    /// ID of the train making this call.
    /// </summary>
    public int TrainId { get; set; }

    /// <summary>
    /// ID of the operation place (station) for this call.
    /// </summary>
    public int OperationPlaceId { get; set; }

    /// <summary>
    /// Scheduled arrival time as "HH:mm" or "HH:mm:ss".
    /// </summary>
    public required string ArrivalTime { get; set; }

    /// <summary>
    /// Scheduled departure time as "HH:mm" or "HH:mm:ss".
    /// </summary>
    public required string DepartureTime { get; set; }

    /// <summary>
    /// Track number at this station.
    /// </summary>
    public required string TrackNumber { get; set; }

    /// <summary>
    /// Whether the train arrives at this call (false for origin station).
    /// </summary>
    public bool IsArrival { get; set; } = true;

    /// <summary>
    /// Whether the train departs from this call (false for destination station).
    /// </summary>
    public bool IsDeparture { get; set; } = true;

    /// <summary>
    /// Sequence number for ordering calls within a train's journey.
    /// </summary>
    public int SequenceNumber { get; set; }

    public CallTime GetCallTime() => new()
    {
        ArrivalTime = TimeSpan.Parse(ArrivalTime),
        DepartureTime = TimeSpan.Parse(DepartureTime)
    };
}
