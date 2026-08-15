using CyberForum.Core.Models;
using CyberForum.Core.Storage;

namespace CyberForum.App.ViewModels;

/// <summary>
/// Строка списка тем: сама тема плюс то, что мы про неё помним. Форум своё
/// «новое» показывает только вошедшим, поэтому непрочитанное считаем сами —
/// по количеству ответов на момент, когда тему открывали в последний раз.
/// </summary>
public sealed class ThreadRow(ThreadSummary thread, ThreadReadState? state)
{
    public ThreadSummary Thread { get; } = thread;

    public string Title => Thread.Title;

    public string? Preview => Thread.Preview;

    public DateTimeOffset? LastPostAt => Thread.LastPostAt;

    public bool IsSeen { get; } = state is not null;

    public bool IsFavorite { get; } = state?.IsFavorite ?? false;

    // У тем, открытых до появления этого счётчика, сохранённых ответов нет —
    // тогда честнее промолчать, чем показать «+368 новых».
    public int FreshReplies { get; } = state is null || state.Replies <= 0
        ? 0
        : Math.Max(0, thread.Replies - state.Replies);

    public bool HasFresh => IsSeen && FreshReplies > 0;

    public string Counter => IsSeen && FreshReplies > 0
        ? $"Ответов: {Thread.Replies}   ·   +{FreshReplies}"
        : $"Ответов: {Thread.Replies}";

    // прочитанное показываем поблёклым, чтобы новое само лезло в глаза
    public double Fade => IsSeen && FreshReplies == 0 ? 0.55 : 1;
}
