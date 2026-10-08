namespace SFD.Models;

public class AppUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = "";
    public string Name { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

public class TeamMember
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public string Name { get; set; } = "";
    public string Area { get; set; } = "";
    public string Color { get; set; } = "";
    public string? Phone { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

public class GpsPoint
{
    public long Id { get; set; }
    public Guid OwnerId { get; set; }
    public Guid MemberId { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Speed { get; set; }
    public double Bearing { get; set; }
    public double Accuracy { get; set; }
    public DateTime RecordedAtUtc { get; set; }
}

public class PlannedRoute
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public string Name { get; set; } = "";
    public string Area { get; set; } = "";
    /// <summary>JSON array of [latitude, longitude] pairs.</summary>
    public string CoordinatesJson { get; set; } = "[]";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
