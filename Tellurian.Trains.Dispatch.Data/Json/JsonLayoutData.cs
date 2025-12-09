using System.Text.Json.Serialization;
using Tellurian.Trains.Dispatch.Trains;

namespace Tellurian.Trains.Dispatch.Data.Json;

/// <summary>
/// Root container for layout data serialized to/from JSON.
/// Layout data is static configuration that doesn't change during operation.
/// </summary>
public class JsonLayoutData
{
    public List<JsonOperationPlace> OperationPlaces { get; set; } = [];
    public List<JsonTrackStretch> TrackStretches { get; set; } = [];
    public List<JsonDispatchStretch> DispatchStretches { get; set; } = [];
}

/// <summary>
/// JSON representation of an operation place (Station, SignalControlledPlace, or OtherPlace).
/// The <see cref="Type"/> property determines which domain class to create.
/// </summary>
public class JsonOperationPlace
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Signature { get; set; }

    /// <summary>
    /// Type of operation place: "Station", "SignalControlledPlace", or "OtherPlace"
    /// </summary>
    public required string Type { get; set; }

    /// <summary>
    /// Only for Station: whether the station is manned.
    /// </summary>
    public bool IsManned { get; set; }

    /// <summary>
    /// Only for SignalControlledPlace: ID of the dispatcher that controls this place.
    /// </summary>
    public int? ControlledByDispatcherId { get; set; }

    /// <summary>
    /// Only for SignalControlledPlace or OtherPlace: whether this is a junction.
    /// </summary>
    public bool IsJunction { get; set; }

    /// <summary>
    /// Tracks available at this place.
    /// </summary>
    public List<JsonStationTrack> Tracks { get; set; } = [];
}

/// <summary>
/// JSON representation of a station track.
/// </summary>
public class JsonStationTrack
{
    public required string Number { get; set; }

    /// <summary>
    /// Maximum train length in meters. Null means no constraint.
    /// </summary>
    public int? MaxLength { get; set; }

    public StationTrack ToDomain() => new(Number) { MaxLength = MaxLength };
}

/// <summary>
/// JSON representation of a track stretch between two operation places.
/// </summary>
public class JsonTrackStretch
{
    public int Id { get; set; }

    /// <summary>
    /// ID of the starting operation place.
    /// </summary>
    public int StartId { get; set; }

    /// <summary>
    /// ID of the ending operation place.
    /// </summary>
    public int EndId { get; set; }

    /// <summary>
    /// Number of physical tracks (1 = single track, 2 = double track).
    /// </summary>
    public int NumberOfTracks { get; set; } = 1;
}

/// <summary>
/// JSON representation of a dispatch stretch between two stations.
/// </summary>
public class JsonDispatchStretch
{
    public int Id { get; set; }

    /// <summary>
    /// ID of the from station.
    /// </summary>
    public int FromStationId { get; set; }

    /// <summary>
    /// ID of the to station.
    /// </summary>
    public int ToStationId { get; set; }
}
