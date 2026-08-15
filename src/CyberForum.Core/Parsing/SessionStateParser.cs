using System.Text.RegularExpressions;
using CyberForum.Core.Models;

namespace CyberForum.Core.Parsing;

// Понимает по любой странице форума, вошли мы или смотрим гостем.
// Признак входа — ссылка выхода с logouthash, у гостя её просто нет.
public sealed partial class SessionStateParser
{
    public SessionState Parse(string html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return new SessionState();
        }

        var token = SecurityTokenRegex().Match(html);
        var authenticated = LogoutRegex().IsMatch(html);
        var userName = UserNameRegex().Match(html);
        var name = authenticated && userName.Success ? userName.Groups[1].Value : null;

        return new SessionState
        {
            IsAuthenticated = authenticated,
            UserName = name,
            UserId = authenticated ? FindUserId(html, name) : null,
            SecurityToken = token.Success ? token.Groups[1].Value : null,
        };
    }

    /* Свой номер ищем двумя способами. Сперва по ссылке «Мой профиль» в меню, а если
       её нет — по ссылке, подписанной нашим же именем: форум здоровается с человеком
       в шапке и ставит там ссылку на его страницу. */
    private static int? FindUserId(string html, string? name)
    {
        var menu = MemberIdRegex().Match(html);

        if (menu.Success)
        {
            return int.Parse(menu.Groups[1].Value);
        }

        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        var greeting = Regex.Match(
            html,
            $@"/members/(\d+)\.html""[^>]*>\s*{Regex.Escape(name)}\s*<",
            RegexOptions.IgnoreCase);

        return greeting.Success ? int.Parse(greeting.Groups[1].Value) : null;
    }

    [GeneratedRegex(@"SECURITYTOKEN\s*=\s*""([^""]+)""")]
    private static partial Regex SecurityTokenRegex();

    [GeneratedRegex(@"do=logout", RegexOptions.IgnoreCase)]
    private static partial Regex LogoutRegex();

    // имя вылезает в ссылках «Мои темы»: search.php?...&searchuser=tester42
    [GeneratedRegex(@"searchuser=([^&""'\s]+)", RegexOptions.IgnoreCase)]
    private static partial Regex UserNameRegex();

    // в меню пользователя пункт называется «Мой профиль», а раньше был просто «Профиль»
    [GeneratedRegex(@"/members/(\d+)\.html""[^>]*>\s*(?:<[^>]+>\s*)?(?:Мой\s+)?(?:Профиль|профиль)", RegexOptions.IgnoreCase)]
    private static partial Regex MemberIdRegex();
}
