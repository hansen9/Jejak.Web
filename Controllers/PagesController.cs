using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using SFD.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace SFD.Controllers;

[AllowAnonymous]
public class PagesController : Controller
{
    /// <summary>Signed-in user as the page's JavaScript sees it; null when signed out.</summary>
    public static object? CurrentUser(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true
            ? new { name = user.FindFirstValue(ClaimTypes.Name), email = user.FindFirstValue(ClaimTypes.Email) }
            : null;

    // Pages embed the signed-in user and a CSRF token, so shared caches must never keep them.
    void NoStore() => Response.Headers.CacheControl = "private, no-store";

    [HttpGet("/")]
    public IActionResult Index()
    {
        NoStore();
        ViewData["Title"] = "Leader Dashboard — Pemantauan Tim";
        ViewData["Description"] = "Pantau posisi GPS, kecepatan, arah perjalanan, jarak tempuh anggota, dan cakupan rute tim lapangan dalam satu dashboard.";
        return View("Dashboard");
    }

    [HttpGet("/masuk")]
    public IActionResult Login(string? returnUrl)
    {
        NoStore();
        // Only local paths are honoured, so the login page cannot be used as an open redirect.
        var target = Url.IsLocalUrl(returnUrl) ? returnUrl! : "/";
        if (User.Identity?.IsAuthenticated == true) return LocalRedirect(target);
        ViewData["Title"] = "Masuk — Leader Dashboard";
        ViewData["Description"] = "Masuk sebagai leader untuk mengelola data tim lapangan Anda di Leader Dashboard.";
        ViewData["NoIndex"] = true;
        ViewBag.ReturnUrl = target;
        return View();
    }

    [HttpGet("/perangkat")]
    public IActionResult Device()
    {
        NoStore();
        ViewData["Title"] = "GPS Perangkat — Leader Dashboard";
        ViewData["Description"] = "Hubungkan perangkat yang Anda kelola untuk mengirim lokasi GPS secara privat ke dashboard Leader Dashboard.";
        return View();
    }

    [HttpGet("/error/{code:int?}")]
    [HttpPost("/error/{code:int?}")]
    [IgnoreAntiforgeryToken]
    public IActionResult Error(int? code)
    {
        NoStore();
        var status = code ?? 500;
        ViewData["Title"] = status == 404 ? "404 — Leader Dashboard" : "Terjadi kendala — Leader Dashboard";
        ViewData["NoIndex"] = true;
        ViewBag.Heading = status == 404 ? "404" : "Terjadi kendala";
        ViewBag.Details = status == 404 ? "Halaman yang Anda cari tidak ditemukan." : "Terjadi kesalahan yang tidak terduga. Silakan muat ulang halaman.";
        ViewBag.RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        Response.StatusCode = status;
        return View();
    }

    [HttpGet("/robots.txt")]
    public IActionResult Robots()
    {
        var origin = SiteOrigin.From(Request);
        var host = Request.Host.Host;
        string[] unpublished = [".app-preview.com", ".app-preview.io", ".hostingersite.com", ".hostingersite.dev"];
        const string signal = "Content-Signal: search=yes, ai-input=yes, ai-train=no";
        string[] answerAgents = ["OAI-SearchBot", "ChatGPT-User", "Claude-SearchBot", "Claude-User", "PerplexityBot", "Perplexity-User", "DuckAssistBot", "MistralAI-User", "meta-webindexer", "meta-externalfetcher", "Amzn-SearchBot", "Amzn-User"];
        string[] trainingAgents = ["GPTBot", "ClaudeBot", "Google-Extended", "Applebot-Extended", "meta-externalagent", "Amazonbot", "Bytespider"];

        var lines = unpublished.Any(host.EndsWith)
            ? ["User-agent: *", "Disallow: /"]
            : new[] { "User-agent: *", signal, "Allow: /", "" }
                .Concat(answerAgents.SelectMany(a => new[] { $"User-agent: {a}", signal, "Allow: /", "" }))
                .Concat(trainingAgents.SelectMany(a => new[] { $"User-agent: {a}", "Disallow: /", "" }))
                .Append($"Sitemap: {origin}/sitemap.xml").ToArray();

        Response.Headers.CacheControl = "public, max-age=3600";
        return Content(string.Join('\n', lines) + "\n", "text/plain", Encoding.UTF8);
    }

    [HttpGet("/sitemap.xml")]
    public IActionResult Sitemap()
    {
        var origin = SiteOrigin.From(Request);
        var urls = string.Concat(new[] { "/", "/perangkat" }.Select(p => $"\t<url>\n\t\t<loc>{System.Net.WebUtility.HtmlEncode(origin + p)}</loc>\n\t</url>\n"));
        Response.Headers.CacheControl = "public, max-age=3600";
        Response.Headers.AccessControlAllowOrigin = "*";
        return Content($"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n{urls}</urlset>\n", "application/xml", Encoding.UTF8);
    }
}
