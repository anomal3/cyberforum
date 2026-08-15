using System.Globalization;

namespace CyberForum.App;

// «Пять минут назад» читается легче, чем дата с точками
public sealed class AgoConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var when = value switch
        {
            DateTimeOffset offset => offset.UtcDateTime,
            DateTime time => time.ToUniversalTime(),
            _ => (DateTime?)null,
        };

        if (when is null)
        {
            return string.Empty;
        }

        var passed = DateTime.UtcNow - when.Value;

        if (passed < TimeSpan.Zero)
        {
            passed = TimeSpan.Zero;
        }

        if (passed < TimeSpan.FromMinutes(1))
        {
            return "только что";
        }

        if (passed < TimeSpan.FromHours(1))
        {
            return Say((int)passed.TotalMinutes, "минуту", "минуты", "минут") + " назад";
        }

        if (passed < TimeSpan.FromDays(1))
        {
            return Say((int)passed.TotalHours, "час", "часа", "часов") + " назад";
        }

        if (passed < TimeSpan.FromDays(7))
        {
            return Say((int)passed.TotalDays, "день", "дня", "дней") + " назад";
        }

        return when.Value.ToLocalTime().ToString("dd.MM.yyyy", culture);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static string Say(int count, string one, string few, string many)
    {
        var hundred = count % 100;
        var last = count % 10;

        if (hundred is >= 11 and <= 14)
        {
            return $"{count} {many}";
        }

        return last switch
        {
            1 => $"{count} {one}",
            >= 2 and <= 4 => $"{count} {few}",
            _ => $"{count} {many}",
        };
    }
}

// Звезда в шапке темы: закрашена — тема в избранном, пустая — нет
public sealed class StarConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        ImageSource.FromFile(value is true ? "icon_star_on.png" : "icon_star.png");

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// обратное значение флага — чтобы показывать «Войти» ровно когда не вошли
public sealed class NotConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not true;
}
