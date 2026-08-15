namespace CyberForum.App.Services;

/// <summary>
/// Рубильники, которые хочется передвинуть в одном месте, а не искать по коду.
/// </summary>
public static class AppFeatures
{
    /// <summary>
    /// Фильтровать ли посторонние запросы страницы. Без фильтра чужие вставки
    /// перерисовывают документ под себя, и читалке нечего причёсывать.
    /// </summary>
    public const bool FilterRequests = true;
}
