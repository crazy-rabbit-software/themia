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

/// <summary>
/// [S-11900] The parts of one widget that a hovered (and, where the topic has one, a picked) value lights:
/// rows (<see cref="Row"/>: the item layer's hover fill, or the selected fill with the accent pill) and
/// marks (<see cref="Mark"/>: whole, the others at <see cref="FamilyShape.LinkPointed"/> or
/// <see cref="FamilyShape.LinkPicked"/>). Parts are registered as they are drawn and forgotten with
/// <see cref="Clear"/>; a change elsewhere repaints fills and opacities only, never rebuilds a row
/// ([S-11961]). A part registered with <c>publishes</c> sets the hover while the pointer is on it
/// ([S-11960]: it never scrolls, pages or resizes anything).
/// </summary>
public sealed class LinkLights<T>
    where T : class, IEquatable<T>
{
    private readonly WidgetLinks _links;
    private readonly LinkTopic<T> _hovered;
    private readonly LinkTopic<T>? _picked;
    private readonly Dictionary<UIElement, Part> _parts = [];
    private readonly HashSet<UIElement> _wired = [];
    private T? _pointed;
    private T? _selected;
    private T? _own;

    /// <param name="links">The widget's place on the links.</param>
    /// <param name="hovered">The hover topic the parts light for.</param>
    /// <param name="picked">The sticky topic of the same thing, when it has one (<see cref="LinkTopics.SelectedApp"/>).</param>
    public LinkLights(WidgetLinks links, LinkTopic<T> hovered, LinkTopic<T>? picked = null)
    {
        _links = links;
        _hovered = hovered;
        _picked = picked;
        links.Follow(hovered, value =>
        {
            _pointed = value;
            _own = null;
            Apply();
        });
        if (picked is not null)
        {
            links.Follow(picked, value =>
            {
                _selected = value;
                Apply();
            });
        }
    }

    /// <summary>Called after the parts were lit again (for what a widget derives: a caption, a total).</summary>
    public event Action? Changed;

    /// <summary>The value pointed at now: this widget's own pointer, else another widget's.</summary>
    public T? Pointed => _own ?? _pointed;

    /// <summary>The value picked on the view now.</summary>
    public T? Picked => _selected;

    /// <summary>Forget every part (before the widget draws its parts again).</summary>
    public void Clear() => _parts.Clear();

    /// <summary>
    /// [S-11901] A row (or a cell, a card) that is <paramref name="key"/>: lit as pointed or picked;
    /// <paramref name="publishes"/> sets the hover while the pointer is on it. <paramref name="rest"/> puts
    /// the row back as the widget draws it when it stops being lit (its default: the item layer's rest fill).
    /// </summary>
    public void Row(FrameworkElement row, T? key, bool publishes = true, ItemRole role = ItemRole.Row, Action? rest = null)
        => Add(row, new Part(key, PartKind.Row, role, 1, rest), publishes);

    /// <summary>
    /// [S-11902] A mark (a bar, a ring's part, a chip, a day's dot) that is <paramref name="key"/>, drawn at
    /// <paramref name="opacity"/> of its own: whole while it is the thing, the others less.
    /// </summary>
    public void Mark(UIElement mark, T? key, bool publishes = true, double opacity = 1)
        => Add(mark, new Part(key, PartKind.Mark, ItemRole.Row, opacity, null), publishes);

    /// <summary>
    /// The widget's own pointer (or keyboard cursor, [S-3307]) is on <paramref name="key"/> (null: it left):
    /// publish it and light it here too.
    /// </summary>
    public void Point(T? key)
    {
        _own = key;
        _links.Set(_hovered, key);
        Apply();
    }

    /// <summary>[S-3302] Pick <paramref name="key"/>, or let it go when it is the pick.</summary>
    public void Pick(T key)
    {
        if (_picked is null)
        {
            return;
        }

        _selected = _links.Joined ? _links.Toggle(_picked, key) : Equals(_selected, key) ? null : key;
        Apply();
    }

    /// <summary>Light every registered part against what is pointed at and picked now.</summary>
    public void Apply()
    {
        var shown = _parts.Values.Select(part => part.Key).OfType<T>().ToHashSet();
        var pointed = LinkEmphasis.Shown(Pointed, shown);
        var picked = LinkEmphasis.Shown(_selected, shown);

        foreach (var (element, part) in _parts)
        {
            var light = LinkEmphasis.Light(part.Key, pointed, picked);
            if (part.Kind == PartKind.Mark)
            {
                element.Opacity = part.Opacity * LinkEmphasis.Strength(part.Key, pointed, picked);
                continue;
            }

            if (light == part.Shown)
            {
                continue;
            }

            part.Shown = light;
            if (light == LinkLight.None && part.Rest is { } back)
            {
                back();
                LinkLook.Pill((FrameworkElement)element, false);
            }
            else
            {
                LinkLook.Row((FrameworkElement)element, light, part.Role);
            }
        }

        Changed?.Invoke();
    }

    private void Add(UIElement element, Part part, bool publishes)
    {
        if (_parts.TryGetValue(element, out var before))
        {
            // A part drawn again keeps how it was lit, so nothing repaints that need not.
            part.Shown = before.Shown;
        }

        _parts[element] = part;

        if (publishes && _wired.Add(element))
        {
            element.PointerEntered += OnEntered;
            element.PointerExited += OnLeft;
            element.PointerCanceled += OnLeft;
            element.PointerCaptureLost += OnLeft;
        }

        var shown = _parts.Values.Select(p => p.Key).OfType<T>().ToHashSet();
        var pointed = LinkEmphasis.Shown(Pointed, shown);
        var picked = LinkEmphasis.Shown(_selected, shown);
        if (part.Kind == PartKind.Mark)
        {
            element.Opacity = part.Opacity * LinkEmphasis.Strength(part.Key, pointed, picked);
        }
        else if (LinkEmphasis.Light(part.Key, pointed, picked) is var light && light != LinkLight.None)
        {
            part.Shown = light;
            LinkLook.Row((FrameworkElement)element, light, part.Role);
        }
    }

    private void OnEntered(object sender, PointerRoutedEventArgs args)
    {
        if (sender is UIElement element && _parts.TryGetValue(element, out var part) && part.Key is { } key)
        {
            Point(key);
        }
    }

    private void OnLeft(object sender, PointerRoutedEventArgs args)
    {
        if (sender is UIElement element && _parts.TryGetValue(element, out var part) && Equals(part.Key, _own))
        {
            Point(null);
        }
    }

    private enum PartKind
    {
        Row,
        Mark,
    }

    private sealed class Part(T? key, PartKind kind, ItemRole role, double opacity, Action? rest)
    {
        public T? Key { get; } = key;

        public PartKind Kind { get; } = kind;

        public ItemRole Role { get; } = role;

        public double Opacity { get; } = opacity;

        public Action? Rest { get; } = rest;

        public LinkLight Shown { get; set; }
    }
}

