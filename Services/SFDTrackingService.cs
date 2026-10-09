using System.Text.Json;
using SFD.Models;
using SFD.Repositories;

namespace SFD.Services;

public class SFDTrackingService(ISFDMemberRepository memberRepo, ISFDGpsPointRepository pointRepo, ISFDRouteRepository routeRepo)
{
    /// <summary>Everything the dashboard shows for one owner on one WIB calendar day. Null when <paramref name="date"/> is not yyyy-MM-dd.</summary>
    public async Task<SFDTrackingDto?> BuildAsync(string ownerId, string date)
    {
        if (!SFDTrackingBuilder.TryDayRange(date, out var start, out var end)) return null;
        var now = DateTime.UtcNow;

        var memberRows = await memberRepo.GetByOwnerAsync(ownerId);
        var members = memberRows.Select(m => new SFDMemberSource(m.Id.ToString(), m.Name, m.Area, m.Color, m.Phone)).ToList();

        var points = await pointRepo.GetByOwnerAsync(ownerId, start, end);
        var routeRows = await routeRepo.GetByOwnerAsync(ownerId);

        var lookup = points.ToLookup(p => p.MemberId.ToString(),
            p => new SFDPointSource(p.Latitude, p.Longitude, p.Speed, p.Bearing, DateTime.SpecifyKind(p.RecordedAtUtc, DateTimeKind.Utc)));
        var memberDtos = SFDTrackingBuilder.Members(members, lookup, now);

        var routes = routeRows.Select(r => new SFDRouteSource(r.Id.ToString(), r.Name, r.Area,
            JsonSerializer.Deserialize<List<SFDLatLon>>(r.CoordinatesJson) ?? [])).ToList();

        return new SFDTrackingDto(date, SFDTrackingBuilder.FormatTime(now), memberDtos, SFDTrackingBuilder.Routes(routes, memberDtos));
    }
}
