using InternalWebApp.Helper;
using SFD.Models;

namespace SFD.Repositories;

public class SFDMemberRepository(DBHelper db) : ISFDMemberRepository
{
    public async Task<IReadOnlyList<SFDTeamMember>> GetByOwnerAsync(string ownerId) =>
        (await db.QueryAsync<SFDTeamMember>(
            "SELECT Id, OwnerId, Name, Area, Color, Phone, CreatedUtc FROM dbo.SFD_Members WHERE OwnerId = @ownerId ORDER BY Name",
            new { ownerId })).ToList();

    public Task AddAsync(SFDTeamMember member) => db.ExecuteAsync(
        "INSERT INTO dbo.SFD_Members (Id, OwnerId, Name, Area, Color, Phone, CreatedUtc) VALUES (@Id, @OwnerId, @Name, @Area, @Color, @Phone, @CreatedUtc)",
        member);

    public async Task<bool> ExistsAsync(Guid memberId, string ownerId) =>
        await db.QueryFirstOrDefaultAsync<int?>(
            "SELECT TOP 1 1 FROM dbo.SFD_Members WHERE Id = @memberId AND OwnerId = @ownerId",
            new { memberId, ownerId }) is not null;
}
