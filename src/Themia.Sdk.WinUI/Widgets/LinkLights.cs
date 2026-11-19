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
/// [S-11900] [S-11965] The parts of one widget that a hovered (and, where the topic has one, a picked) value lights:
/// rows (<see cref="Row"/>: the item layer's hover fill, or the selected fill with the accent pill) and
/// marks (<see cref="Mark"/>: whole, the others at <see cref="FamilyShape.LinkPointed"/> or
/// <see cref="FamilyShape.LinkPicked"/>). Parts are registered as they are drawn and forgotten with
/// <see cref="Clear"/>; a change elsewhere repaints fills and opacities only, never rebuilds a row. A part registered
/// with <c>publishes</c> sets the hover while the pointer is on it (it never scrolls, pages or resizes anything).
/// Which part stands how is <see cref="LinkPartsState{TPart, T}"/>'s: under this widget's own pointer the part under it
/// alone, from another widget every part that is the same item.
/// </summary>
public sealed class LinkLights<T>
    where T : class, IEquatable<T>
{
    private readonly WidgetLinks _links;
    private readonly LinkTopic<T>? _picked;
    private readonly LinkPartsState<UIElement, T> _state;
    private readonly Dictionary<UIElement, Part> _parts = [];
    private readonly HashSet<UIElement> _wired = [];

    /// <param name="links">The widget's place on the links.</param>
    /// <param name="hovered">The hover topic the parts light for.</param>
    /// <param name="picked">The sticky topic of the same thing, when it has one (<see cref="LinkTopics.SelectedApp"/>).</param>
    public LinkLights(WidgetLinks links, LinkTopic<T> hovered, LinkTopic<T>? picked = null)
    {
        _links = links;
        _picked = picked;
        _state = new LinkPartsState<UIElement, T>(() => links.Get(hovered), value => links.Set(hovered, value));
        links.Follow(hovered, _ =>
        {
            _state.Heard();
            Apply();
        });
        if (picked is not null)
        {
            links.Follow(picked, value =>
            {
                _state.Picked = value;
                Apply();
            });
        }
    }

    /// <summary>Called after the parts were lit again (for what a widget derives: a caption, a total).</summary>
    public event Action? Changed;

    /// <summary>
    /// The value pointed at now: this widget's own pointer, else what the view's board holds. [S-11964] Read from the
    /// board, never remembered from the last change heard: a widget that took a hover over from another (the same
    /// value set again owns it silently) and then let it go never hears its own clear, and a remembered value would
    /// keep its row lit with nothing pointed at.
    /// </summary>
    public T? Pointed => _state.Pointed;

    /// <summary>The value picked on the view now.</summary>
    public T? Picked => _state.Picked;

    /// <summary>Forget every part (before the widget draws its parts again).</summary>
    public void Clear()
    {
        foreach (var element in _wired)
        {
            Unwire(element);
        }

        _wired.Clear();
        _parts.Clear();
        _state.ForgetAll();
    }

    /// <summary>Forget one part (a mark the widget took down while the rest stay).</summary>
    public void Remove(UIElement element)
    {
        _parts.Remove(element);
        _state.Forget(element);
        if (_wired.Remove(element))
        {
            Unwire(element);
        }
    }

    /// <summary>
    /// [S-11901] A row (or a cell, a card) that is <paramref name="key"/>: lit as pointed or picked;
    /// <paramref name="publishes"/> sets the hover while the pointer is on it. <paramref name="rest"/> puts
    /// the row back as the widget draws it when it stops being lit (its default: the item layer's rest fill).
    /// </summary>
    public void Row(FrameworkElement row, T? key, bool publishes = true, ItemRole role = ItemRole.Row, Action? rest = null)
        => Add(row, key, new Part(PartKind.Row, role, 1, rest), publishes);

    /// <summary>
    /// [S-11902] A mark (a bar, a ring's part, a chip, a day's dot) that is <paramref name="key"/>, drawn at
    /// <paramref name="opacity"/> of its own: whole while it is the thing, the others less.
    /// </summary>
    public void Mark(UIElement mark, T? key, bool publishes = true, double opacity = 1)
        => Add(mark, key, new Part(PartKind.Mark, ItemRole.Row, opacity, null), publishes);

    /// <summary>
    /// [S-3307] The widget's keyboard cursor (or a part it draws without registering) is on <paramref name="key"/>
    /// (null: it let go): publish it. Nothing here is lit for it ([S-11965]): the cursor's own mark says where it is.
    /// </summary>
    public void Point(T? key)
    {
        _state.Point(key);
        Apply();
    }

    /// <summary>[S-3302] Pick <paramref name="key"/>, or let it go when it is the pick.</summary>
    public void Pick(T key)
    {
        if (_picked is null)
        {
            return;
        }

        _state.Picked = _links.Joined ? _links.Toggle(_picked, key) : Equals(_state.Picked, key) ? null : key;
        Apply();
    }

    /// <summary>Light every registered part against what is pointed at and picked now.</summary>
    public void Apply()
    {
        foreach (var (element, part) in _parts)
        {
            Paint(element, part, drawn: false);
        }

        Changed?.Invoke();
    }

    private void Add(UIElement element, T? key, Part part, bool publishes)
    {
        if (_parts.TryGetValue(element, out var before))
        {
            // A part drawn again keeps how it was lit, so nothing repaints that need not.
            part.Shown = before.Shown;
        }

        _parts[element] = part;
        _state.Set(element, key);

        if (publishes && _wired.Add(element))
        {
            element.PointerEntered += OnEntered;
            element.PointerExited += OnLeft;
            element.PointerCanceled += OnLeft;
            element.PointerCaptureLost += OnLeft;
        }

        Paint(element, part, drawn: true);
    }

    /// <summary>
    /// One part as it stands now: a mark's opacity; a row painted when its light changes, and a row just drawn when it
    /// is lit (it was drawn at rest).
    /// </summary>
    private void Paint(UIElement element, Part part, bool drawn)
    {
        if (part.Kind == PartKind.Mark)
        {
            element.Opacity = part.Opacity * _state.Strength(element);
            return;
        }

        // A part drawn again at rest keeps the light it had, so the next Apply puts a row that was lit back.
        var light = _state.Light(element);
        if (drawn ? light == LinkLight.None : light == part.Shown)
        {
            return;
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

    /// <summary>A part forgotten lets go of its pointer, so a list drawn anew holds none of its old rows.</summary>
    private void Unwire(UIElement element)
    {
        element.PointerEntered -= OnEntered;
        element.PointerExited -= OnLeft;
        element.PointerCanceled -= OnLeft;
        element.PointerCaptureLost -= OnLeft;
    }

    private void OnEntered(object sender, PointerRoutedEventArgs args)
    {
        if (sender is UIElement element && _state.Enter(element))
        {
            Apply();
        }
    }

    private void OnLeft(object sender, PointerRoutedEventArgs args)
    {
        if (sender is not UIElement element || !_parts.TryGetValue(element, out var part))
        {
            return;
        }

        if (_state.Leave(element))
        {
            Apply();
        }

        // An interactive row paints itself at rest as the pointer leaves: one still lit (a pick) is lit again.
        if (part.Kind == PartKind.Row && part.Shown != LinkLight.None && element is FrameworkElement row)
        {
            LinkLook.Row(row, part.Shown, part.Role);
        }
    }

    private enum PartKind
    {
        Row,
        Mark,
    }

    private sealed class Part(PartKind kind, ItemRole role, double opacity, Action? rest)
    {
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
    /// [S-11903] [S-11963] Light the whole card <paramref name="view"/> stands in as <paramref name="light"/>: the card
    /// paints the item layer's fill over its whole box, edge to edge and in its own corners
    /// (<see cref="IWidgetSurface.LightCard"/>), never the view's own background, which stops at the inset the card
    /// gives the view's top and at any margin the view keeps at its foot. A view not yet in its card is lit as it
    /// loads into it.
    /// </summary>
    public static void Card(FrameworkElement view, LinkLight light)
    {
        ArgumentNullException.ThrowIfNull(view);
        Wanted.AddOrUpdate(view, new CardLight(light));
        if (SurfaceOf(view) is { } surface)
        {
            surface.LightCard(State(light));
        }
        else if (light != LinkLight.None)
        {
            view.Loaded -= OnCardLoaded;
            view.Loaded += OnCardLoaded;
        }
    }

    /// <summary>What each view last asked of its card, for one that asked before it stood in it.</summary>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<FrameworkElement, CardLight> Wanted = [];

    private sealed record CardLight(LinkLight Light);

    private static ItemState State(LinkLight light) => light switch
    {
        LinkLight.Pointed => ItemState.Hover,
        LinkLight.Picked => ItemState.Selected,
        _ => ItemState.Rest,
    };

    private static void OnCardLoaded(object sender, RoutedEventArgs args)
    {
        if (sender is not FrameworkElement view)
        {
            return;
        }

        view.Loaded -= OnCardLoaded;
        if (Wanted.TryGetValue(view, out var wanted) && SurfaceOf(view) is { } surface)
        {
            surface.LightCard(State(wanted.Light));
        }
    }

    /// <summary>The card (the surface) <paramref name="view"/> stands in, or null while it stands in none.</summary>
    private static IWidgetSurface? SurfaceOf(DependencyObject view)
    {
        for (var at = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(view) ?? (view as FrameworkElement)?.Parent;
             at is not null;
             at = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(at) ?? (at as FrameworkElement)?.Parent)
        {
            if (at is IWidgetSurface surface)
            {
                return surface;
            }
        }

        return null;
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
