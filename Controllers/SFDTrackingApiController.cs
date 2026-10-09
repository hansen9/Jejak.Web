using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using SFD.Models;
using SFD.Repositories;
using SFD.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SFD.Controllers;

[ApiController]
[Route("SFD/api")]
[Authorize]
public partial class SFDTrackingApiController(ISFDMemberRepository memberRepo, ISFDGpsPointRepository pointRepo, ISFDRouteRepository routeRepo, SFDTrackingService tracking, SFDBoundaryService boundaries, ILogger<SFDTrackingApiController> log) : ControllerBase
{
    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex HexColor();

    string OwnerId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    /// <summary>Dashboard data for one WIB day, for the signed-in leader.</summary>
    [HttpGet("tracking")]
    public async Task<IActionResult> Get([FromQuery] string? date)
    {
        date ??= SFDTrackingBuilder.Today();
        if (!SFDTrackingBuilder.TryDayRange(date, out _, out _)) return BadRequest(new { error = "Tanggal tidak valid." });
        return Ok(await tracking.BuildAsync(OwnerId, date));
    }

    /// <summary>Project boundary polygons inside the visible map bounds (proxied from SoloFleet).</summary>
    [HttpGet("boundaries")]
    public Task<IActionResult> Boundaries([FromQuery] double latBottom, [FromQuery] double latTop, [FromQuery] double lonLeft, [FromQuery] double lonRight, CancellationToken ct) =>
        ProxyMapData(latBottom, latTop, lonLeft, lonRight, async () => await boundaries.GetAsync(latBottom, latTop, lonLeft, lonRight, ct));

    /// <summary>POI points inside the visible map bounds, as [lat, lon] pairs (proxied from SoloFleet).</summary>
    [HttpGet("pois")]
    public Task<IActionResult> Pois([FromQuery] double latBottom, [FromQuery] double latTop, [FromQuery] double lonLeft, [FromQuery] double lonRight, CancellationToken ct) =>
        ProxyMapData(latBottom, latTop, lonLeft, lonRight, async () => await boundaries.GetPointsAsync(latBottom, latTop, lonLeft, lonRight, ct));

    async Task<IActionResult> ProxyMapData(double latBottom, double latTop, double lonLeft, double lonRight, Func<Task<object>> fetch)
    {
        if (!double.IsFinite(latBottom) || !double.IsFinite(latTop) || !double.IsFinite(lonLeft) || !double.IsFinite(lonRight)
            || Math.Abs(latBottom) > 90 || Math.Abs(latTop) > 90 || Math.Abs(lonLeft) > 180 || Math.Abs(lonRight) > 180
            || latBottom >= latTop || lonLeft >= lonRight)
            return BadRequest(new { error = "Batas peta tidak valid." });

        try { return Ok(await fetch()); }
        catch (Exception e) when (e is HttpRequestException or JsonException or KeyNotFoundException or InvalidOperationException or FormatException or TaskCanceledException)
        {
            log.LogWarning("SoloFleet map service unavailable: {Type}", e.GetType().Name);
            return StatusCode(StatusCodes.Status502BadGateway);
        }
    }

    [HttpGet("members")]
    public async Task<IActionResult> Members()
    {
        var rows = await memberRepo.GetByOwnerAsync(OwnerId);
        return Ok(rows.Select(m => new { id = m.Id.ToString(), m.Name, m.Area, m.Color, m.Phone }));
    }

    [HttpPost("members")]
    public async Task<IActionResult> AddMember([FromBody] SFDMemberRequest request)
    {
        var name = request.Name?.Trim() ?? "";
        var area = request.Area?.Trim() ?? "";
        var phone = request.Phone?.Trim() ?? "";
        var color = request.Color?.Trim() ?? "";
        if (name.Length is 0 or > 100 || area.Length > 100 || phone.Length > 30 || (color.Length > 0 && !HexColor().IsMatch(color)))
            return BadRequest(new { error = "Data anggota tidak valid." });

        await memberRepo.AddAsync(new SFDTeamMember { OwnerId = OwnerId, Name = name, Area = area, Color = color, Phone = phone.Length > 0 ? phone : null });
        return StatusCode(StatusCodes.Status201Created);
    }

