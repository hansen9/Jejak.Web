using SFD.Models;

namespace SFD.Repositories;

public interface ISFDMemberRepository
{
    Task<IReadOnlyList<SFDTeamMember>> GetByOwnerAsync(string ownerId);
    Task AddAsync(SFDTeamMember member);
    Task<bool> ExistsAsync(Guid memberId, string ownerId);
}
