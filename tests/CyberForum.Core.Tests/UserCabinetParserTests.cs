using CyberForum.Core.Models;
using CyberForum.Core.Parsing;

namespace CyberForum.Core.Tests;

public class UserCabinetParserTests
{
    private static UserCabinet Cabinet() =>
        new UserCabinetParser().Parse(Fixture.Read("usercp.html"));

    [Fact]
    public void Счётчики_сверху_читаются()
    {
        var cabinet = Cabinet();

        Assert.Equal(1, cabinet.Notifications);
        Assert.Equal(0, cabinet.NewMessages);
        Assert.Equal(102, cabinet.TotalMessages);
        Assert.Equal(95, cabinet.ReputationTotal);
        Assert.Equal(22, cabinet.BestAnswersTotal);
    }

    [Fact]
    public void Отмеченные_ответы_разбираются()
    {
        var answers = Cabinet().BestAnswers;

        Assert.NotEmpty(answers);

        var first = answers[0];

        Assert.Equal("Вывод информации с формы в текстовый файл", first.Title);
        Assert.Equal("windows-forms", first.ForumSlug);
        Assert.Equal(3109202, first.ThreadId);
        Assert.Equal(16922028, first.PostId);
        Assert.Equal("C# Windows Forms", first.ForumTitle);
        Assert.NotNull(first.At);
    }

    [Fact]
    public void Закладки_сообщений_разбираются()
    {
        var bookmarks = Cabinet().Bookmarks;

        Assert.NotEmpty(bookmarks);

        var first = bookmarks[0];

        Assert.Contains("Опросить несколько сетевых устройств", first.ThreadTitle);
        Assert.Equal("csharp-beginners", first.ForumSlug);
        Assert.Equal(1644253, first.ThreadId);
        Assert.Equal(8664967, first.PostId);
        Assert.Equal("OwenGlendower", first.Author);
    }

    [Fact]
    public void Отзывы_разбираются()
    {
        var notes = Cabinet().Reputation;

        Assert.NotEmpty(notes);

        var first = notes[0];

        Assert.Equal("Как отловить глобальные события нажатия клавиш?", first.ThreadTitle);
        Assert.Equal("Spoi", first.Author);
        Assert.Equal("Оценка сообщения", first.Comment);
        Assert.Equal(2276346, first.ThreadId);
        Assert.NotNull(first.At);
    }

    [Fact]
    public void Список_уведомлений_разбирается()
    {
        var notices = Cabinet().Notices;

        Assert.NotEmpty(notices);
        Assert.Contains(notices, notice => notice.Title == "Упоминания в темах" && notice.Count == 1);
        Assert.Contains(notices, notice => notice.Title.StartsWith("Непрочитанные личные", StringComparison.Ordinal));
    }

    [Fact]
    public void Свой_номер_пользователя_находится()
    {
        Assert.Equal(100500, Cabinet().UserId);
    }
}
