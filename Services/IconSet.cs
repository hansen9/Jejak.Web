using System.Net;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace SFD.Services;

/// <summary>Lucide icon bodies loaded once from wwwroot/icons, rendered inline so they inherit currentColor.</summary>
public class IconSet
{
    readonly Dictionary<string, string> bodies = new();

    public IconSet(IWebHostEnvironment env)
    {
        var dir = Path.Combine(env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"), "icons");
        if (!Directory.Exists(dir)) return;
        foreach (var file in Directory.EnumerateFiles(dir, "*.svg"))
            bodies[Path.GetFileNameWithoutExtension(file)] = File.ReadAllText(file).Trim();
    }

    public string? Body(string name) => bodies.GetValueOrDefault(name);
}

public static class IconExtensions
{
    public static IHtmlContent Icon(this IHtmlHelper html, string name, int size = 18, double stroke = 2, string? cssClass = null)
    {
        var body = html.ViewContext.HttpContext.RequestServices.GetRequiredService<IconSet>().Body(name) ?? "";
        var cls = cssClass is null ? "" : $" class=\"{WebUtility.HtmlEncode(cssClass)}\"";
        return new HtmlString($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{size}\" height=\"{size}\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"{stroke.ToString(System.Globalization.CultureInfo.InvariantCulture)}\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"{cls}>{body}</svg>");
    }
}
