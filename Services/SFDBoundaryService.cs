using System.Globalization;
using System.Text.Json;
using SFD.Models;

namespace SFD.Services;

public class SFDBoundaryService(IHttpClientFactory http)
{
    const string Endpoint = "https://www.solofleet.com/DMOprojects/getProjectBoundaryPOIPoly";
    const string PointsEndpoint = "https://www.solofleet.com/DMOprojects/getlatlongpoibyboundaryArray";

    /// <summary>Boundary polygons intersecting the given map bounds. Upstream requires POST with the bounds in the query string.</summary>
    public async Task<List<SFDBoundaryDto>> GetAsync(double latBottom, double latTop, double lonLeft, double lonRight, CancellationToken ct)
    {
        var url = string.Create(CultureInfo.InvariantCulture, $"{Endpoint}?latbottom={latBottom}&lattop={latTop}&longleft={lonLeft}&longright={lonRight}");
        using var response = await http.CreateClient().PostAsync(url, null, ct);
        response.EnsureSuccessStatusCode();
        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);

        var result = new List<SFDBoundaryDto>();
        if (doc.RootElement.ValueKind != JsonValueKind.Array) return result;

        foreach (var item in doc.RootElement.EnumerateArray())
        {
            if (!item.TryGetProperty("geomarray", out var geom) || geom.ValueKind != JsonValueKind.Array) continue;
            // Upstream names are X = longitude, Y = latitude.
            var points = geom.EnumerateArray()
                .Select(p => new SFDLatLon(p.GetProperty("Y").GetDouble(), p.GetProperty("X").GetDouble()))
                .ToList();
            if (points.Count < 3) continue;
            var id = item.TryGetProperty("ogr_fid", out var f) && f.TryGetInt32(out var n) ? n : 0;
            var name = item.TryGetProperty("actualname", out var a) && a.ValueKind == JsonValueKind.String ? a.GetString() ?? "" : "";
            result.Add(new SFDBoundaryDto(id, name, points));
        }
        return result;
    }

    /// <summary>POI points inside the given map bounds. Upstream answers with a JSON string that itself holds an array of [lat, lon] pairs.</summary>
    public async Task<List<SFDLatLon>> GetPointsAsync(double latBottom, double latTop, double lonLeft, double lonRight, CancellationToken ct)
    {
        var url = string.Create(CultureInfo.InvariantCulture, $"{PointsEndpoint}?latbottom={latBottom}&lattop={latTop}&longleft={lonLeft}&longright={lonRight}");
        using var response = await http.CreateClient().PostAsync(url, null, ct);
        response.EnsureSuccessStatusCode();
        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);

        var root = doc.RootElement;
        if (root.ValueKind == JsonValueKind.String)
        {
            using var inner = JsonDocument.Parse(root.GetString() ?? "[]");
            return ReadPoints(inner.RootElement);
        }
        return ReadPoints(root);
    }

    static List<SFDLatLon> ReadPoints(JsonElement array)
    {
        var points = new List<SFDLatLon>();
        if (array.ValueKind != JsonValueKind.Array) return points;
        foreach (var p in array.EnumerateArray())
        {
            if (p.ValueKind != JsonValueKind.Array || p.GetArrayLength() < 2) continue;
            var lat = p[0].GetDouble();
            var lon = p[1].GetDouble();
            if (double.IsFinite(lat) && double.IsFinite(lon) && Math.Abs(lat) <= 90 && Math.Abs(lon) <= 180) points.Add(new SFDLatLon(lat, lon));
        }
        return points;
    }
}
