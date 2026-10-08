using System.Globalization;
using SFD.Models;

namespace SFD.Services;

public record SFDMemberSource(string Id, string Name, string Area, string Color, string? Phone);
public record SFDPointSource(double Lat, double Lon, double Speed, double Bearing, DateTime TimeUtc);
public record SFDRouteSource(string Id, string Name, string Area, List<SFDLatLon> Coordinates, double DemoCovered = 0.7);

/// <summary>Turns raw rows into the member and route figures the dashboard displays.</summary>
public static class SFDTrackingBuilder
{
    public static readonly TimeSpan Wib = TimeSpan.FromHours(7);
    public static readonly string[] MemberColors = ["#815796", "#cf8759", "#568e86", "#6483b6", "#a8788c", "#88985c", "#baaa71", "#7c8398"];
    static readonly SFDLatLon DefaultPosition = new(-6.229, 106.819);
    const int OfflineAfterMinutes = 5;

    public static string Today() => DateTime.UtcNow.Add(Wib).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>A WIB calendar day is [00:00+07:00, next 00:00+07:00), i.e. starting 17:00 UTC the day before.</summary>
    public static bool TryDayRange(string date, out DateTime startUtc, out DateTime endUtc)
    {
        endUtc = default;
        if (!DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
        {
            startUtc = default;
            return false;
        }
        startUtc = DateTime.SpecifyKind(day - Wib, DateTimeKind.Utc);
        endUtc = startUtc.AddDays(1);
        return true;
    }

    public static string Initials(string name) =>
        string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => char.ToUpperInvariant(p[0])));

    public static string FormatTime(DateTime utc) => utc.Add(Wib).ToString("HH'.'mm", CultureInfo.InvariantCulture) + " WIB";

    public static List<SFDMemberDto> Members(IReadOnlyList<SFDMemberSource> members, ILookup<string, SFDPointSource> points, DateTime nowUtc)
    {
        var result = new List<SFDMemberDto>(members.Count);
        for (var index = 0; index < members.Count; index++)
        {
            var m = members[index];
            var gps = points[m.Id].OrderBy(p => p.TimeUtc).ToList();
            var last = gps.Count > 0 ? gps[^1] : null;
            var age = last is null ? double.PositiveInfinity : (nowUtc - last.TimeUtc).TotalMinutes;
            var trace = gps.Select(p => new SFDLatLon(p.Lat, p.Lon)).ToList();

            var hours = new double[12];
            for (var i = 1; i < gps.Count; i++)
            {
                var slot = gps[i].TimeUtc.Add(Wib).Hour - 6;
                if (slot is >= 0 and < 12) hours[slot] += SFDGeo.Haversine(trace[i - 1], trace[i]);
            }

            var speed = last?.Speed ?? 0;
            result.Add(new SFDMemberDto(
                m.Id, m.Name, Initials(m.Name), m.Area,
                string.IsNullOrEmpty(m.Color) ? MemberColors[index % MemberColors.Length] : m.Color,
                m.Phone,
                last is null ? DefaultPosition : new SFDLatLon(last.Lat, last.Lon),
                Math.Round(speed, 1), Math.Round(last?.Bearing ?? 0), SFDGeo.RouteLength(trace),
                age > OfflineAfterMinutes ? "offline" : speed > 1 ? "bergerak" : "berhenti",
                null,
                last is null ? "Belum ada GPS" : FormatTime(last.TimeUtc),
                trace, hours));
        }
        return result;
    }

    public static List<SFDRouteDto> Routes(IReadOnlyList<SFDRouteSource> routes, IReadOnlyList<SFDMemberDto> members, bool demo)
    {
        var traces = members
            .SelectMany(m => m.Trace.Skip(1).Select((p, i) => (From: m.Trace[i], To: p)))
            .ToList();

        return routes.Select(r =>
        {
            var segments = SFDGeo.RouteSegments(r.Coordinates, traces, demo, r.DemoCovered);
            var total = segments.Sum(s => s.Distance);
            var done = segments.Where(s => s.Covered).Sum(s => s.Distance);
            return new SFDRouteDto(
                r.Id, r.Name, r.Area, r.Coordinates, total, done, total - done,
                total > 0 ? done / total * 100 : 0,
                segments.Where(s => !s.Covered).Select(s => new[] { s.A, s.B }).ToList());
        }).ToList();
    }
}
