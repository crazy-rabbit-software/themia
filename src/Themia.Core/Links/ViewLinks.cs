using Themia.Core.Diagnostics;

namespace Themia.Core.Links;

/// <summary>
/// [S-3300] What widgets on one view can share with each other. A closed set: a topic is a piece of
/// code that publishes and a piece that listens, so it is an enum member, never a string a typo
/// could invent.
/// </summary>
public enum LinkTopicId
{
    /// <summary>[S-3310] The app under the pointer (or the keyboard cursor) in a screen-time widget.</summary>
    HoveredApp = 0,

    /// <summary>[S-3310] The app a screen-time widget was clicked on; sticky.</summary>
    SelectedApp = 1,

    /// <summary>[S-3311] A day picked in a date widget (a calendar); sticky.</summary>
    SelectedDate = 2,

    /// <summary>[S-3311] The days a date widget shows right now (a calendar's month or week); sticky.</summary>
    VisibleRange = 3,

    /// <summary>[S-3623] The calendar event under the pointer in a calendar widget.</summary>
    HoveredEvent = 4,
}

/// <summary>[S-3302] How long a shared value lives.</summary>
public enum LinkLifetime
{
    /// <summary>
    /// Ephemeral: belongs to the member that set it, and goes when that member clears it or leaves
    /// (its widget detaches, which a view switch does). Never kept, never persisted.
    /// </summary>
    Hover = 0,

    /// <summary>
    /// Kept until a member changes or clears it, for as long as the app runs: a view switched away
    /// from and back to still has it. Never persisted: a restart starts with nothing selected.
    /// </summary>
    Sticky = 1,
}

/// <summary>[S-3301] A topic without its value type, for the board's bookkeeping.</summary>
public abstract class LinkTopic
{
    private protected LinkTopic(LinkTopicId id, LinkLifetime lifetime)
    {
        Id = id;
        Lifetime = lifetime;
    }

    public LinkTopicId Id { get; }

    public LinkLifetime Lifetime { get; }

    public override string ToString() => Id.ToString();
}

/// <summary>
/// [S-3301] A topic and the type of value it carries. The value is a record, so "the same value"
/// is value equality and setting it again is no change.
/// </summary>
public sealed class LinkTopic<T> : LinkTopic
    where T : class, IEquatable<T>
{
    internal LinkTopic(LinkTopicId id, LinkLifetime lifetime)
        : base(id, lifetime)
    {
    }
}

/// <summary>[S-3623] One calendar event: its account, its calendar (null for the default one) and the provider's id.</summary>
public sealed record EventLink(string Account, string? Calendar, string Event);

/// <summary>[S-3310] An app, by its screen-time key (the lower-case executable stem, or "Other").</summary>
public sealed record AppLink(string App);

/// <summary>[S-3311] One calendar day.</summary>
public sealed record DateLink(DateOnly Date);

/// <summary>[S-3311] A run of days, both ends included. Built in order whichever way round the ends are given.</summary>
public sealed record RangeLink
{
    public RangeLink(DateOnly first, DateOnly last)
    {
        First = first <= last ? first : last;
        Last = first <= last ? last : first;
    }

    public DateOnly First { get; }

    public DateOnly Last { get; }

    public bool Contains(DateOnly day) => day >= First && day <= Last;
}

/// <summary>[S-3301] Every topic there is. Add one here, with its enum member, and nowhere else.</summary>
public static class LinkTopics
{
    public static readonly LinkTopic<AppLink> HoveredApp = new(LinkTopicId.HoveredApp, LinkLifetime.Hover);

    public static readonly LinkTopic<AppLink> SelectedApp = new(LinkTopicId.SelectedApp, LinkLifetime.Sticky);

    public static readonly LinkTopic<DateLink> SelectedDate = new(LinkTopicId.SelectedDate, LinkLifetime.Sticky);

    public static readonly LinkTopic<RangeLink> VisibleRange = new(LinkTopicId.VisibleRange, LinkLifetime.Sticky);

    public static readonly LinkTopic<EventLink> HoveredEvent = new(LinkTopicId.HoveredEvent, LinkLifetime.Hover);

}

/// <summary>
/// [S-3300] One view's shared interaction state: what is hovered and what is selected, by topic.
/// <para>
/// Widgets take part through a <see cref="LinkMember"/> (<see cref="Join"/>): join in Attach,
/// dispose in Detach. A member never hears its own change ([S-3303]), so a widget that lights a
/// row and publishes it does not light it twice. UI thread only: the board has no lock, because
/// every caller is a widget view and every view lives on the one UI thread ([S-3306]).
/// </para>
/// </summary>
public sealed class LinkBoard
{
    private const string Scope = "links";

    private readonly Dictionary<LinkTopicId, Entry> _values = [];
    private readonly List<LinkMember> _members = [];

    /// <summary>Widgets taking part right now. A view that is not shown has none ([S-3305]).</summary>
    public int MemberCount => _members.Count;

    /// <summary>Take part: watch topics and publish to them until the member is disposed.</summary>
    public LinkMember Join()
    {
        var member = new LinkMember(this);
        _members.Add(member);
        return member;
    }

    /// <summary>The value a topic holds now, or null.</summary>
    public T? Peek<T>(LinkTopic<T> topic)
        where T : class, IEquatable<T>
        => _values.TryGetValue(topic.Id, out var entry) ? (T)entry.Value : null;

