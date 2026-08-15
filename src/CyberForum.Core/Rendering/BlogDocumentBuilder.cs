using System.Net;
using System.Text;
using CyberForum.Core.Models;
using CyberForum.Core.Parsing;

namespace CyberForum.Core.Rendering;

/// <summary>
/// Собирает запись блога в тот же читаемый документ, что и тему: свои стили,
/// нормальные блоки кода, никакой чужой вёрстки.
/// </summary>
public sealed class BlogDocumentBuilder(PostContentSanitizer sanitizer)
{
    public string Build(BlogPost post, string styles)
    {
        ArgumentNullException.ThrowIfNull(post);

        var html = new StringBuilder();

        html.Append("<!doctype html><html lang=\"ru\"><head><meta charset=\"utf-8\">")
            .Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1, viewport-fit=cover\">")
            .Append("<style>").Append(styles).Append("</style></head><body><div id=\"cf-reader\">");

        html.Append("<h1 class=\"cf-title\">").Append(Escape(post.Title)).Append("</h1>");

        html.Append("<article class=\"cf-post\"><div class=\"cf-head\"><div class=\"cf-avatar\"></div>")
            .Append("<div class=\"cf-who\"><span class=\"cf-author\">").Append(Escape(post.Author ?? string.Empty))
            .Append("</span><span class=\"cf-when\">").Append(Escape(post.When ?? string.Empty))
            .Append("</span></div></div>");

        html.Append("<div class=\"cf-body\">").Append(sanitizer.Sanitize(post.BodyHtml)).Append("</div>");

        if (post.Tags.Count > 0)
        {
            html.Append("<div class=\"cf-tags\">");

            foreach (var tag in post.Tags)
            {
                html.Append("<span class=\"cf-tag\">").Append(Escape(tag)).Append("</span>");
            }

            html.Append("</div>");
        }

        html.Append("</article></div>");

        // тот же уговор, что и в читалке тем: по картинке зовём просмотрщик,
        // по вложению — качалку. Своими силами webview ни то, ни другое не умеет.
        html.Append("<script>(function(){")
            .Append("var i=document.querySelectorAll('.cf-body img');")
            .Append("for(var n=0;n<i.length;n++){i[n].className='cf-image';i[n].style.cursor='zoom-in';")
            .Append("i[n].addEventListener('click',function(){location.href='cfimage:'+encodeURIComponent(this.src);});}")
            .Append("var f=document.querySelectorAll('a[href*=\"attachment.php\"],a[href*=\"/attachments/\"]');")
            .Append("for(var k=0;k<f.length;k++){f[k].addEventListener('click',function(e){e.preventDefault();")
            .Append("location.href='cffile:'+encodeURIComponent(this.href);});}")
            .Append("})();</script>");

        html.Append("</body></html>");

        return html.ToString();
    }

    private static string Escape(string value) => WebUtility.HtmlEncode(value);
}
