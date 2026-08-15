using CyberForum.Core.Models;
using CyberForum.Core.Parsing;

namespace CyberForum.Core.Tests;

public class MemberProfileParserTests
{
    private static MemberProfile Profile() =>
        new MemberProfileParser().Parse(Fixture.Read("member-tester42.html"));

    [Fact]
    public void Имя_и_номер_читаются()
    {
        var profile = Profile();

        Assert.Equal("tester42", profile.UserName);
        Assert.Equal(100500, profile.UserId);
        Assert.NotNull(profile.LastActivity);
    }

    [Fact]
    public void Мини_статистика_разбирается()
    {
        var stats = Profile().Stats;

        Assert.Contains(stats, field => field.Name == "Регистрация" && field.Value == "11.03.2013");
        Assert.Contains(stats, field => field.Name == "Всего сообщений" && field.Value == "610");
        Assert.Contains(stats, field => field.Name == "Репутация" && field.Value == "95");
    }

    // «Недоступно» — это не заполненное поле, тащить его в приложение незачем
    [Fact]
    public void Обо_мне_разбирается_без_пустых_полей()
    {
        var about = Profile().About;

        Assert.Contains(about, field => field.Name == "Реальное имя" && field.Value == "Иван Петров");
        Assert.DoesNotContain(about, field => field.Value == "Недоступно");
    }

    [Fact]
    public void Записи_блога_находятся()
    {
        var blog = Profile().Blog;

        Assert.NotEmpty(blog);
        Assert.Contains(blog, entry => entry.Title.Contains("Winforstrap", StringComparison.Ordinal));
    }
}
