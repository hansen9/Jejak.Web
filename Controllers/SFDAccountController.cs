using System.Security.Claims;
using SFD.Models;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace SFD.Controllers;

[ApiController]
[Route("SFD/akun")]
[AllowAnonymous]
public class SFDAccountController(IHttpClientFactory http, IAntiforgery antiforgery, ILogger<SFDAccountController> log) : ControllerBase
{
    const string LoginEndpoint = "https://www.solofleet.com/AndroidDevice/logindirect";

    [HttpPost("masuk")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login([FromBody] SFDLoginRequest request)
    {
        var username = request.Username?.Trim() ?? "";
        if (username.Length is 0 or > 254 || string.IsNullOrEmpty(request.Password)) return Unauthorized();

        JsonElement account;
        try
        {
            var url = $"{LoginEndpoint}?username={Uri.EscapeDataString(username)}&password={Uri.EscapeDataString(request.Password)}";
            using var response = await http.CreateClient().PostAsync(url, null, HttpContext.RequestAborted);
            if (!response.IsSuccessStatusCode) return Unauthorized();
            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(HttpContext.RequestAborted), cancellationToken: HttpContext.RequestAborted);
            if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0) return Unauthorized();
            account = doc.RootElement[0].Clone();
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or TaskCanceledException)
        {
            log.LogWarning("SoloFleet login service unavailable: {Type}", e.GetType().Name);
            return StatusCode(StatusCodes.Status502BadGateway);
        }

        if (!IsLeader(account)) return StatusCode(StatusCodes.Status403Forbidden, new { error = "not-leader" });

        var remoteId = Text(account, "UserId") ?? username;
        var name = Text(account, "actualname") ?? Text(account, "UserName") ?? username;
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            // The SoloFleet UserId owns the leader's members, GPS points and routes.
            new Claim(ClaimTypes.NameIdentifier, remoteId),
            new Claim(ClaimTypes.Name, name),
            new Claim(ClaimTypes.Email, Text(account, "UserName") ?? username),
        ], CookieAuthenticationDefaults.AuthenticationScheme));

        await HttpContext.SignInAsync(principal, new AuthenticationProperties { IsPersistent = true });
        return Ok(Session(principal));
    }

    static string? Text(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind is JsonValueKind.String or JsonValueKind.Number
            ? (v.ToString() is { Length: > 0 } t ? t : null) : null;

    // The flag's JSON type is not documented, so accept true / 1 / "1" / "true" / "y".
    static bool IsLeader(JsonElement e) =>
        e.TryGetProperty("isdmoleader", out var v) && v.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.Number => v.TryGetInt32(out var n) && n != 0,
            JsonValueKind.String => v.GetString()?.Trim().ToLowerInvariant() is "1" or "true" or "y" or "yes",
            _ => false,
        };

    [HttpPost("keluar")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync();
        return Ok(Session(new ClaimsPrincipal(new ClaimsIdentity())));
    }

    // Antiforgery tokens are bound to the signed-in identity, so the page needs a fresh one after login/logout.
    object Session(ClaimsPrincipal principal)
    {
        HttpContext.User = principal;
        return new { user = SFDPagesController.CurrentUser(principal), csrf = antiforgery.GetAndStoreTokens(HttpContext).RequestToken };
    }
}
