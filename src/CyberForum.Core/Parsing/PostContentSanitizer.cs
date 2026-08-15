using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace CyberForum.Core.Parsing;

/// <summary>
/// Перекладывает тело сообщения из десктопной вёрстки в компактную мобильную.
/// Собственно здесь «кошмар на телефоне» и превращается в читаемую страницу:
/// таблицы GeSHi становятся нормальными блоками кода, цитаты сворачиваются,
/// а всё, что расчитано на широкий экран, выкидывается.
/// </summary>
public sealed class PostContentSanitizer
{
    private static readonly string[] DroppedTags =
        ["script", "style", "iframe", "object", "embed", "form", "input", "button", "noscript"];

    private static readonly string[] DroppedAttributes =
        ["onclick", "onmouseover", "onmouseout", "onload", "onerror", "align", "border", "cellpadding", "cellspacing"];

    // длиннее этого цитату сворачиваем, иначе она занимает весь экран
    private const int CollapseQuoteAfter = 280;

    private readonly HtmlParser _parser = new();

    public string Sanitize(string? postHtml)
    {
        if (string.IsNullOrWhiteSpace(postHtml))
        {
            return string.Empty;
        }

        using var document = _parser.ParseDocument($"<body>{postHtml}</body>");
        var body = document.Body!;

        RewriteCodeBlocks(body);
        RewriteQuotes(body);
        RewriteImages(body);
        RewriteLinks(body);
        StripUnsafeNodes(body);

        return body.InnerHtml.Trim();
    }

    // Блок кода на форуме — это таблица GeSHi: слева колонка с номерами строк, справа сам код.
    // На телефоне номера съедают пол-ширины, поэтому колонку выкидываем, а подсветку
    // (span-ы с классами GeSHi) оставляем — раскрасим своей темой.
    private static void RewriteCodeBlocks(IElement root)
    {
        foreach (var block in root.QuerySelectorAll("div.codeblock").ToList())
        {
            var language = block.QuerySelector("td.head, .head").CleanText();
            var code = block.QuerySelector("td.de1 pre, pre.de1:not(.ln pre)");

            // номера строк лежат отдельной ячейкой, она нам не нужна
            foreach (var lineNumbers in block.QuerySelectorAll("td.ln").ToList())
            {
                lineNumbers.Remove();
            }

            code ??= block.QuerySelector("pre");

            var replacement = BuildCodeFigure(root.Owner!, language, code?.InnerHtml ?? block.CleanText());
            block.Replace(replacement);
        }

        // в RSS тот же код приходит попроще: div.printablecode с <code> внутри
        foreach (var block in root.QuerySelectorAll("div.printablecode").ToList())
        {
            var code = block.QuerySelector("code");
            var replacement = BuildCodeFigure(root.Owner!, null, code?.InnerHtml ?? block.CleanText());
            block.Replace(replacement);
        }
    }

    private static IElement BuildCodeFigure(IDocument document, string? language, string codeHtml)
    {
        var figure = document.CreateElement("figure");
        figure.ClassName = "cf-code";

        if (!string.IsNullOrWhiteSpace(language))
        {
            figure.SetAttribute("data-lang", language.Trim());
        }

        var pre = document.CreateElement("pre");
        var code = document.CreateElement("code");

        // отступы форум отдаёт неразрывными пробелами, в <pre> они только мешают копированию
        code.InnerHtml = codeHtml.Replace(' ', ' ');

        pre.AppendChild(code);
        figure.AppendChild(pre);

        return figure;
    }

