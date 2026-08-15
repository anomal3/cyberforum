using CyberForum.Core.Parsing;

namespace CyberForum.Core.Tests;

public class SessionStateParserTests
{
    private readonly SessionStateParser _parser = new();

    [Fact]
    public void Recognises_signed_in_user()
    {
        var state = _parser.Parse(Fixture.Read("forum-python-auth.html"));

        Assert.True(state.IsAuthenticated);
        Assert.Equal("tester42", state.UserName);

        // свой номер нужен, чтобы понимать, где наши сообщения и записи блога
        Assert.Equal(100500, state.UserId);

        // Сам токен из эталонных страниц вычищен перед публикацией — им можно
        // было бы действовать от чужого имени. Проверяем, что он вообще нашёлся.
        Assert.False(string.IsNullOrWhiteSpace(state.SecurityToken));
    }

    [Fact]
    public void Recognises_guest()
    {
        var state = _parser.Parse(Fixture.Read("forum-python-guest.html"));

        Assert.False(state.IsAuthenticated);
        Assert.Null(state.UserName);
    }

    [Fact]
    public void Empty_page_is_not_authenticated()
    {
        var state = _parser.Parse(string.Empty);

        Assert.False(state.IsAuthenticated);
        Assert.Null(state.SecurityToken);
    }
}
