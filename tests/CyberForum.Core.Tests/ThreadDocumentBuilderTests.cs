using CyberForum.Core.Parsing;
using CyberForum.Core.Rendering;

namespace CyberForum.Core.Tests;

public class ThreadDocumentBuilderTests
{
    private readonly ThreadPageParser _threadParser = new();
    private readonly ThreadDocumentBuilder _builder = new(new PostContentSanitizer());

    private ThreadDocument Build()
    {
        var thread = _threadParser.Parse(Fixture.Read("thread-nosql-auth.html"));
        return _builder.Build(thread, ThreadStyles.Default);
    }

    [Fact]
    public void Document_contains_every_post_as_article()
    {
        var document = Build();

        var articles = document.Html.Split("<article class=\"cf-post").Length - 1;

        Assert.Equal(20, articles);
        Assert.Contains("<!doctype html>", document.Html);
        Assert.Contains("viewport", document.Html);
    }

    [Fact]
    public void Styles_are_inlined()
    {
        var document = Build();

        Assert.Contains(".cf-post", document.Html);
        Assert.Contains("prefers-color-scheme", document.Html);
    }

    [Fact]
    public void Code_blocks_get_a_bar_with_language_and_copy_button()
    {
        var document = Build();

        Assert.Contains("cf-code-bar", document.Html);
        Assert.Contains("Python", document.Html);
        Assert.Contains("class=\"cf-copy\" data-index=\"0\"", document.Html);
        Assert.NotEmpty(document.CodeSnippets);
        Assert.Contains("import", document.CodeSnippets[0]);
    }

    [Fact]
    public void Author_and_date_are_rendered()
    {
        var document = Build();

        Assert.Contains("Zloyalex100", document.Html);
        Assert.Contains("16.08.2025 16:56", document.Html);
        Assert.Contains("class=\"cf-num\">#1<", document.Html);
    }

    // Побочный, но полезный эффект: файл, который можно открыть браузером и посмотреть глазами
    [Fact]
    public void Preview_file_is_written_for_manual_check()
    {
        var document = Build();
        var path = Path.Combine(AppContext.BaseDirectory, "thread-preview.html");

        File.WriteAllText(path, document.Html);

        Assert.True(File.Exists(path));
    }
}
