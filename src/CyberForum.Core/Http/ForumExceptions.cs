namespace CyberForum.Core.Http;

// Форум ответил заглушкой «Нет доступа». Это не наша поломка: так он встречает
// запросы с серверных адресов и всё, что похоже на массовый обход.
public sealed class ForumBlockedException(string message, Uri? uri = null) : Exception(message)
{
    public Uri? Uri { get; } = uri;
}

// Сеть отвалилась или форум не ответил за отведённое время
public sealed class ForumUnavailableException(string message, Exception? inner = null)
    : Exception(message, inner);

// Скачанный файл: содержимое, имя из заголовка и тип
public sealed record FileAnswer(byte[] Bytes, string? FileName, string ContentType);

// Форум просит пройти «я не робот». Так он закрывает от гостей поиск и вход —
// пройти это можно только руками в настоящем браузере.
public sealed class ForumCaptchaException()
    : Exception("Форум просит пройти проверку «я не робот»");