    // Цитата на форуме — это вложенные таблицы с рамками. Делаем из неё обычный
    // blockquote, а длинную заворачиваем в details, чтоб не листать её насквозь.
    private static void RewriteQuotes(IElement root)
    {
        foreach (var quote in root.QuerySelectorAll("div.bbcode_quote, div.quotebox").ToList())
        {
            var document = root.Owner!;

            var author = quote.QuerySelector("div.bbcode_postedby")?.CleanText() ?? string.Empty;
            var link = quote.QuerySelector("div.bbcode_postedby a[href]")?.GetAttribute("href");
            var messageHtml = quote.QuerySelector("div.message")?.InnerHtml ?? quote.InnerHtml;
            var messageText = quote.QuerySelector("div.message").CleanText();

            var blockquote = document.CreateElement("blockquote");
            blockquote.ClassName = "cf-quote";

            var header = document.CreateElement("div");
            header.ClassName = "cf-quote-author";
            header.TextContent = author.Length > 0 ? author : "Цитата";

            if (link is not null)
            {
                header.SetAttribute("data-post-link", link);
            }

            var bodyElement = document.CreateElement("div");
            bodyElement.ClassName = "cf-quote-body";
            bodyElement.InnerHtml = messageHtml;

            if (messageText.Length > CollapseQuoteAfter)
            {
                var details = document.CreateElement("details");
                var summary = document.CreateElement("summary");
                summary.AppendChild(header);
                details.AppendChild(summary);
                details.AppendChild(bodyElement);
                blockquote.AppendChild(details);
            }
            else
            {
                blockquote.AppendChild(header);
                blockquote.AppendChild(bodyElement);
            }

            // цитата завёрнута в таблицу-контейнер, меняем её целиком
            var container = quote.Closest("table.bbcode_maincontainer") ?? quote;
            container.Replace(blockquote);
        }
    }

    private static void RewriteImages(IElement root)
    {
        foreach (var image in root.QuerySelectorAll("img").ToList())
        {
            var source = image.GetAttribute("src");
            var absolute = ForumUrls.Absolute(source);

            if (absolute is null)
            {
                image.Remove();
                continue;
            }

            image.SetAttribute("src", absolute.ToString());
            image.SetAttribute("loading", "lazy");

            var isSmiley = absolute.AbsolutePath.Contains("/smilies/", StringComparison.OrdinalIgnoreCase);
            image.ClassName = isSmiley ? "cf-smiley" : "cf-image";

            image.RemoveAttribute("width");
            image.RemoveAttribute("height");
        }
    }

    // Ссылки внутрь форума помечаем, чтобы оболочка открыла их своим экраном,
    // а не выкинула человека в браузер.
    private static void RewriteLinks(IElement root)
    {
        foreach (var anchor in root.QuerySelectorAll("a[href]").ToList())
        {
            var href = anchor.GetAttribute("href") ?? string.Empty;

            if (href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
            {
                anchor.Replace(anchor.Owner!.CreateTextNode(anchor.TextContent));
                continue;
            }

            var absolute = ForumUrls.Absolute(href);
            if (absolute is null)
            {
                continue;
            }

            anchor.SetAttribute("href", absolute.ToString());

            var location = ForumUrls.Parse(absolute.ToString());
            switch (location.Kind)
            {
                case ForumUrlKind.Thread when location.ThreadId is { } threadId:
                    anchor.SetAttribute("data-cf-thread", threadId.ToString());
                    anchor.SetAttribute("data-cf-slug", location.Slug ?? string.Empty);
                    anchor.SetAttribute("data-cf-page", location.Page.ToString());
                    break;
                case ForumUrlKind.Forum when location.Slug is { } slug:
                    anchor.SetAttribute("data-cf-forum", slug);
                    break;
                default:
                    anchor.SetAttribute("target", "_blank");
                    anchor.SetAttribute("rel", "noopener noreferrer");
                    break;
            }
        }
    }

    private static void StripUnsafeNodes(IElement root)
    {
        foreach (var tag in DroppedTags)
        {
            foreach (var element in root.QuerySelectorAll(tag).ToList())
            {
                element.Remove();
            }
        }

        foreach (var element in root.QuerySelectorAll("*").ToList())
        {
            foreach (var attribute in DroppedAttributes)
            {
                element.RemoveAttribute(attribute);
            }

            // инлайновые стили расчитаны на широкий экран: отступы по 30-40 пикселей,
            // фиксированные ширины и высоты. своя вёрстка справится лучше
            element.RemoveAttribute("style");
        }
    }
}