    internal bool Set<T>(LinkTopic<T> topic, T? value, LinkMember source)
        where T : class, IEquatable<T>
    {
        var had = _values.TryGetValue(topic.Id, out var entry);

        if (value is null)
        {
            // [S-3304] A hover belongs to whoever set it: a late "pointer left" from one widget never
            // clears what another has lit since.
            if (!had || (topic.Lifetime == LinkLifetime.Hover && !ReferenceEquals(entry.Owner, source)))
            {
                return false;
            }

            _values.Remove(topic.Id);
            Notify(topic.Id, source);
            return true;
        }

        if (had && value.Equals((T)entry.Value))
        {
            // The same value again changes nothing, but the latest to set a hover owns it.
            _values[topic.Id] = entry with { Owner = source };
            return false;
        }

        _values[topic.Id] = new Entry(value, source, topic.Lifetime);
        Notify(topic.Id, source);
        return true;
    }

    internal void Leave(LinkMember member)
    {
        _members.Remove(member);

        // [S-3304] A widget that goes takes its hover with it; what it selected stays.
        foreach (var id in _values.Where(pair => pair.Value.Lifetime == LinkLifetime.Hover && ReferenceEquals(pair.Value.Owner, member))
            .Select(pair => pair.Key).ToList())
        {
            _values.Remove(id);
            Notify(id, member);
        }
    }

    /// <summary>
    /// Tell every member but <paramref name="source"/>. Over a copy, so a handler may join, leave or
    /// set another topic; each hears the value as it is when its turn comes, never a stale one.
    /// </summary>
    private void Notify(LinkTopicId id, LinkMember? source)
    {
        foreach (var member in _members.ToArray())
        {
            if (ReferenceEquals(member, source) || member.IsDisposed)
            {
                continue;
            }

            try
            {
                member.Deliver(id);
            }
            catch (Exception ex)
            {
                // One widget failing to redraw must not keep the others from hearing the change.
                Log.Error(Scope, $"a widget failed handling {id}", ex);
            }
        }
    }

    private readonly record struct Entry(object Value, LinkMember? Owner, LinkLifetime Lifetime);
}

/// <summary>
/// [S-3303] One widget's place on a <see cref="LinkBoard"/>. Disposing it unwatches everything and
/// clears the hovers it set; afterwards every call is a no-op, so a late pointer event from a
/// detached view is harmless.
/// </summary>
public sealed class LinkMember : IDisposable
{
    private readonly LinkBoard _board;
    private readonly Dictionary<LinkTopicId, Action> _watches = [];

    internal LinkMember(LinkBoard board) => _board = board;

    public bool IsDisposed { get; private set; }

    /// <summary>The value a topic holds now, or null.</summary>
    public T? Get<T>(LinkTopic<T> topic)
        where T : class, IEquatable<T>
        => IsDisposed ? null : _board.Peek(topic);

    /// <summary>Hear every change another member makes to <paramref name="topic"/>. One handler per topic: a second replaces the first.</summary>
    public void Watch<T>(LinkTopic<T> topic, Action<T?> changed)
        where T : class, IEquatable<T>
    {
        ArgumentNullException.ThrowIfNull(changed);

        if (!IsDisposed)
        {
            _watches[topic.Id] = () => changed(_board.Peek(topic));
        }
    }

    /// <summary>Publish a value, or null to clear it. True when the board changed.</summary>
    public bool Set<T>(LinkTopic<T> topic, T? value)
        where T : class, IEquatable<T>
        => !IsDisposed && _board.Set(topic, value, this);

    /// <summary>Clear a topic. A hover is only cleared by the member that set it ([S-3304]).</summary>
    public bool Clear<T>(LinkTopic<T> topic)
        where T : class, IEquatable<T>
        => Set(topic, null);

    /// <summary>
    /// [S-3302] Select <paramref name="value"/>, or clear the topic when it is already the selection:
    /// clicking the selected thing again lets it go. Returns what the topic holds afterwards.
    /// </summary>
    public T? Toggle<T>(LinkTopic<T> topic, T value)
        where T : class, IEquatable<T>
    {
        ArgumentNullException.ThrowIfNull(value);

        if (IsDisposed)
        {
            return null;
        }

        _board.Set(topic, value.Equals(_board.Peek(topic)) ? null : value, this);
        return _board.Peek(topic);
    }

    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        IsDisposed = true;
        _watches.Clear();
        _board.Leave(this);
    }

    internal void Deliver(LinkTopicId id)
    {
        if (_watches.TryGetValue(id, out var handler))
        {
            handler();
        }
    }
}

/// <summary>
/// [S-3300] The boards of every view, by view id: one board per view, however many displays show
/// it. Boards are made on first use and dropped with their view ([S-3305]). UI thread only.
/// </summary>
public sealed class LinkRegistry
{
    private readonly Dictionary<string, LinkBoard> _boards = new(StringComparer.Ordinal);

    public int Count => _boards.Count;

    /// <summary>The board of the view with this id, made on first use.</summary>
    public LinkBoard For(string viewId)
    {
        ArgumentException.ThrowIfNullOrEmpty(viewId);

        if (!_boards.TryGetValue(viewId, out var board))
        {
            board = new LinkBoard();
            _boards[viewId] = board;
        }

        return board;
    }

    /// <summary>[S-3305] Keep the boards of these views and drop the rest: a deleted view's selection goes with it.</summary>
    public void Retain(IEnumerable<string> viewIds)
    {
        var live = new HashSet<string>(viewIds, StringComparer.Ordinal);

        foreach (var id in _boards.Keys.Where(id => !live.Contains(id)).ToList())
        {
            _boards.Remove(id);
        }
    }
}
