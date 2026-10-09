using SFD.Models;

namespace SFD.Repositories;

public interface ISFDGpsPointRepository
{
    /// <summary>Points with <paramref name="startUtc"/> &lt;= RecordedAtUtc &lt; <paramref name="endUtc"/>, oldest first.</summary>
    Task<IReadOnlyList<SFDGpsPoint>> GetByOwnerAsync(string ownerId, DateTime startUtc, DateTime endUtc);
    Task AddAsync(SFDGpsPoint point);
    /// <summary>Inserts all points in one statement, so a failure stores none of them.</summary>
    Task AddRangeAsync(IReadOnlyCollection<SFDGpsPoint> points);
}
