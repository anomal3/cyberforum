using Android.Content;
using Android.OS;
using Android.Provider;

namespace CyberForum.App.Services;

public sealed partial class DownloadService
{
    private partial async Task<bool> StoreAsync(byte[] bytes, string name, string type, CancellationToken token)
    {
        var resolver = Platform.CurrentActivity?.ContentResolver;

        if (resolver is null)
        {
            return false;
        }

        var values = new ContentValues();

        values.Put(MediaStore.IMediaColumns.DisplayName, name);
        values.Put(MediaStore.IMediaColumns.MimeType, string.IsNullOrEmpty(type) ? "application/octet-stream" : type);

        // Картинки кладём в галерею, остальное — в «Загрузки». Разрешений на это
        // не нужно: пишем через MediaStore, а он сам решает вопрос доступа.
        var target = type.StartsWith("image", StringComparison.OrdinalIgnoreCase)
            ? MediaStore.Images.Media.GetContentUri(MediaStore.VolumeExternalPrimary)
            : MediaStore.Downloads.GetContentUri(MediaStore.VolumeExternalPrimary);

        if (type.StartsWith("image", StringComparison.OrdinalIgnoreCase))
        {
            values.Put(MediaStore.IMediaColumns.RelativePath, global::Android.OS.Environment.DirectoryPictures + "/Киберфорум");
        }
        else
        {
            values.Put(MediaStore.IMediaColumns.RelativePath, global::Android.OS.Environment.DirectoryDownloads + "/Киберфорум");
        }

        var uri = resolver.Insert(target!, values);

        if (uri is null)
        {
            return false;
        }

        await using var stream = resolver.OpenOutputStream(uri);

        if (stream is null)
        {
            return false;
        }

        await stream.WriteAsync(bytes, token);
        await stream.FlushAsync(token);

        return true;
    }
}
