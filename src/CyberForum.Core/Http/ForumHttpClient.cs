using System.Net;
using System.Text;

namespace CyberForum.Core.Http;

/// <summary>
/// Транспорт до форума. Ведёт себя как один живой читатель с телефона: не больше пары
/// запросов разом, с паузой между ними, с браузерными заголовками и общими куками.
/// Массово молотить страницы отсюда нельзя — форум за это отвечает заглушкой.
/// </summary>
public sealed class ForumHttpClient : IDisposable
{
    // Десктопным клиентам форум отдаёт больше данных в списках тем (просмотры, колонки),
    // поэтому представляемся десктопом, хотя сами живём в телефоне.
    private const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/139.0.0.0 Safari/537.36";

    private static readonly TimeSpan MinimalGap = TimeSpan.FromMilliseconds(350);

    private readonly HttpClient _client;
    private readonly CookieContainer _cookies;
    private readonly SemaphoreSlim _gate = new(2, 2);
    private readonly bool _ownsClient;

    private DateTimeOffset _lastRequest = DateTimeOffset.MinValue;

    public ForumHttpClient(HttpMessageHandler? handler = null, CookieContainer? cookies = null)
    {
        _cookies = cookies ?? new CookieContainer();

        if (handler is null)
        {
            handler = new HttpClientHandler
            {
                CookieContainer = _cookies,
                UseCookies = true,
                AutomaticDecompression = DecompressionMethods.All,
                AllowAutoRedirect = true,
            };
            _ownsClient = true;
        }

        // Пятнадцати секунд хватает: если форум решил не отвечать, он не ответит и за минуту,
        // а человек всё это время смотрит в пустой экран.
        _client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(15),
            DefaultRequestVersion = HttpVersion.Version20,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower,
        };

        _client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        _client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("ru-RU,ru;q=0.9,en;q=0.8");
        _client.DefaultRequestHeaders.Accept.ParseAdd(
            "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
    }

    public CookieContainer Cookies => _cookies;

    // Кладём куки, снятые из WebView после входа
    public void ApplyCookies(IEnumerable<Cookie> cookies)
    {
        foreach (var cookie in cookies)
        {
            _cookies.Add(ForumUrls.Base, cookie);
        }
    }

    public async Task<string> GetStringAsync(Uri uri, Uri? referer = null, CancellationToken token = default)
    {
        await _gate.WaitAsync(token);

        try
        {
            await WaitTurnAsync(token);

            using var request = new HttpRequestMessage(HttpMethod.Get, uri);

            if (referer is not null)
            {
                request.Headers.Referrer = referer;
            }

            using var response = await SendWithRetryAsync(request, token);

            var body = await response.Content.ReadAsStringAsync(token);

            if (response.StatusCode == HttpStatusCode.Forbidden || LooksLikeBlockPage(body))
            {
                throw new ForumBlockedException(
                    "Форум ответил «Нет доступа» — так он встречает запросы, похожие на роботов.", uri);
            }

            response.EnsureSuccessStatusCode();
            return body;
        }
        catch (HttpRequestException error)
        {
            throw new ForumUnavailableException("Не получилось достучаться до форума.", error);
        }
        catch (TaskCanceledException error) when (!token.IsCancellationRequested)
        {
            throw new ForumUnavailableException("Форум не ответил вовремя.", error);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Забирает файл целиком: картинку из сообщения или вложение. Заодно отдаёт имя
    /// из заголовка — форум присылает его в Content-Disposition, а в адресе вложения
    /// одни только цифры.
    /// </summary>
    public async Task<FileAnswer> GetBytesAsync(Uri uri, CancellationToken token = default)
    {
        await _gate.WaitAsync(token);

        try
        {
            await WaitTurnAsync(token);

            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Referrer = ForumUrls.Base;

            using var response = await SendWithRetryAsync(request, token);

            response.EnsureSuccessStatusCode();

            var bytes = await response.Content.ReadAsByteArrayAsync(token);
            var name = response.Content.Headers.ContentDisposition?.FileNameStar
                       ?? response.Content.Headers.ContentDisposition?.FileName;
            var type = response.Content.Headers.ContentType?.MediaType ?? string.Empty;

            return new FileAnswer(bytes, name?.Trim('"'), type);
        }
        catch (HttpRequestException error)
        {
            throw new ForumUnavailableException("Не получилось скачать файл с форума.", error);
        }
        catch (TaskCanceledException error) when (!token.IsCancellationRequested)
        {
            throw new ForumUnavailableException("Форум не отдал файл вовремя.", error);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string> PostFormAsync(
        Uri uri,
        IReadOnlyDictionary<string, string> fields,
        Uri? referer = null,
        CancellationToken token = default)
    {
        await _gate.WaitAsync(token);

        try
        {
            await WaitTurnAsync(token);

            using var request = new HttpRequestMessage(HttpMethod.Post, uri)
            {
                Content = new FormUrlEncodedContent(fields),
            };

            if (referer is not null)
            {
                request.Headers.Referrer = referer;
            }

            using var response = await SendWithRetryAsync(request, token);
            var body = await response.Content.ReadAsStringAsync(token);

            if (response.StatusCode == HttpStatusCode.Forbidden || LooksLikeBlockPage(body))
            {
                throw new ForumBlockedException("Форум отклонил запрос.", uri);
            }

            return body;
        }
        catch (HttpRequestException error)
        {
            throw new ForumUnavailableException("Не получилось отправить запрос на форум.", error);
        }
        finally
        {
            _gate.Release();
        }
    }

    // Три попытки с нарастающей паузой — на случай, если форум просто моргнул.
    // Повторяем только серверные ошибки: если ответа нет вообще, повтор лишь утроит ожидание.
    private async Task<HttpResponseMessage> SendWithRetryAsync(
        HttpRequestMessage request,
        CancellationToken token)
    {
        HttpResponseMessage? response = null;

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            response?.Dispose();

            System.Diagnostics.Debug.WriteLine($"[CyberForum] запрос {request.RequestUri}, попытка {attempt}");

            response = await _client.SendAsync(CloneRequest(request), HttpCompletionOption.ResponseContentRead, token);

            System.Diagnostics.Debug.WriteLine($"[CyberForum] ответ {(int)response.StatusCode} от {request.RequestUri}");

            var retryable = (int)response.StatusCode >= 500 || response.StatusCode == HttpStatusCode.RequestTimeout;

            if (!retryable || attempt == 3)
            {
                return response;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(400 * attempt), token);
        }

        return response!;
    }

    private static HttpRequestMessage CloneRequest(HttpRequestMessage source)
    {
        var clone = new HttpRequestMessage(source.Method, source.RequestUri)
        {
            Content = source.Content,
            Version = source.Version,
        };

        foreach (var header in source.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }

    // не частим: между запросами выдерживаем небольшой промежуток
    private async Task WaitTurnAsync(CancellationToken token)
    {
        var since = DateTimeOffset.UtcNow - _lastRequest;

        if (since < MinimalGap)
        {
            await Task.Delay(MinimalGap - since, token);
        }

        _lastRequest = DateTimeOffset.UtcNow;
    }

    // Заглушка блокировки — крошечная статичная страничка. Ловим по размеру и тексту,
    // чтобы не спутать с настоящей страницей форума.
    private static bool LooksLikeBlockPage(string body) =>
        body.Length < 4000 &&
        body.Contains("Нет доступа", StringComparison.OrdinalIgnoreCase) &&
        body.Contains("ошибка 403", StringComparison.OrdinalIgnoreCase);

    public void Dispose()
    {
        if (_ownsClient)
        {
            _client.Dispose();
        }

        _gate.Dispose();
    }
}
