using System.Globalization;
using System.Net;
using System.Text;
using AngleSharp.Html.Parser;
using CyberForum.Core.Models;
using CyberForum.Core.Parsing;

namespace CyberForum.Core.Rendering;

/// <summary>
/// Собирает из разобранной темы один готовый документ. Именно здесь тема перестаёт
/// быть таблицей на тысячу пикселей и превращается в ленту сообщений.
/// Вся тема идёт одним документом — по WebView на каждый пост это верная смерть скролла.
/// </summary>
public sealed class ThreadDocumentBuilder(PostContentSanitizer sanitizer)
{
    private static readonly CultureInfo Russian = CultureInfo.GetCultureInfo("ru-RU");

    private readonly HtmlParser _parser = new();

    public ThreadDocument Build(ThreadView thread, string styles)
    {
        var snippets = new List<string>();
        var body = new StringBuilder();

        foreach (var post in thread.Posts)
        {
            AppendPost(body, post, snippets);
        }

        var html = $$"""
                   <!doctype html>
                   <html lang="ru">
                   <head>
                   <meta charset="utf-8">
                   <meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover">
                   <style>{{styles}}</style>
                   </head>
                   <body>
                   {{body}}
                   <script>
                   document.addEventListener('click', function (e) {
                     var button = e.target.closest('.cf-copy');
                     if (!button) return;
                     e.preventDefault();
                     location.href = 'cfapp://copy/' + button.dataset.index;
                   });
                   </script>
                   </body>
                   </html>
                   """;

        return new ThreadDocument(html, snippets);
    }

    private void AppendPost(StringBuilder body, ForumPost post, List<string> snippets)
    {
        var content = DecorateCodeBlocks(sanitizer.Sanitize(post.ContentHtml), snippets);

        var avatar = post.AvatarUrl is null
            ? "<div class=\"cf-avatar\"></div>"
            : $"<img class=\"cf-avatar\" src=\"{WebUtility.HtmlEncode(post.AvatarUrl)}\" alt=\"\">";

        var when = post.PostedAt is { } date ? date.ToString("dd.MM.yyyy HH:mm", Russian) : string.Empty;
        var number = post.Number is { } n ? $"#{n}" : string.Empty;
        var best = post.IsBestAnswer ? " best" : string.Empty;
        var badge = post.IsBestAnswer ? "<div class=\"cf-badge\">Лучший ответ</div>" : string.Empty;

        body.Append($"""
                     <article class="cf-post{best}" id="post-{post.PostId}">
                       <div class="cf-head">
                         {avatar}
                         <div class="cf-who">
                           <span class="cf-author">{WebUtility.HtmlEncode(post.Author)}</span>
                           <span class="cf-when">{when}</span>
                         </div>
                         <span class="cf-num">{number}</span>
                       </div>
                       {badge}
                       <div class="cf-body">{content}</div>
                     </article>

                     """);
    }

    // Вешаем блокам кода шапку с языком и кнопкой копирования. Сам текст кода
    // запоминаем на своей стороне: буфер обмена в WebView капризный, лучше нативный.
    private string DecorateCodeBlocks(string html, List<string> snippets)
    {
        using var document = _parser.ParseDocument($"<body>{html}</body>");

        foreach (var figure in document.QuerySelectorAll("figure.cf-code"))
        {
            var code = figure.QuerySelector("code");
            if (code is null)
            {
                continue;
            }

            var index = snippets.Count;
            snippets.Add(code.TextContent);

            var language = figure.GetAttribute("data-lang") ?? "код";

            var bar = document.CreateElement("div");
            bar.ClassName = "cf-code-bar";
            bar.InnerHtml =
                $"<span>{WebUtility.HtmlEncode(language)}</span>" +
                $"<button class=\"cf-copy\" data-index=\"{index}\">копировать</button>";

            figure.InsertBefore(bar, figure.FirstChild);
        }

        return document.Body!.InnerHtml;
    }
}

// Готовый документ и тексты блоков кода из него — по порядку, как они встретились
public sealed record ThreadDocument(string Html, IReadOnlyList<string> CodeSnippets);
