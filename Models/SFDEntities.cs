namespace SFD.Models;

// OwnerId is the SoloFleet UserId of the leader who owns the row.
public class SFDTeamMember
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OwnerId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Area { get; set; } = "";
    public string Color { get; set; } = "";
    public string? Phone { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

public class SFDGpsPoint
{
    public long Id { get; set; }
    public string OwnerId { get; set; } = "";
    public Guid MemberId { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Speed { get; set; }
    public double Bearing { get; set; }
    public double Accuracy { get; set; }
    public DateTime RecordedAtUtc { get; set; }
}

public class SFDPlannedRoute
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OwnerId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Area { get; set; } = "";
    /// <summary>JSON array of [latitude, longitude] pairs.</summary>
    public string CoordinatesJson { get; set; } = "[]";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
