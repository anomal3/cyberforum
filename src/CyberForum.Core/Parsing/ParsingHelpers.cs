using System.Globalization;
using System.Text.RegularExpressions;
using AngleSharp.Dom;

namespace CyberForum.Core.Parsing;

// Мелкие разборщики значений, общие для всех парсеров
public static partial class ParsingHelpers
{
    // время на форуме московское — гостю движок отдаёт именно его
    public static readonly TimeSpan ForumOffset = TimeSpan.FromHours(3);

    // числа приходят с разделителями тысяч: 4,374 или 4 374
    public static int ParseCount(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        var digits = DigitsRegex().Replace(text, string.Empty);
        return int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
    }

    // тащим числовой хвост из id вида thread_title_3050934
    public static int? IdSuffix(string? elementId)
    {
        if (string.IsNullOrEmpty(elementId))
        {
            return null;
        }

        var match = TrailingNumberRegex().Match(elementId);
        return match.Success ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : null;
    }

    // Дата бывает обычной (29.04.2026 + 16:47), а бывает «Сегодня»/«Вчера» —
    // vBulletin так подменяет свежие даты, и это надо разворачивать обратно.
    public static DateTimeOffset? ParseDateTime(string? date, string? time, DateTimeOffset? now = null)
    {
        if (string.IsNullOrWhiteSpace(date))
        {
            return null;
        }

        date = date.Trim().Trim(',').Trim();
        var reference = now ?? DateTimeOffset.UtcNow.ToOffset(ForumOffset);

        DateOnly day;
        if (date.StartsWith("Сегодня", StringComparison.OrdinalIgnoreCase))
        {
            day = DateOnly.FromDateTime(reference.Date);
        }
        else if (date.StartsWith("Вчера", StringComparison.OrdinalIgnoreCase))
        {
            day = DateOnly.FromDateTime(reference.Date.AddDays(-1));
        }
        else if (!DateOnly.TryParseExact(date, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out day))
        {
            return null;
        }

        var clock = new TimeOnly(0, 0);
        if (!string.IsNullOrWhiteSpace(time))
        {
            var timeMatch = TimeRegex().Match(time);
            if (timeMatch.Success)
            {
                clock = new TimeOnly(
                    int.Parse(timeMatch.Groups[1].Value, CultureInfo.InvariantCulture),
                    int.Parse(timeMatch.Groups[2].Value, CultureInfo.InvariantCulture));
            }
        }

        return new DateTimeOffset(day.ToDateTime(clock), ForumOffset);
    }

    // текст элемента без лишних и неразрывных пробелов
    public static string CleanText(this IElement? element) =>
        element is null ? string.Empty : CleanText(element.TextContent);

    public static string CleanText(string? text) =>
        string.IsNullOrEmpty(text)
            ? string.Empty
            : WhitespaceRegex().Replace(text.Replace(' ', ' '), " ").Trim();

    [GeneratedRegex(@"[^\d]")]
    private static partial Regex DigitsRegex();

    [GeneratedRegex(@"(\d+)$")]
    private static partial Regex TrailingNumberRegex();

    [GeneratedRegex(@"(\d{1,2}):(\d{2})")]
    private static partial Regex TimeRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
