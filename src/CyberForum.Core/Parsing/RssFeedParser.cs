using System.Globalization;
using System.Xml.Linq;
using CyberForum.Core.Models;

namespace CyberForum.Core.Parsing;

/// <summary>
/// RSS-лента форума. Ценна тем, что отдаёт первое сообщение целиком в content:encoded
/// и весит вчетверо меньше страницы раздела — на мобильном трафике разница заметная.
/// </summary>
public sealed class RssFeedParser
{
    private static readonly XNamespace Content = "http://purl.org/rss/1.0/modules/content/";
    private static readonly XNamespace DublinCore = "http://purl.org/dc/elements/1.1/";

    public IReadOnlyList<FeedItem> Parse(string xml)
    {
        ArgumentNullException.ThrowIfNull(xml);

        XDocument document;
        try
        {
            document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        }
        catch (System.Xml.XmlException)
        {
            return [];
        }

        var items = new List<FeedItem>();

        foreach (var item in document.Descendants("item"))
        {
            var link = (string?)item.Element("link");
            var threadId = ForumUrls.ThreadIdFromUrl(link);

            if (link is null || threadId is null)
            {
                continue;
            }

            var category = item.Element("category");

            items.Add(new FeedItem
            {
                ThreadId = threadId.Value,
                Title = ParsingHelpers.CleanText((string?)item.Element("title")),
                Link = link,
                ForumSlug = ForumUrls.Parse((string?)category?.Attribute("domain") ?? string.Empty).Slug,
                ForumTitle = ParsingHelpers.CleanText((string?)category),
                Author = ParsingHelpers.CleanText((string?)item.Element(DublinCore + "creator")),
                Summary = ParsingHelpers.CleanText((string?)item.Element("description")),
                ContentHtml = (string?)item.Element(Content + "encoded"),
                PublishedAt = ParseDate((string?)item.Element("pubDate")),
            });
        }

        return items;
    }

    private static DateTimeOffset? ParseDate(string? value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : null;
}
