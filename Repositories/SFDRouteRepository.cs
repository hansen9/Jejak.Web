using System.Globalization;
using System.Text.Json;
using InternalWebApp.Helper;
using SFD.Models;

namespace SFD.Repositories;

public class SFDRouteRepository(DBHelper db) : ISFDRouteRepository
{
    public async Task<IReadOnlyList<SFDPlannedRoute>> GetByOwnerAsync(string ownerId) =>
        (await db.QueryAsync<SFDPlannedRoute>(
            "SELECT Id, OwnerId, Name, Area, CoordinatesJson, CreatedUtc FROM dbo.SFD_Routes WHERE OwnerId = @ownerId ORDER BY Name",
            new { ownerId })).ToList();

    // Single statement over a JSON document, so the whole import is atomic. Requires SQL Server 2016+ (OPENJSON).
    public async Task AddRangeAsync(IReadOnlyCollection<SFDPlannedRoute> routes)
    {
        if (routes.Count == 0) return;
        var json = JsonSerializer.Serialize(routes.Select(r => new
        {
            id = r.Id, ownerId = r.OwnerId, name = r.Name, area = r.Area, coordinatesJson = r.CoordinatesJson,
            createdUtc = r.CreatedUtc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffffff", CultureInfo.InvariantCulture),
        }));
        await db.ExecuteAsync(
            @"INSERT INTO dbo.SFD_Routes (Id, OwnerId, Name, Area, CoordinatesJson, CreatedUtc)
              SELECT Id, OwnerId, Name, Area, CoordinatesJson, CreatedUtc
              FROM OPENJSON(@json) WITH (
                  Id uniqueidentifier '$.id', OwnerId nvarchar(64) '$.ownerId', Name nvarchar(100) '$.name',
                  Area nvarchar(100) '$.area', CoordinatesJson nvarchar(max) '$.coordinatesJson', CreatedUtc datetime2 '$.createdUtc')",
            new { json });
    }
}
