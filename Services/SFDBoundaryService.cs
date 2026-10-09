using System.Globalization;
using System.Text.Json;
using SFD.Models;

namespace SFD.Services;

public class SFDBoundaryService(IHttpClientFactory http)
{
    const string Endpoint = "https://www.solofleet.com/DMOprojects/getProjectBoundaryPOIPoly";

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
}
