using System.Text.Json;
using System.Text.Json.Serialization;
using Tellurian.Trains.Dispatch.Layout;
using Tellurian.Trains.Dispatch.Trains;

namespace Tellurian.Trains.Dispatch.Data.Json;

/// <summary>
/// JSON converter for <see cref="TimeSpan"/> that serializes as "HH:mm:ss" string.
/// </summary>
public class TimeSpanJsonConverter : JsonConverter<TimeSpan>
{
    public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (string.IsNullOrEmpty(value))
            return TimeSpan.Zero;

        return TimeSpan.Parse(value);
    }

    public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString(@"hh\:mm\:ss"));
    }
}

/// <summary>
/// JSON converter for <see cref="CallTime"/> that serializes arrival and departure times.
/// </summary>
public class CallTimeJsonConverter : JsonConverter<CallTime>
{
    public override CallTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected start of object for CallTime");

        var callTime = new CallTime();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                break;

            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("Expected property name");

            var propertyName = reader.GetString();
            reader.Read();

            switch (propertyName?.ToLowerInvariant())
            {
                case "arrivaltime":
                case "arrival":
                    var arrivalStr = reader.GetString();
                    if (!string.IsNullOrEmpty(arrivalStr))
                        callTime.ArrivalTime = TimeSpan.Parse(arrivalStr);
                    break;

                case "departuretime":
                case "departure":
                    var departureStr = reader.GetString();
                    if (!string.IsNullOrEmpty(departureStr))
                        callTime.DepartureTime = TimeSpan.Parse(departureStr);
                    break;
            }
        }

        return callTime;
    }

    public override void Write(Utf8JsonWriter writer, CallTime value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("arrivalTime", value.ArrivalTime.ToString(@"hh\:mm\:ss"));
        writer.WriteString("departureTime", value.DepartureTime.ToString(@"hh\:mm\:ss"));
        writer.WriteEndObject();
    }
}

/// <summary>
/// JSON converter for <see cref="OperationPlace"/> that handles polymorphism
/// (Station, SignalControlledPlace, OtherPlace).
/// </summary>
public class OperationPlaceJsonConverter : JsonConverter<OperationPlace>
{
    private const string TypePropertyName = "$type";

    public override OperationPlace Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected start of object for OperationPlace");

        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        // Check for type discriminator
        var typeName = root.TryGetProperty(TypePropertyName, out var typeElement)
            ? typeElement.GetString()
            : root.TryGetProperty("type", out var typeElement2)
                ? typeElement2.GetString()
                : null;

        // Read common properties
        var id = root.TryGetProperty("id", out var idElement) ? idElement.GetInt32() : 0;
        var name = root.TryGetProperty("name", out var nameElement) ? nameElement.GetString() ?? "" : "";
        var signature = root.TryGetProperty("signature", out var sigElement) ? sigElement.GetString() ?? "" : "";

        // Read tracks
        var tracks = new List<StationTrack>();
        if (root.TryGetProperty("tracks", out var tracksElement) && tracksElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var trackElement in tracksElement.EnumerateArray())
            {
                var trackNumber = trackElement.TryGetProperty("number", out var numElement)
                    ? numElement.GetString() ?? ""
                    : "";
                var maxLength = trackElement.TryGetProperty("maxLength", out var maxLenElement) && maxLenElement.ValueKind != JsonValueKind.Null
                    ? maxLenElement.GetInt32()
                    : (int?)null;
                tracks.Add(new StationTrack(trackNumber) { MaxLength = maxLength });
            }
        }

        return typeName?.ToLowerInvariant() switch
        {
            "station" => CreateStation(root, id, name, signature, tracks),
            "signalcontrolledplace" => CreateSignalControlledPlace(root, id, name, signature, tracks),
            "otherplace" => CreateOtherPlace(root, id, name, signature, tracks),
            _ => InferType(root, id, name, signature, tracks)
        };
    }

    private static Station CreateStation(JsonElement root, int id, string name, string signature, List<StationTrack> tracks)
    {
        var isManned = root.TryGetProperty("isManned", out var mannedElement) && mannedElement.GetBoolean();
        return new Station(name, signature)
        {
            Id = id,
            IsManned = isManned,
            Tracks = tracks
        };
    }

    private static SignalControlledPlace CreateSignalControlledPlace(JsonElement root, int id, string name, string signature, List<StationTrack> tracks)
    {
        var controlledByDispatcherId = root.TryGetProperty("controlledByDispatcherId", out var ctrlElement)
            ? ctrlElement.GetInt32()
            : 0;
        var isJunction = root.TryGetProperty("isJunction", out var juncElement) && juncElement.GetBoolean();

        return new SignalControlledPlace(name, signature, controlledByDispatcherId)
        {
            Id = id,
            IsJunction = isJunction,
            Tracks = tracks
        };
    }

    private static OtherPlace CreateOtherPlace(JsonElement root, int id, string name, string signature, List<StationTrack> tracks)
    {
        var isJunction = root.TryGetProperty("isJunction", out var juncElement) && juncElement.GetBoolean();
        return new OtherPlace(name, signature)
        {
            Id = id,
            IsJunction = isJunction,
            Tracks = tracks
        };
    }

    private static OperationPlace InferType(JsonElement root, int id, string name, string signature, List<StationTrack> tracks)
    {
        // Infer type based on properties
        if (root.TryGetProperty("controlledByDispatcherId", out _))
        {
            return CreateSignalControlledPlace(root, id, name, signature, tracks);
        }
        else if (root.TryGetProperty("isManned", out _))
        {
            return CreateStation(root, id, name, signature, tracks);
        }
        else
        {
            return CreateOtherPlace(root, id, name, signature, tracks);
        }
    }

    public override void Write(Utf8JsonWriter writer, OperationPlace value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        // Write type discriminator
        var typeName = value switch
        {
            Station => "Station",
            SignalControlledPlace => "SignalControlledPlace",
            OtherPlace => "OtherPlace",
            _ => value.GetType().Name
        };
        writer.WriteString(TypePropertyName, typeName);

        // Write common properties
        writer.WriteNumber("id", value.Id);
        writer.WriteString("name", value.Name);
        writer.WriteString("signature", value.Signature);

        // Write type-specific properties
        switch (value)
        {
            case Station station:
                writer.WriteBoolean("isManned", station.IsManned);
                break;

            case SignalControlledPlace scp:
                writer.WriteNumber("controlledByDispatcherId", scp.ControlledByDispatcherId);
                writer.WriteBoolean("isJunction", scp.IsJunction);
                break;

            case OtherPlace other:
                writer.WriteBoolean("isJunction", other.IsJunction);
                break;
        }

        // Write tracks
        writer.WritePropertyName("tracks");
        writer.WriteStartArray();
        foreach (var track in value.Tracks)
        {
            writer.WriteStartObject();
            writer.WriteString("number", track.Number);
            if (track.MaxLength.HasValue)
                writer.WriteNumber("maxLength", track.MaxLength.Value);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        writer.WriteEndObject();
    }
}
