using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CyberForum.Core.Storage;

// Докуда человек дочитал тему
public sealed class ThreadReadState
{
    public int ThreadId { get; set; }

    public string Slug { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public int Page { get; set; } = 1;

    public int LastPostId { get; set; }

    // сколько ответов было в теме, когда мы её открывали — по этому и считаем новые
    public int Replies { get; set; }

    public DateTime SeenAtUtc { get; set; }

    public bool IsFavorite { get; set; }
}

/// <summary>
/// Локальное хранилище: страницы форума и отметки о прочитанном. Нужно не столько ради
/// скорости, сколько ради метро и лифтов — без сети надо показывать то, что уже открывали.
///
/// Раньше тут был SQLite, но ради «положить страницу по адресу и достать её обратно»
/// он не окупается: тянет нативную библиотеку, которая на Android ещё и не завелась.
/// Страницы теперь лежат обычными файлами, отметки — одним json.
/// </summary>
public sealed class CacheStore : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly string _pagesDirectory;
    private readonly string _statePath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private Dictionary<int, ThreadReadState>? _states;

    public CacheStore(string directory)
    {
        Directory = directory;
        _pagesDirectory = Path.Combine(directory, "pages");
        _statePath = Path.Combine(directory, "read-state.json");

        System.IO.Directory.CreateDirectory(_pagesDirectory);
    }

    public string Directory { get; }

    // сколько живёт страница, прежде чем мы полезем за свежей
    public TimeSpan FreshFor { get; init; } = TimeSpan.FromMinutes(10);

    public async Task SavePageAsync(Uri url, string body)
    {
        var path = PathFor(url);

        await File.WriteAllTextAsync(path, body, Encoding.UTF8);
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
    }

    // Отдаёт страницу, если она не старше указанного срока. Передай TimeSpan.MaxValue,
    // когда сеть отвалилась: вчерашняя тема лучше пустого экрана.
    public async Task<string?> GetPageAsync(Uri url, TimeSpan? maxAge = null)
    {
        var path = PathFor(url);

        if (!File.Exists(path))
        {
            return null;
        }

        var limit = maxAge ?? FreshFor;
        var age = DateTime.UtcNow - File.GetLastWriteTimeUtc(path);

        if (age > limit)
        {
            return null;
        }

        try
        {
            return await File.ReadAllTextAsync(path, Encoding.UTF8);
        }
        catch (IOException)
        {
            // файл могли подчистить прямо сейчас — не беда, сходим в сеть
            return null;
        }
    }

    public Task<int> ClearOlderThanAsync(TimeSpan age)
    {
        var edge = DateTime.UtcNow - age;
        var removed = 0;

        foreach (var path in System.IO.Directory.EnumerateFiles(_pagesDirectory, "*.html"))
        {
            if (File.GetLastWriteTimeUtc(path) >= edge)
            {
                continue;
            }

            try
            {
                File.Delete(path);
                removed++;
            }
            catch (IOException)
            {
                // занят — переживём, вычистим в следующий раз
            }
        }

        return Task.FromResult(removed);
    }

    public async Task RememberPositionAsync(ThreadReadState state)
    {
        await _lock.WaitAsync();

        try
        {
            var states = await LoadStatesAsync();

            state.SeenAtUtc = DateTime.UtcNow;

            // Отметку «в избранном» и число прочитанных ответов случайным заходом
            // не сбрасываем: их проставляет тот, кто знает, а знают о них разные места.
            if (states.TryGetValue(state.ThreadId, out var previous))
            {
                state.IsFavorite = state.IsFavorite || previous.IsFavorite;

                if (state.Replies <= 0)
                {
                    state.Replies = previous.Replies;
                }
            }

            states[state.ThreadId] = state;
            await SaveStatesAsync(states);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Переключает «в избранном» и отдаёт то, что получилось. Если тему ещё не открывали,
    /// заводим для неё запись на месте — иначе закладку некуда положить.
    /// </summary>
    public async Task<bool> ToggleFavoriteAsync(ThreadReadState state)
    {
        await _lock.WaitAsync();

        try
        {
            var states = await LoadStatesAsync();

            if (states.TryGetValue(state.ThreadId, out var saved))
            {
                saved.IsFavorite = !saved.IsFavorite;
                saved.SeenAtUtc = DateTime.UtcNow;
            }
            else
            {
                state.IsFavorite = true;
                state.SeenAtUtc = DateTime.UtcNow;
                states[state.ThreadId] = saved = state;
            }

            await SaveStatesAsync(states);

            return saved.IsFavorite;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<ThreadReadState?> GetPositionAsync(int threadId)
    {
        var states = await LoadStatesAsync();

        return states.GetValueOrDefault(threadId);
    }

    // всё, что мы помним о темах — списку нужно разом, а не по одной
    public async Task<IReadOnlyDictionary<int, ThreadReadState>> GetStatesAsync() =>
        await LoadStatesAsync();

    public async Task<IReadOnlyList<ThreadReadState>> GetFavoritesAsync()
    {
        var states = await LoadStatesAsync();

        return states.Values
            .Where(s => s.IsFavorite)
            .OrderByDescending(s => s.SeenAtUtc)
            .ToList();
    }

    public async Task<IReadOnlyList<ThreadReadState>> GetHistoryAsync(int limit = 50)
    {
        var states = await LoadStatesAsync();

        return states.Values
            .OrderByDescending(s => s.SeenAtUtc)
            .Take(limit)
            .ToList();
    }

    // адрес превращаем в имя файла хэшем: в нём не бывает запрещённых символов
    // и он не упрётся в предел длины пути
    private string PathFor(Uri url)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(url.ToString()));

        return Path.Combine(_pagesDirectory, Convert.ToHexString(hash)[..32] + ".html");
    }

    private async Task<Dictionary<int, ThreadReadState>> LoadStatesAsync()
    {
        if (_states is not null)
        {
            return _states;
        }

        if (!File.Exists(_statePath))
        {
            return _states = [];
        }

        try
        {
            var json = await File.ReadAllTextAsync(_statePath, Encoding.UTF8);
            var loaded = JsonSerializer.Deserialize<Dictionary<int, ThreadReadState>>(json, JsonOptions);

            return _states = loaded ?? [];
        }
        catch (Exception error) when (error is JsonException or IOException)
        {
            // испорченный файл — не повод падать, начнём историю заново
            return _states = [];
        }
    }

    private async Task SaveStatesAsync(Dictionary<int, ThreadReadState> states)
    {
        var json = JsonSerializer.Serialize(states, JsonOptions);
        var temporary = _statePath + ".tmp";

        // пишем через временный файл, чтобы не остаться с обрезанным json,
        // если приложение закроют посреди записи
        await File.WriteAllTextAsync(temporary, json, Encoding.UTF8);
        File.Move(temporary, _statePath, overwrite: true);
    }

    public ValueTask DisposeAsync()
    {
        _lock.Dispose();

        return ValueTask.CompletedTask;
    }
}
