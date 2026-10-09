namespace SFD.Models;

/// <summary>A project boundary polygon. <paramref name="Points"/> serialise as [lat, lon] pairs, ready for Leaflet.</summary>
public record SFDBoundaryDto(int Id, string Name, List<SFDLatLon> Points);
