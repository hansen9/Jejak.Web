using SFD.Models;

namespace SFD.Repositories;

public interface ISFDRouteRepository
{
    Task<IReadOnlyList<SFDPlannedRoute>> GetByOwnerAsync(string ownerId);
    /// <summary>Inserts all routes in one statement, so a failure stores none of them.</summary>
    Task AddRangeAsync(IReadOnlyCollection<SFDPlannedRoute> routes);
}
