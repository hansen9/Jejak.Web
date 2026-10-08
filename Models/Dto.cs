using System.Text.Json;
using System.Text.Json.Serialization;

namespace SFD.Models;

/// <summary>A [latitude, longitude] pair. Serialises as a two-element JSON array.</summary>
[JsonConverter(typeof(LatLonConverter))]
public readonly record struct LatLon(double Lat, double Lon);

public sealed class LatLonConverter : JsonConverter<LatLon>
{
    public override LatLon Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray) throw new JsonException("Expected [lat, lon].");
        reader.Read(); var lat = reader.GetDouble();
        reader.Read(); var lon = reader.GetDouble();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray) { }
        return new LatLon(lat, lon);
    }

    public override void Write(Utf8JsonWriter writer, LatLon value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(value.Lat);
        writer.WriteNumberValue(value.Lon);
        writer.WriteEndArray();
    }
}

public record MemberDto(
    string Id, string Name, string Initials, string Area, string Color, string? Phone,
    LatLon Position, double Speed, double Bearing, double Distance, string Status,
    int? Battery, string LastSeen, List<LatLon> Trace, double[] Hours);

public record RouteDto(
    string Id, string Name, string Area, List<LatLon> Coordinates,
    double Total, double Done, double Remaining, double Percent,
    List<LatLon[]> Uncovered);

public record TrackingDto(bool Demo, string Date, string UpdatedAt, List<MemberDto> Members, List<RouteDto> Routes);

public record LoginRequest(string? Username, string? Password);
public record MemberRequest(string? Name, string? Area, string? Phone, string? Color);
public record GpsPointRequest(Guid MemberId, double Latitude, double Longitude, double Speed, double Bearing, double Accuracy, DateTime RecordedAt);
