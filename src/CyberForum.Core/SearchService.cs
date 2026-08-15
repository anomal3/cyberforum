using CyberForum.Core.Http;
using CyberForum.Core.Models;
using CyberForum.Core.Parsing;

namespace CyberForum.Core;

/// <summary>
/// Поиск по форуму. Родной поиск движка — это POST на search.php?do=process с токеном
/// формы, который надо сперва подсмотреть на любой странице. Гостю форум вдобавок
/// подсовывает reCAPTCHA, и без входа поиск не отработает — это не наша беда, а его
/// защита, поэтому такой ответ мы узнаём и говорим об этом человеку прямо.
/// </summary>
public sealed class SearchService(ForumHttpClient http)
{
    private readonly SessionStateParser _sessionParser = new();
    private readonly SearchResultParser _resultParser = new();

    public async Task<IReadOnlyList<ThreadSummary>> SearchAsync(
        string query,
        bool titlesOnly = true,
        CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var securityToken = await GetSecurityTokenAsync(token);

        var fields = new Dictionary<string, string>
        {
            ["do"] = "process",
            ["securitytoken"] = securityToken,
            ["query"] = query.Trim(),
            ["titleonly"] = titlesOnly ? "1" : "0",
            ["showposts"] = "0",
            ["childforums"] = "1",
            ["exactname"] = "1",
        };

        var body = await http.PostFormAsync(ForumUrls.SearchProcess(), fields, ForumUrls.Search(), token);

        var found = _resultParser.Parse(body);

        if (found.Count == 0 && NeedsHumanCheck(body))
        {
            throw new ForumCaptchaException();
        }

        return found;
    }

    // Вместо выдачи форум вернул форму поиска с проверкой «я не робот».
    private static bool NeedsHumanCheck(string body) =>
        body.Contains("humanverify", StringComparison.OrdinalIgnoreCase) ||
        body.Contains("g-recaptcha", StringComparison.OrdinalIgnoreCase);

    // токен лежит в скриптах любой страницы; у гостя он так и называется — guest
    private async Task<string> GetSecurityTokenAsync(CancellationToken token)
    {
        var html = await http.GetStringAsync(ForumUrls.Home(), token: token);

        return _sessionParser.Parse(html).SecurityToken ?? "guest";
    }
}
