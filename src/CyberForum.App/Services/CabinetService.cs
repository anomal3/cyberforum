using CyberForum.Core;
using CyberForum.Core.Models;

namespace CyberForum.App.Services;

/// <summary>
/// Личный кабинет форума, загруженный один раз на всех: профиль показывает счётчики,
/// а страницы-списки берут отсюда же готовые данные и в сеть повторно не ходят.
/// </summary>
public sealed class CabinetService(ForumClient client)
{
    public UserCabinet Current { get; private set; } = new();

    public bool Loaded { get; private set; }

    // по нему оболочка показывает и прячет колокольчик в нижних вкладках
    public event EventHandler<int>? NotificationsChanged;

    public async Task<UserCabinet> RefreshAsync(CancellationToken token = default)
    {
        Current = await client.GetCabinetAsync(token);
        Loaded = true;

        NotificationsChanged?.Invoke(this, Current.Notifications);

        return Current;
    }

    /// <summary>
    /// Страница участника. Своя, чужая — неважно, кэш у клиента общий.
    /// </summary>
    public Task<MemberProfile> MemberAsync(int userId, CancellationToken token = default) =>
        client.GetMemberAsync(userId, token);

    public void Forget()
    {
        Current = new UserCabinet();
        Loaded = false;

        NotificationsChanged?.Invoke(this, 0);
    }
}
