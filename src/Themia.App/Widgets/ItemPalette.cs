using System.Runtime.CompilerServices;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Shell;
using Themia.Core.Diagnostics;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>What holds the item palette its items are painted from: a widget's card.</summary>
internal interface IItemPaletteOwner
{
    ItemPalette Items { get; }
}

/// <summary>
/// [S-6510]..[S-6514] One set of item-fill brushes, a brush per role and state, recoloured in place
/// from an item layer (Core <see cref="ItemLook"/>). Every card owns one, so a widget's own layer
/// ([S-6512]) reaches its items and no other widget's; <see cref="App"/> is the app-wide layer, for an
/// item painted before it is in a card. A brush handed out stays the same object for the palette's
/// life, so nothing that holds one has to be told the layer changed. UI thread only.
/// </summary>
internal sealed class ItemPalette
{
    private readonly Dictionary<(ItemRole Role, ItemState State), SolidColorBrush> _brushes = [];
    private ItemLayer _layer = new();
    private bool _dark = true;
    private bool _applied;

    /// <summary>The palette of the card whose view is being built right now, if one is.</summary>
    [ThreadStatic]
    private static ItemPalette? _building;

    /// <summary>The app-wide layer's palette, kept up to date by <see cref="DesignApplier"/>.</summary>
    public static ItemPalette App { get; } = new();

    /// <summary>
    /// The palette an item made right now belongs to when it is in no card yet: the card whose view
    /// is being built (<see cref="Building"/>), else none.
    /// </summary>
    internal static ItemPalette? BuildingNow => _building;

    /// <summary>
    /// While the scope lives, items made outside the visual tree belong to <paramref name="palette"/>:
    /// a card holds it while it builds and attaches its view, so the items made then are drawn from
    /// the card's own layer from their first paint, and a button whose fills are template resources
    /// (set once, at build) gets the card's brushes too.
    /// </summary>
    public static BuildScope Building(ItemPalette palette)
    {
        ArgumentNullException.ThrowIfNull(palette);
        var scope = new BuildScope(_building);
        _building = palette;
        return scope;
    }

    /// <summary>Ends a <see cref="Building"/> scope, putting back the one it was nested in.</summary>
    internal readonly struct BuildScope(ItemPalette? previous) : IDisposable
    {
        public void Dispose() => _building = previous;
    }

    /// <summary>The brush for <paramref name="role"/> in <paramref name="state"/>. Prefer <see cref="ItemFill.Paint"/>, which finds the right palette.</summary>
    public Brush Get(ItemRole role, ItemState state)
    {
        if (!_brushes.TryGetValue((role, state), out var brush))
        {
            brush = new SolidColorBrush();
            Paint(brush, role, state);
            _brushes[(role, state)] = brush;
        }

        return brush;
    }

    /// <summary>
    /// Recolour every brush for <paramref name="layer"/> in the theme. Cheap when neither moved:
    /// it runs on every state change. The theme fills depend on the theme alone, so a layer and a
    /// theme that did not change leave the brushes as they are.
    /// </summary>
    public void Apply(ItemLayer layer, bool dark)
    {
        ArgumentNullException.ThrowIfNull(layer);

        if (_applied && _dark == dark && _layer.SameAs(layer))
        {
            return;
        }

        _layer = layer.Clone();
        _dark = dark;
        _applied = true;

        foreach (var ((role, state), brush) in _brushes)
        {
            Paint(brush, role, state);
        }
    }

    private void Paint(SolidColorBrush brush, ItemRole role, ItemState state)
    {
        var ink = ItemLook.Ink(role, state, _layer, _dark);

        if (ink.Colour is { } c)
        {
            brush.Color = Windows.UI.Color.FromArgb(255, c.R, c.G, c.B);
            brush.Opacity = ink.Opacity;
            return;
        }

        // The Fluent fill, value for value: its colour and its own opacity, faded by the layer's.
        if (DesignApplier.Brush(ink.ThemeKey ?? ItemLook.ThemeKey(role, state), Colors.Transparent) is SolidColorBrush theme)
        {
            brush.Color = theme.Color;
            brush.Opacity = theme.Opacity * ink.Opacity;
        }
        else
        {
            Log.Warn("items", $"'{ink.ThemeKey}' is not a plain colour; the item is drawn clear");
            brush.Color = Colors.Transparent;
            brush.Opacity = 1;
        }
    }
}

/// <summary>
/// [S-6513] The one way a widget fills an item: <see cref="Paint"/> says what the element is (its
/// <see cref="ItemRole"/>) and the state it is in, and the fill comes from the item palette of the
/// card it stands in. An element painted before it is in a card is drawn from the app-wide palette
/// and painted again from its card's as it loads, which is before its first frame.
/// </summary>
internal static class ItemFill
{
    private sealed class Entry
    {
        public ItemRole Role;
        public ItemState State;
        public bool Stroke;
        public ItemPalette? Palette;
        public bool Waiting;
    }

