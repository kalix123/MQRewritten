using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Server.Base.Core.Configs;
using Web.Razor.Services;

namespace Web.Razor.Pages;

/// <summary>The published changelog as an RSS feed, one item per client version. Text is plain, so it reads well in Discord.</summary>
public partial class ChangelogFeedModel(InternalRwConfig config, PagesService pService) : PageModel
{
    private const int MaxItems = 20;

    public ContentResult OnGet()
    {
        var link = $"{pService.SiteUrl}/changelog";

        var channel = new XElement("channel",
            new XElement("title", $"{config.ServerName} Changelog"),
            new XElement("link", link),
            new XElement("description", $"New versions of the {config.ServerName} game client."),
            new XElement("language", "en"));

        foreach (var release in ChangelogRelease.Read(pService).Where(r => !r.IsUnreleased).Take(MaxItems))
        {
            var item = new XElement("item",
                new XElement("title", $"Update {release.Title}"),
                new XElement("link", link),
                new XElement("guid", new XAttribute("isPermaLink", "false"), $"{config.ServerName}-{release.Title}"),
                new XElement("description", ToText(release)));

            // Versions are UTC build stamps: yyyy.MM.dd.HHmm.
            if (DateTime.TryParseExact(release.Title, "yyyy.MM.dd.HHmm", CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var published))
                item.Add(new XElement("pubDate", published.ToString("R", CultureInfo.InvariantCulture)));

            channel.Add(item);
        }

        var feed = new XDocument(new XDeclaration("1.0", "utf-8", null), new XElement("rss", new XAttribute("version", "2.0"), channel));
        return Content(feed.Declaration + Environment.NewLine + feed, "application/rss+xml; charset=utf-8");
    }

    private static string ToText(ChangelogRelease release) =>
        string.Join("\n", release.Intro.Select(Plain).Concat(release.Items.Select(i => "• " + Plain(i))));

    private static string Plain(string html) => WebUtility.HtmlDecode(Tag().Replace(html, string.Empty));

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tag();
}
