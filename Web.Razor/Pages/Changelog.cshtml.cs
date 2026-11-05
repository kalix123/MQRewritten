using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Server.Base.Core.Configs;
using Web.Razor.Services;

namespace Web.Razor.Pages;

public class ChangelogModel(InternalRwConfig config, PagesService pService) : PageModel
{
    public List<ChangelogRelease> Releases { get; private set; } = [];

    public void OnGet()
    {
        ViewData["ServerName"] = config.ServerName;
        Releases = ChangelogRelease.Read(pService);
    }
}

/// <summary>
/// One section of CHANGELOG.md, the client's changelog, which tools\make_release.ps1 copies next to the website
/// download (Data\Downloads). Lines for server operators are left out.
/// </summary>
public partial class ChangelogRelease
{
    public const string Unreleased = "Unreleased";

    public string Title { get; private init; }
    public string Date { get; private init; }
    public List<string> Intro { get; } = [];
    public List<string> Items { get; } = [];

    public bool IsUnreleased => Title == Unreleased;

    public static List<ChangelogRelease> Read(PagesService pService)
    {
        var releases = new List<ChangelogRelease>();
        var path = Path.Join(Path.GetDirectoryName(pService.ZipPath), "CHANGELOG.md");

        if (!File.Exists(path))
            return releases;

        ChangelogRelease release = null;
        var item = string.Empty;

        void EndItem()
        {
            if (item.Length > 0 && !item.StartsWith("Server operator", StringComparison.OrdinalIgnoreCase))
                release?.Items.Add(ToHtml(item));

            item = string.Empty;
        }

        foreach (var line in File.ReadLines(path))
        {
            if (line.StartsWith("## "))
            {
                EndItem();
                var title = line[3..].Trim();
                release = new ChangelogRelease { Title = title, Date = ToDate(title) };
                releases.Add(release);
            }
            else if (line.StartsWith("- "))
            {
                EndItem();
                item = line[2..].Trim();
            }
            else if (item.Length > 0 && line.StartsWith("  "))
            {
                item += " " + line.Trim();
            }
            else
            {
                EndItem();

                if (release != null && line.Trim().Length > 0)
                    release.Intro.Add(ToHtml(line.Trim()));
            }
        }

        EndItem();
        releases.RemoveAll(r => r.Items.Count == 0 && r.Intro.Count == 0);
        return releases;
    }

    /// <summary>The newest published version, or null when there's no changelog.</summary>
    public static string Latest(PagesService pService) =>
        Read(pService).FirstOrDefault(r => !r.IsUnreleased)?.Title;

    // Versions are build stamps: yyyy.MM.dd.HHmm.
    private static string ToDate(string title) =>
        DateTime.TryParseExact(title, "yyyy.MM.dd.HHmm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date.ToString("d MMMM yyyy", CultureInfo.InvariantCulture)
            : null;

    private static string ToHtml(string text)
    {
        var html = WebUtility.HtmlEncode(text);
        html = Bold().Replace(html, "<strong>$1</strong>");
        return Code().Replace(html, "<code>$1</code>");
    }

    [GeneratedRegex(@"\*\*(.+?)\*\*")]
    private static partial Regex Bold();

    [GeneratedRegex("`(.+?)`")]
    private static partial Regex Code();
}
