using SFD.Models;

namespace SFD.Services;

/// <summary>Fixed sample team for the signed-out demo. Coverage on demo routes is illustrative.</summary>
public static class SFDDemoData
{
    static SFDLatLon[] P(params double[][] pts) => pts.Select(p => new SFDLatLon(p[0], p[1])).ToArray();

    static SFDMemberDto M(string id, string name, string area, string color, double[] pos, double speed, double bearing, double distance,
        string status, int battery, string lastSeen, double[][] trace, double[] hours) =>
        new(id, name, SFDTrackingBuilder.Initials(name), area, color, null, new SFDLatLon(pos[0], pos[1]), speed, bearing, distance, status,
            battery, lastSeen, P(trace).ToList(), hours);

    public static readonly IReadOnlyList<SFDMemberDto> Members =
    [
        M("demo-1", "Andi Pratama", "Kebayoran Baru", "#815796", [-6.2391, 106.8004], 24, 45, 28.4, "bergerak", 82, "14.32 WIB",
            [[-6.2591, 106.8001], [-6.2552, 106.8000], [-6.2490, 106.8003], [-6.2440, 106.8005], [-6.2391, 106.8004]], [0, 1.2, 2.6, 4.0, 3.8, 2.7, 1.6, 3.5, 4.2, 2.3, 1.4, 1.1]),
        M("demo-2", "Budi Santoso", "Senayan", "#cf8759", [-6.2163, 106.8028], 18, 315, 24.8, "bergerak", 75, "14.32 WIB",
            [[-6.2350, 106.7964], [-6.2308, 106.7964], [-6.2270, 106.7990], [-6.2228, 106.8018], [-6.2190, 106.8050], [-6.2163, 106.8028]], [0, 1.8, 2.1, 3.5, 3.1, 1.9, 1.2, 2.8, 3.6, 2.4, 1.3, 1.1]),
        M("demo-3", "Citra Lestari", "Setiabudi", "#568e86", [-6.2201, 106.8294], 32, 90, 22.6, "bergerak", 91, "14.31 WIB",
            [[-6.2370, 106.8248], [-6.2300, 106.8233], [-6.2253, 106.8230], [-6.2207, 106.8240], [-6.2201, 106.8294]], [0, 1.0, 2.4, 2.5, 3.7, 1.8, 1.0, 2.4, 3.5, 2.0, 1.2, 1.1]),
        M("demo-4", "Dimas Saputra", "Mampang Prapatan", "#6483b6", [-6.2519, 106.8279], 0, 180, 19.2, "berhenti", 64, "14.32 WIB",
            [[-6.2390, 106.8300], [-6.2430, 106.8295], [-6.2475, 106.8285], [-6.2519, 106.8279]], [0, 0.8, 2.1, 2.8, 3.2, 1.4, 0.7, 2.1, 2.8, 1.6, 1.0, 0.7]),
        M("demo-5", "Eka Putri", "Tebet", "#a8788c", [-6.2358, 106.8503], 21, 0, 18.3, "bergerak", 88, "14.32 WIB",
            [[-6.2501, 106.8522], [-6.2453, 106.8508], [-6.2410, 106.8504], [-6.2358, 106.8503]], [0, 0.9, 1.7, 2.6, 2.9, 1.5, 0.8, 2.2, 2.4, 1.5, 1.1, 0.7]),
        M("demo-6", "Fajar Hidayat", "Gandaria", "#88985c", [-6.2491, 106.7874], 15, 225, 16.7, "bergerak", 57, "14.31 WIB",
            [[-6.2378, 106.7822], [-6.2404, 106.7850], [-6.2425, 106.7870], [-6.2460, 106.7872], [-6.2491, 106.7874]], [0, 0.7, 1.8, 2.2, 2.5, 1.6, 0.9, 1.9, 2.1, 1.4, 0.9, 0.7]),
        M("demo-7", "Gilang Ramadhan", "Pancoran", "#baaa71", [-6.2432, 106.8439], 27, 270, 12.1, "bergerak", 72, "14.32 WIB",
            [[-6.2478, 106.8580], [-6.2470, 106.8533], [-6.2460, 106.8495], [-6.2432, 106.8439]], [0, 0.4, 1.0, 1.6, 1.9, 1.1, 0.6, 1.5, 1.6, 1.1, 0.8, 0.5]),
        M("demo-8", "Hendra Wijaya", "Kuningan", "#7c8398", [-6.2331, 106.8374], 0, 0, 6.5, "offline", 12, "14.08 WIB",
            [[-6.2255, 106.8335], [-6.2290, 106.8345], [-6.2331, 106.8374]], [0, 0.3, 0.6, 1.0, 1.2, 0.7, 0.4, 0.8, 0.8, 0.4, 0.2, 0.1]),
    ];

    public static readonly IReadOnlyList<SFDRouteSource> Routes =
    [
        new("route-1", "Koridor Kebayoran", "Kebayoran Baru", P([-6.2591, 106.8001], [-6.2490, 106.8003], [-6.2391, 106.8004], [-6.2310, 106.8007], [-6.2244, 106.8075], [-6.2212, 106.8125]).ToList(), 0.78),
        new("route-2", "Koridor Senayan", "Senayan", P([-6.2350, 106.7964], [-6.2308, 106.7964], [-6.2270, 106.7990], [-6.2190, 106.8050], [-6.2163, 106.8028], [-6.2095, 106.8013]).ToList(), 0.85),
        new("route-3", "Koridor Setiabudi", "Setiabudi", P([-6.2370, 106.8248], [-6.2300, 106.8233], [-6.2207, 106.8240], [-6.2201, 106.8294], [-6.2180, 106.8386], [-6.2111, 106.8456]).ToList(), 0.67),
        new("route-4", "Koridor Mampang", "Mampang Prapatan", P([-6.2390, 106.8300], [-6.2475, 106.8285], [-6.2519, 106.8279], [-6.2610, 106.8256], [-6.2681, 106.8239]).ToList(), 0.73),
        new("route-5", "Koridor Tebet", "Tebet", P([-6.2501, 106.8522], [-6.2453, 106.8508], [-6.2358, 106.8503], [-6.2290, 106.8505], [-6.2231, 106.8530]).ToList(), 0.6),
        new("route-6", "Koridor Gandaria", "Gandaria", P([-6.2378, 106.7822], [-6.2425, 106.7870], [-6.2491, 106.7874], [-6.2555, 106.7882]).ToList(), 0.81),
        new("route-7", "Koridor Pancoran", "Pancoran", P([-6.2478, 106.8580], [-6.2460, 106.8495], [-6.2432, 106.8439], [-6.2400, 106.8365], [-6.2385, 106.8304]).ToList(), 0.65),
        new("route-8", "Koridor Kuningan", "Kuningan", P([-6.2255, 106.8335], [-6.2290, 106.8345], [-6.2331, 106.8374], [-6.2370, 106.8381], [-6.2390, 106.8403]).ToList(), 0.35),
    ];

    /// <summary>The demo describes "today"; any other date has no demo data, as in the original dashboard.</summary>
    public static SFDTrackingDto Build(string date)
    {
        var visible = date == SFDTrackingBuilder.Today();
        var members = visible ? Members.ToList() : [];
        var routes = visible ? SFDTrackingBuilder.Routes(Routes, members, demo: true) : [];
        return new SFDTrackingDto(true, date, "14.32 WIB", members, routes);
    }
}
