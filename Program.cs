using System.Threading.RateLimiting;
using InternalWebApp.Helper;
using SFD.Repositories;
using SFD.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(options => options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");

builder.Services.AddHttpClient();
builder.Services.AddSingleton<DBHelper>();
builder.Services.AddScoped<ISFDMemberRepository, SFDMemberRepository>();
builder.Services.AddScoped<ISFDGpsPointRepository, SFDGpsPointRepository>();
builder.Services.AddScoped<ISFDRouteRepository, SFDRouteRepository>();
builder.Services.AddScoped<SFDTrackingService>();
builder.Services.AddSingleton<SFDIconSet>();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "sfd.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.SlidingExpiration = true;
        // There is no login page: the dashboard signs in through a dialog, so API callers get a status code, not a redirect.
        options.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
        options.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
    });
builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);

var app = builder.Build();

app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/SFD/error");
    app.UseHsts();
}

app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    // The dashboard shows live location data; never let a shared cache keep it.
    if (context.Request.Path.StartsWithSegments("/SFD/api")) headers["Cache-Control"] = "no-store";
    await next();
});

// Friendly pages for browser navigation only; fetch() callers just read the status code.
app.UseWhen(
    context => HttpMethods.IsGet(context.Request.Method) && context.Request.Path.StartsWithSegments("/SFD") && !context.Request.Path.StartsWithSegments("/SFD/api"),
    branch => branch.UseStatusCodePagesWithReExecute("/SFD/error/{0}"));
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/SFD/api/health", () => Results.Ok(new { ok = true })).DisableAntiforgery();
app.MapControllers();

app.Run();
return 0;
