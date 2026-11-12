using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Themia.Core.Links;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;

namespace Themia.App.Widgets;

/// <summary>
/// [S-3300] [S-11905] One widget's place on its view's links, kept across a view switch: the topics it
/// follows are named once (<see cref="Follow{T}"/>, any time) and watched again on every
/// <see cref="Join"/>, each told the value it holds there at once, so a widget that joins a view lights
/// what is pointed at or picked there already. Without a board (a host with no view) publishing changes
/// nothing and every follower hears nothing. UI thread only, as the board is ([S-3306]).
/// </summary>
public sealed class WidgetLinks
{
    private readonly List<Action<LinkMember>> _follows = [];
    private LinkMember? _member;

    /// <summary>Whether the widget is on a board now.</summary>
    public bool Joined => _member is { IsDisposed: false };

    /// <summary>
    /// Follow <paramref name="topic"/>: <paramref name="changed"/> hears every other widget's change, and on
    /// joining the value the topic holds then (null when nothing). One handler per topic.
    /// </summary>
    public void Follow<T>(LinkTopic<T> topic, Action<T?> changed)
        where T : class, IEquatable<T>
    {
        ArgumentNullException.ThrowIfNull(changed);
        void Watch(LinkMember member)
        {
            member.Watch(topic, changed);
            changed(member.Get(topic));
        }

        _follows.Add(Watch);
        if (_member is { } joined)
        {
            Watch(joined);
        }
    }

    /// <summary>Join the board of the view this widget is on (Attach); a second join leaves the first board.</summary>
    public void Join(LinkBoard? board)
    {
        Leave();
        if (board is null)
        {
            return;
        }

        _member = board.Join();
        foreach (var watch in _follows.ToArray())
        {
            watch(_member);
        }
    }

    /// <summary>Leave the board (Detach): this widget's hovers go with it, a selection stays ([S-3304]).</summary>
    public void Leave()
    {
        _member?.Dispose();
        _member = null;
    }

    /// <summary>The value a topic holds on the board now, or null.</summary>
    public T? Get<T>(LinkTopic<T> topic)
        where T : class, IEquatable<T>
        => _member?.Get(topic);

    /// <summary>Publish a value, or null to let go of this widget's own. True when the board changed.</summary>
    public bool Set<T>(LinkTopic<T> topic, T? value)
        where T : class, IEquatable<T>
        => _member?.Set(topic, value) ?? false;

    /// <summary>[S-3302] Select a value, or let it go when it is the selection. Returns what the topic holds afterwards.</summary>
    public T? Toggle<T>(LinkTopic<T> topic, T value)
        where T : class, IEquatable<T>
        => _member?.Toggle(topic, value);
}
