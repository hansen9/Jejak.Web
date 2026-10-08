using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using SFD.Models;

namespace SFD.Services;

public class SFDImportException(string message) : Exception(message);

public record SFDImportedRoute(string Name, string Area, List<SFDLatLon> Coordinates);
public record SFDImportedPoint(double Latitude, double Longitude, double Speed, double Bearing, DateTime RecordedAtUtc);

public static partial class SFDImportParsers
{
    public const int MaxFileBytes = 5 * 1024 * 1024;
    const int MaxRoutes = 100, MaxPointsPerRoute = 10_000, MaxGpsRows = 5_000;

    [GeneratedRegex(@"(Z|[+-]\d{2}:\d{2})$")]
    private static partial Regex ZoneSuffix();

    /// <summary>GeoJSON Feature/FeatureCollection/geometry containing LineString or MultiLineString. Coordinates are [lon, lat].</summary>
    public static List<SFDImportedRoute> ParseGeoJson(string text)
    {
        JsonDocument doc;
        try { doc = JsonDocument.Parse(text); }
        catch (JsonException) { throw new SFDImportException("Berkas bukan JSON yang valid."); }

        using (doc)
        {
            var root = doc.RootElement;
            var type = Str(root, "type");
            List<JsonElement> features;
            if (type == "FeatureCollection")
            {
                if (!root.TryGetProperty("features", out var arr) || arr.ValueKind != JsonValueKind.Array) throw new SFDImportException("Struktur GeoJSON tidak valid.");
                features = arr.EnumerateArray().ToList();
            }
            else features = [root];

            var result = new List<SFDImportedRoute>();
            for (var index = 0; index < features.Count; index++)
            {
                var feature = features[index];
                if (feature.ValueKind != JsonValueKind.Object) continue;
                var geometry = Str(feature, "type") is "Feature" or null && feature.TryGetProperty("geometry", out var g) ? g : feature;
                if (geometry.ValueKind != JsonValueKind.Object) continue;
                var gType = Str(geometry, "type");
                if (gType is not ("LineString" or "MultiLineString")) continue;
                if (!geometry.TryGetProperty("coordinates", out var coords) || coords.ValueKind != JsonValueKind.Array) throw new SFDImportException("Koordinat rute tidak valid.");

                var lines = gType == "LineString" ? [coords] : coords.EnumerateArray().ToList();
                var props = feature.ValueKind == JsonValueKind.Object && feature.TryGetProperty("properties", out var p) && p.ValueKind == JsonValueKind.Object ? p : default;
                var baseName = Clip(props.ValueKind == JsonValueKind.Object ? Str(props, "name") : null, 100) is { Length: > 0 } n ? n : $"Rute {index + 1}";
                var area = Clip(props.ValueKind == JsonValueKind.Object ? Str(props, "area") : null, 100) ?? "";

                for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
                {
                    var line = lines[lineIndex];
                    var points = new List<SFDLatLon>();
                    if (line.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var pt in line.EnumerateArray())
                        {
                            if (!TryCoordinate(pt, out var ll)) throw LineError();
                            points.Add(ll);
                            if (points.Count > MaxPointsPerRoute) throw LineError();
                        }
                    }
                    if (points.Count < 2) throw LineError();
                    var name = baseName + (lines.Count > 1 ? $" ({lineIndex + 1})" : "");
                    result.Add(new SFDImportedRoute(Clip(name, 100)!, area, points));
                }
            }
            if (result.Count == 0) throw new SFDImportException("Tidak ada LineString atau MultiLineString di berkas ini.");
            if (result.Count > MaxRoutes) throw new SFDImportException("Maksimal 100 rute dalam satu impor.");
            return result;
        }
    }

    static SFDImportException LineError() => new("Setiap rute membutuhkan 2–10.000 koordinat [longitude, latitude] yang valid.");

    static bool TryCoordinate(JsonElement pt, out SFDLatLon ll)
    {
        ll = default;
        if (pt.ValueKind != JsonValueKind.Array || pt.GetArrayLength() < 2) return false;
        var e = pt.EnumerateArray();
        e.MoveNext(); var first = e.Current;
        e.MoveNext(); var second = e.Current;
        if (first.ValueKind != JsonValueKind.Number || second.ValueKind != JsonValueKind.Number) return false;
        double lon = first.GetDouble(), lat = second.GetDouble();
        if (!double.IsFinite(lon) || !double.IsFinite(lat) || Math.Abs(lon) > 180 || Math.Abs(lat) > 90) return false;
        ll = new SFDLatLon(lat, lon);
        return true;
    }

    static string? Str(JsonElement el, string prop) =>
        el.ValueKind == JsonValueKind.Object && el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    static string? Clip(string? s, int max) => s is null ? null : s.Length <= max ? s : s[..max];

    /// <summary>CSV with latitude, longitude, recorded_at (ISO with zone); speed and bearing optional.</summary>
    public static List<SFDImportedPoint> ParseGpsCsv(string text)
    {
        var lines = text.Trim().Split(["\r\n", "\n"], StringSplitOptions.None);
        var delimiter = lines[0].Contains(';') ? ';' : ',';
        var headers = lines[0].TrimStart('﻿').Split(delimiter).Select(h => h.Trim().ToLowerInvariant()).ToList();
        if (!new[] { "latitude", "longitude", "recorded_at" }.All(headers.Contains))
            throw new SFDImportException("CSV wajib memiliki kolom latitude, longitude, recorded_at.");

        var rows = new List<SFDImportedPoint>();
        for (var i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            if (rows.Count >= MaxGpsRows) throw new SFDImportException("CSV harus memiliki 1–5.000 titik GPS.");
            var cells = lines[i].Split(delimiter).Select(c => c.Trim().Trim('"')).ToList();
            string Get(string key) { var idx = headers.IndexOf(key); return idx >= 0 && idx < cells.Count ? cells[idx] : ""; }

            var ok = double.TryParse(Get("latitude"), NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)
                & double.TryParse(Get("longitude"), NumberStyles.Float, CultureInfo.InvariantCulture, out var lon);
            double speed = 0, bearing = 0;
            if (Get("speed") is { Length: > 0 } s) ok &= double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out speed);
            if (Get("bearing") is { Length: > 0 } b) ok &= double.TryParse(b, NumberStyles.Float, CultureInfo.InvariantCulture, out bearing);
            var time = Get("recorded_at");
            var parsed = DateTimeOffset.TryParse(time, CultureInfo.InvariantCulture, DateTimeStyles.None, out var at);

            if (!ok || !double.IsFinite(lat) || !double.IsFinite(lon) || Math.Abs(lat) > 90 || Math.Abs(lon) > 180
                || !double.IsFinite(speed) || speed < 0 || !double.IsFinite(bearing) || bearing < 0 || bearing > 360
                || !parsed || !ZoneSuffix().IsMatch(time))
                throw new SFDImportException($"Baris {i + 1} tidak valid. Gunakan waktu ISO dengan zona waktu, misalnya 2026-10-07T09:00:00+07:00.");

            rows.Add(new SFDImportedPoint(lat, lon, speed, bearing, at.UtcDateTime));
        }
        if (rows.Count == 0) throw new SFDImportException("CSV harus memiliki 1–5.000 titik GPS.");
        return rows;
    }
}
