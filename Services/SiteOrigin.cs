namespace SFD.Services;

/// <summary>
/// The public origin used in canonical links, og:url and the sitemap. TLS usually terminates at a proxy, so the scheme
/// comes from the hostname: every non-local host is served over HTTPS.
/// </summary>
public static class SiteOrigin
{
    static readonly HashSet<string> Local = new(StringComparer.OrdinalIgnoreCase) { "localhost", "127.0.0.1", "0.0.0.0", "[::1]" };

    public static string From(HttpRequest request) =>
        $"{(Local.Contains(request.Host.Host) ? "http" : "https")}://{request.Host}";
}
