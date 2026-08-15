using System.Security.Cryptography;
using System.Text;
using AngleSharp.Html.Parser;
using CyberForum.Core.Http;

namespace CyberForum.Core;

/// <summary>
/// Вход обычной формой, без показа человеку десктопной страницы форума. Движок
/// принимает пароль хэшем — так его делает и сам сайт, открытым текстом он не ходит.
/// Проверку «я не робот» форум на странице рисует, но при входе не спрашивает,
/// поэтому просто передаём её hash из формы как есть.
/// </summary>
public sealed class LoginService(ForumHttpClient http)
{
    private readonly HtmlParser _parser = new();

    public async Task<LoginResult> SignInAsync(string user, string password, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrEmpty(password))
        {
            return new LoginResult(false, "Введи имя пользователя и пароль.");
        }

        var page = await http.GetStringAsync(ForumUrls.Login(), token: token);
        var fields = ReadForm(page);

        var hash = Hash(password);

        fields["do"] = "login";
        fields["url"] = "/";
        fields["cookieuser"] = "1";
        fields["vb_login_username"] = user.Trim();
        fields["vb_login_md5password"] = hash;
        fields["vb_login_md5password_utf"] = hash;

        // сам пароль открытым текстом не отправляем — движку хватает хэша
        fields.Remove("vb_login_password");

        var answer = await http.PostFormAsync(ForumUrls.LoginPost(), fields, ForumUrls.Login(), token);

        if (LooksLikeWrongPassword(answer))
        {
            return new LoginResult(false, "Форум не принял имя пользователя или пароль.");
        }

        if (LooksLikeHumanCheck(answer))
        {
            return new LoginResult(false, "Форум просит пройти проверку «я не робот» — войди через его страницу.");
        }

        return new LoginResult(true, null);
    }

    // забираем из формы всё, что она несёт сама: номер сессии, токен и hash проверки
    private Dictionary<string, string> ReadForm(string html)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);

        using var document = _parser.ParseDocument(html);

        var form = document.QuerySelector("form[action*='do=login']");

        if (form is null)
        {
            return fields;
        }

        foreach (var input in form.QuerySelectorAll("input[name]"))
        {
            var name = input.GetAttribute("name");

            if (!string.IsNullOrEmpty(name))
            {
                fields[name] = input.GetAttribute("value") ?? string.Empty;
            }
        }

        return fields;
    }

    private static bool LooksLikeWrongPassword(string answer) =>
        answer.Contains("неверн", StringComparison.OrdinalIgnoreCase) ||
        answer.Contains("неправильн", StringComparison.OrdinalIgnoreCase) ||
        answer.Contains("не совпада", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeHumanCheck(string answer) =>
        answer.Contains("g-recaptcha", StringComparison.OrdinalIgnoreCase) &&
        answer.Contains("humanverify", StringComparison.OrdinalIgnoreCase);

    private static string Hash(string value)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(value));

        return Convert.ToHexStringLower(bytes);
    }
}

public sealed record LoginResult(bool Ok, string? Message);
