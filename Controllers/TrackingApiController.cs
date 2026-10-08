using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using SFD.Data;
using SFD.Models;
using SFD.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SFD.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public partial class TrackingApiController(AppDbContext db, TrackingService tracking) : ControllerBase
{
    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex HexColor();

    Guid OwnerId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>Dashboard data for one WIB day. <c>demo=true</c> needs no account and returns the fixed sample team.</summary>
    [HttpGet("tracking")]
    [AllowAnonymous]
    public async Task<IActionResult> Get([FromQuery] string? date, [FromQuery] bool demo, CancellationToken ct)
    {
        date ??= TrackingBuilder.Today();
        if (!TrackingBuilder.TryDayRange(date, out _, out _)) return BadRequest(new { error = "Tanggal tidak valid." });
        if (demo) return Ok(DemoData.Build(date));
        if (User.Identity?.IsAuthenticated != true) return Unauthorized();
        return Ok(await tracking.BuildAsync(OwnerId, date, ct));
    }

    [HttpGet("members")]
    public async Task<IActionResult> Members(CancellationToken ct)
    {
        var owner = OwnerId;
        var rows = await db.Members.AsNoTracking().Where(m => m.OwnerId == owner).OrderBy(m => m.Name).ToListAsync(ct);
        return Ok(rows.Select(m => new { id = m.Id.ToString(), m.Name, m.Area, m.Color, m.Phone }));
    }

    [HttpPost("members")]
    public async Task<IActionResult> AddMember([FromBody] MemberRequest request, CancellationToken ct)
    {
        var name = request.Name?.Trim() ?? "";
        var area = request.Area?.Trim() ?? "";
        var phone = request.Phone?.Trim() ?? "";
        var color = request.Color?.Trim() ?? "";
        if (name.Length is 0 or > 100 || area.Length > 100 || phone.Length > 30 || (color.Length > 0 && !HexColor().IsMatch(color)))
            return BadRequest(new { error = "Data anggota tidak valid." });

        db.Members.Add(new TeamMember { OwnerId = OwnerId, Name = name, Area = area, Color = color, Phone = phone.Length > 0 ? phone : null });
        await db.SaveChangesAsync(ct);
        return StatusCode(StatusCodes.Status201Created);
    }

    /// <summary>One position from the device sender page.</summary>
    [HttpPost("gps")]
    public async Task<IActionResult> AddPoint([FromBody] GpsPointRequest p, CancellationToken ct)
    {
        var owner = OwnerId;
        var at = p.RecordedAt.ToUniversalTime();
        if (!double.IsFinite(p.Latitude) || Math.Abs(p.Latitude) > 90 || !double.IsFinite(p.Longitude) || Math.Abs(p.Longitude) > 180
            || !double.IsFinite(p.Speed) || p.Speed < 0 || !double.IsFinite(p.Bearing) || p.Bearing is < 0 or > 360
            || !double.IsFinite(p.Accuracy) || p.Accuracy < 0 || at > DateTime.UtcNow.AddMinutes(5))
            return BadRequest(new { error = "Titik GPS tidak valid." });
        if (!await db.Members.AnyAsync(m => m.Id == p.MemberId && m.OwnerId == owner, ct)) return NotFound();

        db.GpsPoints.Add(new GpsPoint
        {
            OwnerId = owner, MemberId = p.MemberId, Latitude = p.Latitude, Longitude = p.Longitude,
            Speed = p.Speed, Bearing = p.Bearing, Accuracy = p.Accuracy, RecordedAtUtc = at,
        });
        await db.SaveChangesAsync(ct);
        return StatusCode(StatusCodes.Status201Created);
    }

    [HttpPost("import/routes")]
    [RequestSizeLimit(ImportParsers.MaxFileBytes + 64 * 1024)]
    public async Task<IActionResult> ImportRoutes(IFormFile? file, CancellationToken ct)
    {
        var text = await ReadUpload(file, ct);
        if (text is null) return BadRequest(new { error = "Ukuran berkas maksimal 5 MB." });
        try
        {
            var routes = ImportParsers.ParseGeoJson(text);
            var owner = OwnerId;
            db.Routes.AddRange(routes.Select(r => new PlannedRoute
            {
                OwnerId = owner, Name = r.Name, Area = r.Area, CoordinatesJson = JsonSerializer.Serialize(r.Coordinates),
            }));
            await db.SaveChangesAsync(ct);
            return Ok(new { imported = routes.Count });
        }
        catch (ImportException e) { return BadRequest(new { error = e.Message }); }
    }

    [HttpPost("import/gps")]
    [RequestSizeLimit(ImportParsers.MaxFileBytes + 64 * 1024)]
    public async Task<IActionResult> ImportGps(IFormFile? file, [FromForm] Guid member, CancellationToken ct)
    {
        var owner = OwnerId;
        if (!await db.Members.AnyAsync(m => m.Id == member && m.OwnerId == owner, ct)) return BadRequest(new { error = "Pilih anggota pemilik perjalanan." });
        var text = await ReadUpload(file, ct);
        if (text is null) return BadRequest(new { error = "Ukuran berkas maksimal 5 MB." });
        try
        {
            var rows = ImportParsers.ParseGpsCsv(text);
            // One SaveChanges is one transaction: a bad file imports nothing instead of half the rows.
            db.GpsPoints.AddRange(rows.Select(r => new GpsPoint
            {
                OwnerId = owner, MemberId = member, Latitude = r.Latitude, Longitude = r.Longitude,
                Speed = r.Speed, Bearing = r.Bearing, Accuracy = 0, RecordedAtUtc = r.RecordedAtUtc,
            }));
            await db.SaveChangesAsync(ct);
            return Ok(new { imported = rows.Count });
        }
        catch (ImportException e) { return BadRequest(new { error = e.Message }); }
    }

    static async Task<string?> ReadUpload(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return "";
        if (file.Length > ImportParsers.MaxFileBytes) return null;
        using var reader = new StreamReader(file.OpenReadStream());
        return await reader.ReadToEndAsync(ct);
    }
}