/// <summary>
/// [S-11901] [S-11904] The one look of a linked row: the item layer's hover fill for a thing pointed at
/// elsewhere, its selected fill and the accent pill at the start for a thing picked, as if the pointer
/// or a click were on it. No animation; high contrast is the item layer's own (the system's highlight).
/// </summary>
public static class LinkLook
{
    private const string PillTag = "link-pill";

    /// <summary>Light <paramref name="row"/> as <paramref name="light"/>.</summary>
    public static void Row(FrameworkElement row, LinkLight light, ItemRole role = ItemRole.Row)
    {
        ItemFill.Paint(row, role, light switch
        {
            LinkLight.Pointed => ItemState.Hover,
            LinkLight.Picked => ItemState.Selected,
            _ => ItemState.Rest,
        });
        Pill(row, light == LinkLight.Picked);
    }

    /// <summary>
    /// [S-11901] The accent pill at a picked row's start, centred in its side inset: on a grid (a
    /// <see cref="Themia.Sdk.WinUI.Kit.RowBlock"/>, a cell) or a border holding one; a part of another
    /// shape shows its selected fill alone.
    /// </summary>
    public static void Pill(FrameworkElement row, bool shown)
    {
        var grid = row as Grid ?? (row as Border)?.Child as Grid;
        if (grid is null)
        {
            return;
        }

        var pill = grid.Children.OfType<Border>().FirstOrDefault(b => Equals(b.Tag, PillTag));
        if (pill is null)
        {
            if (!shown)
            {
                return;
            }

            var inset = grid.Padding.Left;
            pill = new Border
            {
                Tag = PillTag,
                Width = FamilyShape.LinkPillWidth,
                Height = FamilyShape.LinkPillHeight,
                CornerRadius = Family.Round(FamilyShape.LinkPillWidth, FamilyShape.LinkPillHeight),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(FamilyShape.LinkPillStart(inset), 0, 0, 0),
                IsHitTestVisible = false,
                Background = Family.Brush(ColourRole.Accent),
            };
            Grid.SetColumnSpan(pill, Math.Max(1, grid.ColumnDefinitions.Count));
            Grid.SetRowSpan(pill, Math.Max(1, grid.RowDefinitions.Count));
            grid.Children.Add(pill);
        }

        pill.Visibility = shown ? Visibility.Visible : Visibility.Collapsed;
    }
}
