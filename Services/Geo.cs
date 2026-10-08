using SFD.Models;

namespace SFD.Services;

/// <summary>Distance and route-coverage maths. A segment counts as covered when its midpoint lies within 50 m of a GPS trace.</summary>
public static class Geo
{
    public const double CoverageRadiusKm = 0.05;
    const double SegmentLengthKm = 0.075;

    public static double Haversine(LatLon a, LatLon b)
    {
        const double rad = Math.PI / 180;
        var lat = (b.Lat - a.Lat) * rad;
        var lon = (b.Lon - a.Lon) * rad;
        var h = Math.Pow(Math.Sin(lat / 2), 2) + Math.Cos(a.Lat * rad) * Math.Cos(b.Lat * rad) * Math.Pow(Math.Sin(lon / 2), 2);
        return 6371 * 2 * Math.Atan2(Math.Sqrt(h), Math.Sqrt(1 - h));
    }

    public static double RouteLength(IReadOnlyList<LatLon> points)
    {
        double sum = 0;
        for (var i = 1; i < points.Count; i++) sum += Haversine(points[i - 1], points[i]);
        return sum;
    }

    static double DistanceToSegment(LatLon point, LatLon a, LatLon b)
    {
        var scale = Math.Cos(point.Lat * Math.PI / 180);
        var x = (point.Lon - a.Lon) * scale; var y = point.Lat - a.Lat;
        var dx = (b.Lon - a.Lon) * scale; var dy = b.Lat - a.Lat;
        var denominator = dx * dx + dy * dy;
        var t = denominator > 0 ? Math.Max(0, Math.Min(1, (x * dx + y * dy) / denominator)) : 0;
        return Math.Sqrt(Math.Pow(x - t * dx, 2) + Math.Pow(y - t * dy, 2)) * 111.32;
    }

    public readonly record struct Segment(LatLon A, LatLon B, bool Covered, double Distance);

    /// <param name="demoCovered">When set, coverage is illustrative: the first fraction of the route counts as covered.</param>
    public static List<Segment> RouteSegments(IReadOnlyList<LatLon> route, IReadOnlyList<(LatLon From, LatLon To)> traces, bool demo, double demoCovered)
    {
        var result = new List<Segment>();
        for (var index = 1; index < route.Count; index++)
        {
            var start = route[index - 1]; var end = route[index];
            var count = Math.Max(1, (int)Math.Ceiling(Haversine(start, end) / SegmentLengthKm));
            for (var step = 0; step < count; step++)
            {
                var a = Lerp(start, end, (double)step / count);
                var b = Lerp(start, end, (double)(step + 1) / count);
                var mid = new LatLon((a.Lat + b.Lat) / 2, (a.Lon + b.Lon) / 2);
                var covered = demo
                    ? (index - 1 + (double)step / count) / (route.Count - 1) < demoCovered
                    : traces.Any(t => DistanceToSegment(mid, t.From, t.To) <= CoverageRadiusKm);
                result.Add(new Segment(a, b, covered, Haversine(a, b)));
            }
        }
        return result;
    }

    static LatLon Lerp(LatLon a, LatLon b, double t) => new(a.Lat + (b.Lat - a.Lat) * t, a.Lon + (b.Lon - a.Lon) * t);
}
