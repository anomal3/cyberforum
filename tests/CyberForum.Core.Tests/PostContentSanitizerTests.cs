using CyberForum.Core.Parsing;

namespace CyberForum.Core.Tests;

public class PostContentSanitizerTests
{
    private readonly ThreadPageParser _threadParser = new();
    private readonly PostContentSanitizer _sanitizer = new();

    private string SanitizePost(int postId)
    {
        var thread = _threadParser.Parse(Fixture.Read("thread-nosql-auth.html"));
        var post = thread.Posts.Single(p => p.PostId == postId);

        return _sanitizer.Sanitize(post.ContentHtml);
    }

    [Fact]
    public void Code_block_becomes_plain_figure_with_language()
    {
        var html = SanitizePost(17627670);

        Assert.Contains("<figure class=\"cf-code\"", html);
        Assert.Contains("data-lang=\"Python\"", html);
        Assert.Contains("<pre><code>", html);
        Assert.Contains("import", html);
    }

    [Fact]
    public void Code_block_drops_line_numbers_and_geshi_tables()
    {
        var html = SanitizePost(17627670);

        Assert.DoesNotContain("class=\"ln\"", html);
        Assert.DoesNotContain("codeframe", html);
        Assert.DoesNotContain("<table", html);
    }

    [Fact]
    public void Quote_becomes_blockquote_with_author()
    {
        var html = SanitizePost(17648021);

        Assert.Contains("<blockquote class=\"cf-quote\"", html);
        Assert.Contains("cf-quote-author", html);
        Assert.Contains("ViachaslauK", html);
        Assert.DoesNotContain("bbcode_maincontainer", html);
    }

    [Fact]
    public void Inline_styles_and_scripts_are_stripped()
    {
        var thread = _threadParser.Parse(Fixture.Read("thread-nosql-auth.html"));

        foreach (var post in thread.Posts)
        {
            var html = _sanitizer.Sanitize(post.ContentHtml);

            Assert.DoesNotContain("<script", html);
            Assert.DoesNotContain("style=", html);
            Assert.DoesNotContain("onmouseover", html);
        }
    }

    [Fact]
    public void Internal_thread_links_are_marked_for_native_navigation()
    {
        var html = _sanitizer.Sanitize(
            """<a href="https://www.cyberforum.ru/python/thread3212200-page2.html">тема</a>""");

        Assert.Contains("data-cf-thread=\"3212200\"", html);
        Assert.Contains("data-cf-slug=\"python\"", html);
        Assert.Contains("data-cf-page=\"2\"", html);
    }

    [Fact]
    public void External_links_open_outside()
    {
        var html = _sanitizer.Sanitize("""<a href="https://example.com/x">там</a>""");

        Assert.Contains("target=\"_blank\"", html);
        Assert.Contains("rel=\"noopener noreferrer\"", html);
    }

    [Fact]
    public void Javascript_links_lose_their_href()
    {
        var html = _sanitizer.Sanitize("""<a href="javascript:do_qrpos(1);">Цитата</a>""");

        Assert.DoesNotContain("javascript:", html);
        Assert.Contains("Цитата", html);
    }

    [Fact]
    public void Images_become_absolute_and_smileys_are_separated()
    {
        var html = _sanitizer.Sanitize(
            """<img src="//cyberstatic.net/images/smilies/smile.gif"><img src="/attachments/pic.png">""");

        Assert.Contains("https://cyberstatic.net/images/smilies/smile.gif", html);
        Assert.Contains("cf-smiley", html);
        Assert.Contains("https://www.cyberforum.ru/attachments/pic.png", html);
        Assert.Contains("cf-image", html);
    }

    [Fact]
    public void Empty_input_stays_empty()
    {
        Assert.Equal(string.Empty, _sanitizer.Sanitize(null));
        Assert.Equal(string.Empty, _sanitizer.Sanitize("   "));
    }
}