    private static readonly ConditionalWeakTable<FrameworkElement, Entry> Entries = [];

    /// <summary>Fill <paramref name="item"/> (a border, panel, control or shape) as <paramref name="role"/> in <paramref name="state"/>.</summary>
    public static void Paint(FrameworkElement item, ItemRole role, ItemState state = ItemState.Rest) => Set(item, role, state, stroke: false);

    /// <summary>
    /// [S-6513] A button whose fills its template takes from resources (<paramref name="prefix"/>
    /// <c>Background</c>, <c>…PointerOver</c>, <c>…Pressed</c>, <c>…Disabled</c>; the stock
    /// visual states outrank a local <c>Background</c>): the item layer's row in each state. Set
    /// once, as the button is built, from the card being built or the app-wide palette.
    /// </summary>
    public static void Button(FrameworkElement button, string prefix)
    {
        ArgumentNullException.ThrowIfNull(button);
        var palette = Find(button) ?? ItemPalette.BuildingNow ?? ItemPalette.App;

        button.Resources[prefix + "Background"] = palette.Get(ItemRole.Row, ItemState.Rest);
        button.Resources[prefix + "BackgroundPointerOver"] = palette.Get(ItemRole.Row, ItemState.Hover);
        button.Resources[prefix + "BackgroundPressed"] = palette.Get(ItemRole.Row, ItemState.Pressed);
        button.Resources[prefix + "BackgroundDisabled"] = palette.Get(ItemRole.Row, ItemState.Rest);
    }

    /// <summary>
    /// The brush for many items drawn alike (a year of dots): from the card <paramref name="context"/>
    /// (the view drawing them) stands in, the card being built, or the app-wide palette. The brush is
    /// the palette's own, so it follows the layer in place; it is not moved to another card's later.
    /// </summary>
    public static Brush For(FrameworkElement context, ItemRole role, ItemState state = ItemState.Rest)
    {
        ArgumentNullException.ThrowIfNull(context);
        return (Find(context) ?? ItemPalette.BuildingNow ?? ItemPalette.App).Get(role, state);
    }

    /// <summary>Stroke <paramref name="shape"/> as <paramref name="role"/>: a ring's track is a stroke, not a fill.</summary>
    public static void PaintStroke(Shape shape, ItemRole role, ItemState state = ItemState.Rest) => Set(shape, role, state, stroke: true);

    private static void Set(FrameworkElement item, ItemRole role, ItemState state, bool stroke)
    {
        ArgumentNullException.ThrowIfNull(item);

        var entry = Entries.GetValue(item, static _ => new Entry());
        entry.Role = role;
        entry.State = state;
        entry.Stroke = stroke;

        if (entry.Palette is null)
        {
            entry.Palette = Find(item) ?? ItemPalette.BuildingNow;

            if (entry.Palette is null && !entry.Waiting)
            {
                entry.Waiting = true;
                item.Loaded += OnLoaded;
            }
        }

        Assign(item, (entry.Palette ?? ItemPalette.App).Get(role, state), stroke);
    }

    private static void OnLoaded(object sender, RoutedEventArgs args)
    {
        if (sender is not FrameworkElement item)
        {
            return;
        }

        item.Loaded -= OnLoaded;
        if (!Entries.TryGetValue(item, out var entry))
        {
            return;
        }

        entry.Waiting = false;

        // Outside any card (a debug render's loose part): the app-wide layer, for good.
        entry.Palette = Find(item) ?? ItemPalette.App;
        Assign(item, entry.Palette.Get(entry.Role, entry.State), entry.Stroke);
    }

    /// <summary>The palette of the card <paramref name="item"/> stands in, or null while it stands in none.</summary>
    private static ItemPalette? Find(DependencyObject item)
    {
        for (DependencyObject? at = item; at is not null; at = VisualTreeHelper.GetParent(at) ?? (at as FrameworkElement)?.Parent)
        {
            if (at is IItemPaletteOwner owner)
            {
                return owner.Items;
            }
        }

        return null;
    }

    private static void Assign(FrameworkElement item, Brush brush, bool stroke)
    {
        switch (item)
        {
            case Shape shape when stroke:
                shape.Stroke = brush;
                break;
            case Shape shape:
                shape.Fill = brush;
                break;
            case Border border:
                border.Background = brush;
                break;
            case Panel panel:
                panel.Background = brush;
                break;
            case Control control:
                control.Background = brush;
                break;
            default:
                Log.Warn("items", $"a {item.GetType().Name} has no fill to paint; the item stays as it is");
                break;
        }
    }
}