    /// <summary>One position from the device sender page.</summary>
    [HttpPost("gps")]
    public async Task<IActionResult> AddPoint([FromBody] SFDGpsPointRequest p)
    {
        var owner = OwnerId;
        var at = p.RecordedAt.ToUniversalTime();
        if (!double.IsFinite(p.Latitude) || Math.Abs(p.Latitude) > 90 || !double.IsFinite(p.Longitude) || Math.Abs(p.Longitude) > 180
            || !double.IsFinite(p.Speed) || p.Speed < 0 || !double.IsFinite(p.Bearing) || p.Bearing is < 0 or > 360
            || !double.IsFinite(p.Accuracy) || p.Accuracy < 0 || at > DateTime.UtcNow.AddMinutes(5))
            return BadRequest(new { error = "Titik GPS tidak valid." });
        if (!await memberRepo.ExistsAsync(p.MemberId, owner)) return NotFound();

        await pointRepo.AddAsync(new SFDGpsPoint
        {
            OwnerId = owner, MemberId = p.MemberId, Latitude = p.Latitude, Longitude = p.Longitude,
            Speed = p.Speed, Bearing = p.Bearing, Accuracy = p.Accuracy, RecordedAtUtc = at,
        });
        return StatusCode(StatusCodes.Status201Created);
    }

    [HttpPost("import/routes")]
    [RequestSizeLimit(SFDImportParsers.MaxFileBytes + 64 * 1024)]
    public async Task<IActionResult> ImportRoutes(IFormFile? file, CancellationToken ct)
    {
        var text = await ReadUpload(file, ct);
        if (text is null) return BadRequest(new { error = "Ukuran berkas maksimal 5 MB." });
        try
        {
            var routes = SFDImportParsers.ParseGeoJson(text);
            var owner = OwnerId;
            await routeRepo.AddRangeAsync(routes.Select(r => new SFDPlannedRoute
            {
                OwnerId = owner, Name = r.Name, Area = r.Area, CoordinatesJson = JsonSerializer.Serialize(r.Coordinates),
            }).ToList());
            return Ok(new { imported = routes.Count });
        }
        catch (SFDImportException e) { return BadRequest(new { error = e.Message }); }
    }

    [HttpPost("import/gps")]
    [RequestSizeLimit(SFDImportParsers.MaxFileBytes + 64 * 1024)]
    public async Task<IActionResult> ImportGps(IFormFile? file, [FromForm] Guid member, CancellationToken ct)
    {
        var owner = OwnerId;
        if (!await memberRepo.ExistsAsync(member, owner)) return BadRequest(new { error = "Pilih anggota pemilik perjalanan." });
        var text = await ReadUpload(file, ct);
        if (text is null) return BadRequest(new { error = "Ukuran berkas maksimal 5 MB." });
        try
        {
            var rows = SFDImportParsers.ParseGpsCsv(text);
            // The repository inserts all rows in one statement: a bad file imports nothing instead of half the rows.
            await pointRepo.AddRangeAsync(rows.Select(r => new SFDGpsPoint
            {
                OwnerId = owner, MemberId = member, Latitude = r.Latitude, Longitude = r.Longitude,
                Speed = r.Speed, Bearing = r.Bearing, Accuracy = 0, RecordedAtUtc = r.RecordedAtUtc,
            }).ToList());
            return Ok(new { imported = rows.Count });
        }
        catch (SFDImportException e) { return BadRequest(new { error = e.Message }); }
    }

    static async Task<string?> ReadUpload(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return "";
        if (file.Length > SFDImportParsers.MaxFileBytes) return null;
        using var reader = new StreamReader(file.OpenReadStream());
        return await reader.ReadToEndAsync(ct);
    }
}
