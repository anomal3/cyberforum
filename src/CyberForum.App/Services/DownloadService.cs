using CyberForum.Core;

namespace CyberForum.App.Services;

/// <summary>
/// Качает картинки и вложения форума. Ходит нашим http-клиентом, а не системным:
/// у него куки, и вложения форум отдаёт только вошедшим.
/// </summary>
public sealed partial class DownloadService(ForumClient client)
{
    /// <summary>Скачивает и кладёт в загрузки. Возвращает имя файла или null.</summary>
    public async Task<string?> SaveAsync(string url, CancellationToken token = default)
    {
        var (bytes, name, type) = await FetchAsync(url, token);

        if (bytes.Length == 0)
        {
            return null;
        }

        return await StoreAsync(bytes, name, type, token) ? name : null;
    }

    private async Task<(byte[] Bytes, string Name, string Type)> FetchAsync(string url, CancellationToken token)
    {
        var address = url.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? new Uri(url)
            : new Uri(ForumUrls.Base, url);

        var answer = await client.Http.GetBytesAsync(address, token);

        return (answer.Bytes, NameFor(address, answer.FileName, answer.ContentType), answer.ContentType);
    }

    // имя берём из заголовка, а если его нет — из адреса; расширение чиним по типу
    private static string NameFor(Uri address, string? given, string type)
    {
        var name = given;

        if (string.IsNullOrWhiteSpace(name))
        {
            name = Path.GetFileName(address.LocalPath);
        }

        if (string.IsNullOrWhiteSpace(name) || !name.Contains('.'))
        {
            var stamp = address.Query.Length > 1 ? address.Query.GetHashCode().ToString("x8") : "file";

            name = $"cyberforum-{stamp}{Extension(type)}";
        }

        foreach (var bad in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(bad, '_');
        }

        return name;
    }

    private static string Extension(string type) => type switch
    {
        var value when value.Contains("png", StringComparison.OrdinalIgnoreCase) => ".png",
        var value when value.Contains("gif", StringComparison.OrdinalIgnoreCase) => ".gif",
        var value when value.Contains("webp", StringComparison.OrdinalIgnoreCase) => ".webp",
        var value when value.Contains("jpeg", StringComparison.OrdinalIgnoreCase) => ".jpg",
        var value when value.Contains("zip", StringComparison.OrdinalIgnoreCase) => ".zip",
        var value when value.Contains("pdf", StringComparison.OrdinalIgnoreCase) => ".pdf",
        var value when value.Contains("text", StringComparison.OrdinalIgnoreCase) => ".txt",
        _ => ".bin",
    };

    // складываем средствами платформы: на Android это общая папка «Загрузки»
    private partial Task<bool> StoreAsync(byte[] bytes, string name, string type, CancellationToken token);
}
