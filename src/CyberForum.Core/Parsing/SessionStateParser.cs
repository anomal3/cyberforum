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
        var userId = MemberIdRegex().Match(html);

        return new SessionState
        {
            IsAuthenticated = authenticated,
            UserName = authenticated && userName.Success ? userName.Groups[1].Value : null,
            UserId = authenticated && userId.Success ? int.Parse(userId.Groups[1].Value) : null,
            SecurityToken = token.Success ? token.Groups[1].Value : null,
        };
    }

    [GeneratedRegex(@"SECURITYTOKEN\s*=\s*""([^""]+)""")]
    private static partial Regex SecurityTokenRegex();

    [GeneratedRegex(@"do=logout", RegexOptions.IgnoreCase)]
    private static partial Regex LogoutRegex();

    // имя вылезает в ссылках «Мои темы»: search.php?...&searchuser=tester42
    [GeneratedRegex(@"searchuser=([^&""'\s]+)", RegexOptions.IgnoreCase)]
    private static partial Regex UserNameRegex();

    [GeneratedRegex(@"/members/(\d+)\.html""[^>]*>\s*(?:<[^>]+>\s*)?(?:Профиль|профиль)", RegexOptions.IgnoreCase)]
    private static partial Regex MemberIdRegex();
}
