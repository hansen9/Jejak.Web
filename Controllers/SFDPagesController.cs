using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using SFD.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace SFD.Controllers;

[AllowAnonymous]
public class SFDPagesController : Controller
{
    /// <summary>Signed-in user as the page's JavaScript sees it; null when signed out.</summary>
    public static object? CurrentUser(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true
            ? new { name = user.FindFirstValue(ClaimTypes.Name), email = user.FindFirstValue(ClaimTypes.Email) }
            : null;

    // Pages embed the signed-in user and a CSRF token, so shared caches must never keep them.
    void NoStore() => Response.Headers.CacheControl = "private, no-store";

    [HttpGet("/SFD")]
    public IActionResult Index()
    {
        NoStore();
        ViewData["Title"] = "Leader Dashboard — Pemantauan Tim";
        ViewData["Description"] = "Pantau posisi GPS, kecepatan, arah perjalanan, jarak tempuh anggota, dan cakupan rute tim lapangan dalam satu dashboard.";
        return View("~/Views/SFD/SFDDashboard.cshtml");
    }

    [HttpGet("/SFD/masuk")]
    public IActionResult Login(string? returnUrl)
    {
        NoStore();
        // Only local paths are honoured, so the login page cannot be used as an open redirect.
        var target = Url.IsLocalUrl(returnUrl) ? returnUrl! : Url.Content("~/SFD");
        if (User.Identity?.IsAuthenticated == true) return LocalRedirect(target);
        ViewData["Title"] = "Masuk — Leader Dashboard";
        ViewData["Description"] = "Masuk sebagai leader untuk mengelola data tim lapangan Anda di Leader Dashboard.";
        ViewData["NoIndex"] = true;
        ViewBag.ReturnUrl = target;
        return View("~/Views/SFD/SFDLogin.cshtml");
    }

    [HttpGet("/SFD/perangkat")]
    public IActionResult Device()
    {
        NoStore();
        ViewData["Title"] = "GPS Perangkat — Leader Dashboard";
        ViewData["Description"] = "Hubungkan perangkat yang Anda kelola untuk mengirim lokasi GPS secara privat ke dashboard Leader Dashboard.";
        return View("~/Views/SFD/SFDDevice.cshtml");
    }

    [HttpGet("/SFD/error/{code:int?}")]
    [HttpPost("/SFD/error/{code:int?}")]
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
        return View("~/Views/SFD/SFDError.cshtml");
    }
}
