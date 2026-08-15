using CyberForum.Core.Posting;

namespace CyberForum.Core.Tests;

public class ReplyFormParserTests
{
    private static ReplyForm Form() =>
        new ReplyFormParser().Parse(Fixture.Read("thread-nosql-auth.html"))!;

    [Fact]
    public void Форма_быстрого_ответа_находится_прямо_в_теме()
    {
        var form = Form();

        Assert.Contains("newreply.php", form.Action.ToString());
        Assert.Contains("do=postreply", form.Action.ToString());
        Assert.Equal(3212200, form.ThreadId);
    }

    [Fact]
    public void Одноразовые_поля_забираются_как_есть()
    {
        var fields = Form().Fields;

        Assert.Equal("7baa6720dcfec5fbaaaa5c62ec1e67b8", fields["posthash"]);
        Assert.Equal("1786734027", fields["poststarttime"]);
        Assert.Equal("100500", fields["loggedinuser"]);
        Assert.Equal("postreply", fields["do"]);
    }

    [Fact]
    public void Кнопки_и_невыбранные_галочки_в_поля_не_попадают()
    {
        var fields = Form().Fields;

        // «Показывать подпись» отмечена, а «Вставить цитату» и «Отключить смайлы» нет
        Assert.True(fields.ContainsKey("signature"));
        Assert.False(fields.ContainsKey("quickreply"));
        Assert.False(fields.ContainsKey("disablesmilies"));
        Assert.False(fields.ContainsKey("sbutton"));
        Assert.False(fields.ContainsKey("preview"));
    }

    [Fact]
    public void Окно_вложений_находится_вместе_с_ключами()
    {
        var address = Form().AttachmentUrl?.ToString();

        Assert.NotNull(address);
        Assert.Contains("newattachment.php", address);
        Assert.Contains("posthash=7baa6720dcfec5fbaaaa5c62ec1e67b8", address);
        Assert.Contains("poststarttime=1786734027", address);
    }

    [Fact]
    public void Без_ссылки_на_окно_вложений_адрес_собирается_из_полей()
    {
        // на живой странице ссылка спрятана в noscript, а браузер такое разметкой не считает
        var form = new ReplyForm
        {
            Action = ForumUrls.PostReply(3212200),
            Fields = new Dictionary<string, string>
            {
                ["t"] = "3212200",
                ["posthash"] = "abc",
                ["poststarttime"] = "1786734027",
                ["securitytoken"] = "тут-токен",
            },
        };

        var address = form.AttachmentWindow?.ToString();

        Assert.Equal(
            "https://www.cyberforum.ru/newattachment.php?t=3212200&poststarttime=1786734027&posthash=abc",
            address);
    }

    [Fact]
    public void Ошибку_из_ajax_ответа_видно()
    {
        var answer = "<?xml version=\"1.0\"?><root><error>Вы должны подождать 30 секунд.</error></root>";

        Assert.Equal("Вы должны подождать 30 секунд.", ReplyFormParser.ReadError(answer));
        Assert.Null(ReplyFormParser.ReadError("<root><postbit postid=\"1\"/></root>"));
    }

    [Fact]
    public void Принятый_ответ_отличаем_от_непонятного()
    {
        Assert.True(ReplyFormParser.LooksAccepted("<root><postbit postid=\"7\"><time>1</time></postbit></root>"));
        Assert.True(ReplyFormParser.LooksAccepted("<meta http-equiv=\"Refresh\" content=\"1; url=/python/thread1.html\">"));
        Assert.False(ReplyFormParser.LooksAccepted("<html><body>здрасьте</body></html>"));
    }

    [Fact]
    public void Номер_вложения_берём_последний()
    {
        var answer = "<a href=\"attachment.php?attachmentid=111&stc=1\">a.png</a>" +
                     "<a href=\"attachment.php?attachmentid=222&stc=1\">b.png</a>";

        Assert.Equal(222, ReplyFormParser.ReadAttachmentId(answer));
        Assert.Null(ReplyFormParser.ReadAttachmentId("ничего"));
    }

    [Fact]
    public void Блок_кода_оборачивается_тегом_языка()
    {
        var language = CodeLanguages.All.Single(item => item.Tag == "csharp");

        Assert.Equal("[csharp]\nvar x = 1;\n[/csharp]\n", CodeLanguages.Wrap(language, "  var x = 1;  "));
    }

    [Fact]
    public void Форма_выбирается_по_полю_ввода_а_не_по_адресу()
    {
        /* На странице правки записи блога первой идёт форма удаления — если брать
           её, человек нажмёт «Отправить» и лишится записи. */
        var page = """
            <html><body>
            <form action="blog_post.php?do=deleteblog&b=9921" method="post">
              <input type="hidden" name="do" value="deleteblog" />
              <input type="hidden" name="b" value="9921" />
            </form>
            <form action="blog_post.php?do=updateblog" method="post">
              <input type="hidden" name="do" value="updateblog" />
              <input type="hidden" name="posthash" value="ключ" />
              <input type="hidden" name="securitytoken" value="токен" />
              <input type="text" name="title" value="Заголовок" />
              <textarea name="message">Текст записи</textarea>
            </form>
            </body></html>
            """;

        var form = new ReplyFormParser().Parse(page);

        Assert.NotNull(form);
        Assert.Contains("do=updateblog", form.Action.ToString());
        Assert.Equal("Текст записи", form.Message);
        Assert.Equal("Заголовок", form.EntryTitle);
    }
}
