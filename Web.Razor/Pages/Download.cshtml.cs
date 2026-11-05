using Microsoft.AspNetCore.Mvc.RazorPages;
using Server.Base.Core.Configs;
using System.Globalization;
using Web.Razor.Services;

namespace Web.Razor.Pages;

/// <summary>One card on the downloads page. With no <see cref="Url"/> it shows as coming soon.</summary>
public record DownloadPlatform(string Name, bool Mobile, string Detail, string Url);

public class DownloadModel(InternalRwConfig config, PagesService pService) : PageModel
{
    public List<DownloadPlatform> Platforms { get; private set; } = [];

    public void OnGet()
    {
        ViewData["ServerName"] = config.ServerName;

        var zip = new FileInfo(pService.ZipPath);
        var latest = ChangelogRelease.Latest(pService);
        var size = zip.Exists ? (zip.Length / 1048576d).ToString("0", CultureInfo.InvariantCulture) + " MB" : null;
        var detail = string.Join(" · ", new[] { latest == null ? null : "Version " + latest, size }.Where(s => s != null));

        Platforms =
        [
            new("Windows", false, detail, zip.Exists ? "/StartDownload" : null),
            // To publish the mobile version, give this a detail and a download URL.
            new("Mobile", true, "We're working on it.", null),
        ];
    }
}
