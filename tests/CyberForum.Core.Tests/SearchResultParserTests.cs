using CyberForum.Core.Parsing;

namespace CyberForum.Core.Tests;

public class SearchResultParserTests
{
    // Гостю форум вместо выдачи возвращает ту же форму поиска с reCAPTCHA.
    // Тем на такой странице нет — и парсер не должен придумывать их из шапки и меню.
    [Fact]
    public void Форма_поиска_гостя_не_считается_выдачей()
    {
        var html = Fixture.Read("search-form-guest.html");

        var found = new SearchResultParser().Parse(html);

        Assert.Empty(found);
    }

    [Fact]
    public void Форма_поиска_гостя_просит_проверку_на_робота()
    {
        var html = Fixture.Read("search-form-guest.html");

        Assert.Contains("humanverify", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("g-recaptcha", html, StringComparison.OrdinalIgnoreCase);
    }
}
