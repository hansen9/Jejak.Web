using System.Text.Json;
using System.Text.Json.Serialization;

namespace SFD.Models;

/// <summary>A [latitude, longitude] pair. Serialises as a two-element JSON array.</summary>
[JsonConverter(typeof(SFDLatLonConverter))]
public readonly record struct SFDLatLon(double Lat, double Lon);

public sealed class SFDLatLonConverter : JsonConverter<SFDLatLon>
{
    public override SFDLatLon Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray) throw new JsonException("Expected [lat, lon].");
        reader.Read(); var lat = reader.GetDouble();
        reader.Read(); var lon = reader.GetDouble();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray) { }
        return new SFDLatLon(lat, lon);
    }

    public override void Write(Utf8JsonWriter writer, SFDLatLon value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(value.Lat);
        writer.WriteNumberValue(value.Lon);
        writer.WriteEndArray();
    }
}

public record SFDMemberDto(
    string Id, string Name, string Initials, string Area, string Color, string? Phone,
    SFDLatLon Position, double Speed, double Bearing, double Distance, string Status,
    int? Battery, string LastSeen, List<SFDLatLon> Trace, double[] Hours);

public record SFDRouteDto(
    string Id, string Name, string Area, List<SFDLatLon> Coordinates,
    double Total, double Done, double Remaining, double Percent,
    List<SFDLatLon[]> Uncovered);

public record SFDTrackingDto(bool Demo, string Date, string UpdatedAt, List<SFDMemberDto> Members, List<SFDRouteDto> Routes);

public record SFDLoginRequest(string? Username, string? Password);
public record SFDMemberRequest(string? Name, string? Area, string? Phone, string? Color);
public record SFDGpsPointRequest(Guid MemberId, double Latitude, double Longitude, double Speed, double Bearing, double Accuracy, DateTime RecordedAt);
