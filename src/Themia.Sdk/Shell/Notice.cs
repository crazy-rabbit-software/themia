namespace Themia.Core.Shell;

/// <summary>[S-880] How much a notice matters: its icon, its accent and how long it stays.</summary>
public enum NoticeKind
{
    Info = 0,
    Success = 1,
    Warning = 2,
    Error = 3,
}

/// <summary>[S-881] The one button a notice may carry ("Undo", "Upgrade", "Retry").</summary>
public sealed record NoticeAction(string Label, Action Invoke);

/// <summary>
/// [S-880] A transient message on the desktop: a title, an optional line of detail and an optional
/// action. Every notice gets its own id at construction (a class, not a record:
/// a <c>with</c> copy would share the id and dismissing one would take down both).
/// </summary>
public sealed class Notice
{
    private static long s_next;

    public Notice(NoticeKind kind, string title)
    {
        Kind = kind;
        Title = title;
        Id = Interlocked.Increment(ref s_next);
    }

    /// <summary>Unique for the process; what dismissing names.</summary>
    public long Id { get; }

    public NoticeKind Kind { get; }

    public string Title { get; }

    public string? Message { get; init; }

    public NoticeAction? Action { get; init; }

    /// <summary>
    /// [S-882] Notices with the same key are the same message: a new one replaces the one showing
    /// instead of stacking a copy (pressing Ctrl+Z five times shows one "Undid …", not five).
    /// </summary>
    public string? Key { get; init; }

    /// <summary>[S-883] Stays until dismissed or acted on (safe mode), rather than timing out.</summary>
    public bool Sticky { get; init; }

    public static Notice Info(string title, string? message = null) => new(NoticeKind.Info, title) { Message = message };

    public static Notice Success(string title, string? message = null) => new(NoticeKind.Success, title) { Message = message };

    public static Notice Warning(string title, string? message = null) => new(NoticeKind.Warning, title) { Message = message };

    public static Notice Error(string title, string? message = null) => new(NoticeKind.Error, title) { Message = message };
}

/// <summary>
/// [S-882]/[S-883] The rules for the notices one surface shows: how many at once, which one goes when
/// another arrives, and how long each stays. Pure, so the host only draws what this decides.
/// </summary>
public sealed class NoticeStack
{
    /// <summary>[S-882] At most this many at once, like Windows' own toasts: a fourth pushes out the oldest.</summary>
    public const int MaxVisible = 3;

    private readonly List<Notice> _visible = [];

    /// <summary>Oldest first, so the newest is drawn at the bottom, nearest the taskbar.</summary>
    public IReadOnlyList<Notice> Visible => _visible;

    /// <summary>
    /// Show <paramref name="notice"/>. Returns the notices it displaced (one with the same
    /// <see cref="Notice.Key"/>, or the oldest past <see cref="MaxVisible"/>) so the host can take them down.
    /// <para>
    /// A keyed replacement takes the old one's place at the bottom, as the newest: it is news again.
    /// Sticky notices are pushed out last, because they wait for the user to answer them.
    /// </para>
    /// </summary>
    public IReadOnlyList<Notice> Push(Notice notice)
    {
        ArgumentNullException.ThrowIfNull(notice);

        var removed = new List<Notice>();
        if (notice.Key is { } key)
        {
            foreach (var same in _visible.Where(n => n.Key == key).ToList())
            {
                _visible.Remove(same);
                removed.Add(same);
            }
        }

        _visible.Add(notice);

        while (_visible.Count > MaxVisible)
        {
            var oldest = _visible.FirstOrDefault(n => !n.Sticky && n.Id != notice.Id) ?? _visible[0];
            _visible.Remove(oldest);
            removed.Add(oldest);
        }

        return removed;
    }

    /// <summary>Take a notice down (timed out, closed or acted on). False when it was not showing.</summary>
    public bool Dismiss(long id) => _visible.RemoveAll(n => n.Id == id) > 0;

    /// <summary>
    /// [S-883] How long a notice stays: longer the more it matters, and longer again with a button,
    /// which has to be read and then reached. Null for a sticky notice. The timer pauses while the
    /// pointer is over the notice, so it never vanishes under a hand on its way to the button.
    /// </summary>
    public static TimeSpan? Lifetime(Notice notice)
    {
        ArgumentNullException.ThrowIfNull(notice);

        if (notice.Sticky)
        {
            return null;
        }

        var seconds = notice.Kind switch
        {
            NoticeKind.Error => 8,
            NoticeKind.Warning => 6,
            _ => 4,
        };

        return TimeSpan.FromSeconds(notice.Action is null ? seconds : seconds + 3);
    }
}
