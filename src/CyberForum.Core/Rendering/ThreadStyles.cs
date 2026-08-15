using System.Reflection;

namespace CyberForum.Core.Rendering;

// Стили ленты сообщений лежат ресурсом в сборке — так их видят и приложение, и тесты
public static class ThreadStyles
{
    private const string ResourceName = "CyberForum.Core.Rendering.thread.css";

    private static string? _cached;

    public static string Default => _cached ??= Read();

    private static string Read()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
                           ?? throw new InvalidOperationException($"Не нашёлся ресурс {ResourceName}");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
