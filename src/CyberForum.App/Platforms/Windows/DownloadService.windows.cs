namespace CyberForum.App.Services;

// На этой платформе кладём файл в личную папку приложения — системного места
// «Загрузки», как на Android, тут нет.
public sealed partial class DownloadService
{
    private partial async Task<bool> StoreAsync(byte[] bytes, string name, string type, CancellationToken token)
    {
        var path = Path.Combine(FileSystem.AppDataDirectory, name);

        await File.WriteAllBytesAsync(path, bytes, token);

        return true;
    }
}
