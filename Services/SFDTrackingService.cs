using System.Text.Json;
using SFD.Data;
using SFD.Models;
using Microsoft.EntityFrameworkCore;

namespace SFD.Services;

public class SFDTrackingService(SFDAppDbContext db)
{
    /// <summary>Everything the dashboard shows for one owner on one WIB calendar day. Null when <paramref name="date"/> is not yyyy-MM-dd.</summary>
    public async Task<SFDTrackingDto?> BuildAsync(Guid ownerId, string date, CancellationToken ct)
    {
        if (!SFDTrackingBuilder.TryDayRange(date, out var start, out var end)) return null;
        var now = DateTime.UtcNow;

        // Guid.ToString() inside a query is translated to SQL and comes back upper-case; convert after materialising.
        var memberRows = await db.Members.AsNoTracking().Where(m => m.OwnerId == ownerId).OrderBy(m => m.Name).ToListAsync(ct);
        var members = memberRows.Select(m => new SFDMemberSource(m.Id.ToString(), m.Name, m.Area, m.Color, m.Phone)).ToList();

        var points = await db.GpsPoints.AsNoTracking()
            .Where(p => p.OwnerId == ownerId && p.RecordedAtUtc >= start && p.RecordedAtUtc < end)
            .OrderBy(p => p.RecordedAtUtc)
            .Select(p => new { p.MemberId, p.Latitude, p.Longitude, p.Speed, p.Bearing, p.RecordedAtUtc }).ToListAsync(ct);

        var routeRows = await db.Routes.AsNoTracking().Where(r => r.OwnerId == ownerId).OrderBy(r => r.Name).ToListAsync(ct);

        var lookup = points.ToLookup(p => p.MemberId.ToString(),
            p => new SFDPointSource(p.Latitude, p.Longitude, p.Speed, p.Bearing, DateTime.SpecifyKind(p.RecordedAtUtc, DateTimeKind.Utc)));
        var memberDtos = SFDTrackingBuilder.Members(members, lookup, now);

        var routes = routeRows.Select(r => new SFDRouteSource(r.Id.ToString(), r.Name, r.Area,
            JsonSerializer.Deserialize<List<SFDLatLon>>(r.CoordinatesJson) ?? [])).ToList();

        return new SFDTrackingDto(false, date, SFDTrackingBuilder.FormatTime(now), memberDtos, SFDTrackingBuilder.Routes(routes, memberDtos, demo: false));
    }
}
