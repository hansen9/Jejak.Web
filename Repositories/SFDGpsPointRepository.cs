using System.Globalization;
using System.Text.Json;
using InternalWebApp.Helper;
using SFD.Models;

namespace SFD.Repositories;

public class SFDGpsPointRepository(DBHelper db) : ISFDGpsPointRepository
{
    public async Task<IReadOnlyList<SFDGpsPoint>> GetByOwnerAsync(string ownerId, DateTime startUtc, DateTime endUtc) =>
        (await db.QueryAsync<SFDGpsPoint>(
            @"SELECT Id, OwnerId, MemberId, Latitude, Longitude, Speed, Bearing, Accuracy, RecordedAtUtc
              FROM dbo.SFD_GpsPoints
              WHERE OwnerId = @ownerId AND RecordedAtUtc >= @startUtc AND RecordedAtUtc < @endUtc
              ORDER BY RecordedAtUtc",
            new { ownerId, startUtc, endUtc })).ToList();

    public Task AddAsync(SFDGpsPoint p) => db.ExecuteAsync(
        @"INSERT INTO dbo.SFD_GpsPoints (OwnerId, MemberId, Latitude, Longitude, Speed, Bearing, Accuracy, RecordedAtUtc)
          VALUES (@OwnerId, @MemberId, @Latitude, @Longitude, @Speed, @Bearing, @Accuracy, @RecordedAtUtc)", p);

    // One INSERT ... SELECT over a JSON document is a single statement, hence atomic, without needing a transaction in DBHelper.
    // Requires SQL Server 2016+ (OPENJSON) at compatibility level 130 or higher.
    public async Task AddRangeAsync(IReadOnlyCollection<SFDGpsPoint> points)
    {
        if (points.Count == 0) return;
        var json = JsonSerializer.Serialize(points.Select(p => new
        {
            ownerId = p.OwnerId, memberId = p.MemberId, latitude = p.Latitude, longitude = p.Longitude,
            speed = p.Speed, bearing = p.Bearing, accuracy = p.Accuracy,
            recordedAtUtc = p.RecordedAtUtc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffffff", CultureInfo.InvariantCulture),
        }));
        await db.ExecuteAsync(
            @"INSERT INTO dbo.SFD_GpsPoints (OwnerId, MemberId, Latitude, Longitude, Speed, Bearing, Accuracy, RecordedAtUtc)
              SELECT OwnerId, MemberId, Latitude, Longitude, Speed, Bearing, Accuracy, RecordedAtUtc
              FROM OPENJSON(@json) WITH (
                  OwnerId nvarchar(64) '$.ownerId', MemberId uniqueidentifier '$.memberId',
                  Latitude float '$.latitude', Longitude float '$.longitude', Speed float '$.speed',
                  Bearing float '$.bearing', Accuracy float '$.accuracy', RecordedAtUtc datetime2 '$.recordedAtUtc')",
            new { json });
    }
}
