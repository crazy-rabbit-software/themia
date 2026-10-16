using System.Runtime.CompilerServices;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
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
/// from an item layer (Core <see cref="ItemLook"/>); [S-6546] and the text brushes of what is drawn
/// on the items, one per text tier, from the item text colour ([S-6540]). Every card owns one, so a widget's own layer
/// ([S-6512]) reaches its items and no other widget's; <see cref="App"/> is the app-wide layer, for an
/// item painted before it is in a card. A brush handed out stays the same object for the palette's
/// life, so nothing that holds one has to be told the layer changed. UI thread only.
/// </summary>
internal sealed class ItemPalette
{
    /// <summary>Every palette's text brushes, by the tier each stands for: so a brush can be told for one.</summary>
    private static readonly ConditionalWeakTable<Brush, object> TextTiers = [];

    private readonly Dictionary<(ItemRole Role, ItemState State), SolidColorBrush> _brushes = [];
    private readonly Dictionary<ItemTextTier, SolidColorBrush> _text = [];
    private FillChoice _layer = new();
    private string _textColour = string.Empty;
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
    /// [S-6546] The brush text of <paramref name="tier"/> on this palette's items is drawn in. Prefer
    /// <see cref="ItemFill.Paint"/>, which inks what stands on an item through it.
    /// </summary>
    public Brush Text(ItemTextTier tier)
    {
        if (!_text.TryGetValue(tier, out var brush))
        {
            brush = new SolidColorBrush();
            PaintText(brush, tier);
            _text[tier] = brush;
            TextTiers.AddOrUpdate(brush, tier);
        }

        return brush;
    }

    /// <summary>The tier <paramref name="brush"/> stands for when it is a palette's text brush (any palette's).</summary>
    internal static ItemTextTier? TierOf(Brush brush) => TextTiers.TryGetValue(brush, out var tier) ? (ItemTextTier)tier : null;

    /// <summary>
    /// Recolour every brush for <paramref name="layer"/> and the item text colour
    /// <paramref name="textColour"/> (empty: the theme's text, [S-6541]) in the theme. Cheap when
    /// none moved: it runs on every state change. The theme fills depend on the theme alone, so a
    /// layer, a text colour and a theme that did not change leave the brushes as they are.
    /// </summary>
    public void Apply(FillChoice layer, string textColour, bool dark)
    {
        ArgumentNullException.ThrowIfNull(layer);
        textColour = HexColor.Normalise(textColour);

        if (_applied && _dark == dark && _layer.SameAs(layer) && string.Equals(_textColour, textColour, StringComparison.Ordinal))
        {
            return;
        }

        _layer = layer.Clone();
        _textColour = textColour;
        _dark = dark;
        _applied = true;

        foreach (var ((role, state), brush) in _brushes)
        {
            Paint(brush, role, state);
        }

        foreach (var (tier, brush) in _text)
        {
            PaintText(brush, tier);
        }
    }

    private void PaintText(SolidColorBrush brush, ItemTextTier tier)
    {
        var ink = ItemLook.TextInk(tier, _textColour, _dark);

        if (ink.Colour is { } c)
        {
            brush.Color = Windows.UI.Color.FromArgb(255, c.R, c.G, c.B);
            brush.Opacity = ink.Opacity;
            return;
        }

        // The theme's text colour, value for value, so text on an item reads as it did before.
        if (DesignApplier.Brush(ink.ThemeKey ?? ItemLook.TextThemeKey(tier), Colors.Gray) is SolidColorBrush theme)
        {
            brush.Color = theme.Color;
            brush.Opacity = theme.Opacity;
        }
        else
        {
            Log.Warn("items", $"'{ink.ThemeKey}' is not a plain colour; item text is drawn grey");
            brush.Color = Colors.Gray;
            brush.Opacity = 1;
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

        // The Fluent fill, value for value: its colour and its own opacity, times the ink's (0 for a transparent rest).
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
/// <para>
/// [S-6546] The same call inks what is drawn on the item: a painted item (any role but a track) is
/// an item from then on, and every text, glyph and shape inside it drawn in one of the theme's text
/// colours (<c>TextFillColorPrimary/Secondary/Tertiary/Disabled</c>) is drawn in its palette's text
/// brush of that tier instead, so the item text colour ([S-6540]) reaches it. A view keeps
/// choosing its text colours by the theme's names: whatever it assigns later is inked again as it
/// is assigned, and new text is inked when its item is painted again or loads. The accent, the
/// on-accent text, status and the user's own colours are never touched. What stands over an item
/// without being inside it says so with <see cref="OnItem"/>; an item that stops being one (a tile
/// turned accent, a look without a tile) says so with <see cref="Release"/>.
/// </para>
/// </summary>
internal static class ItemFill
{
    private const string Scope = "items";

    /// <summary>The theme text tiers, in the order the inks are tried.</summary>
    private static readonly ItemTextTier[] Tiers = Enum.GetValues<ItemTextTier>();

    /// <summary>[S-6546] The elements that are items (or stand on one): what is inside them is inked.</summary>
    private static readonly ConditionalWeakTable<FrameworkElement, object> Items = [];

    /// <summary>The elements whose text colour is watched, so a colour a view sets later is inked too.</summary>
    private static readonly ConditionalWeakTable<DependencyObject, object> Watched = [];

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

        // [S-6546] Its text is the item text, in each state its fill is the row's.
        button.Resources[prefix + "Foreground"] = palette.Text(ItemTextTier.Primary);
        button.Resources[prefix + "ForegroundPointerOver"] = palette.Text(ItemTextTier.Primary);
        button.Resources[prefix + "ForegroundPressed"] = palette.Text(ItemTextTier.Secondary);
        button.Resources[prefix + "ForegroundDisabled"] = palette.Text(ItemTextTier.Disabled);
        Mark(button);
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

        // [S-6546] A track is what a value is drawn over, never what text stands on.
        if (!stroke && role != ItemRole.Track)
        {
            Mark(item);
        }
    }

    /// <summary>
    /// [S-6546] <paramref name="element"/> stands on an item without being inside it (an icon laid
    /// over a round button's circle): it and what is inside it are inked as the item's text.
    /// </summary>
    public static void OnItem(FrameworkElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        Mark(element);
    }

    /// <summary>
    /// [S-6546] <paramref name="element"/> is no longer an item, nor on one (a toggle's tile turned
    /// accent, a round look drawing no tile): what is inside it goes back to the theme's text.
    /// </summary>
    public static void Release(FrameworkElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (Items.Remove(element))
        {
            Ink(element);
        }
    }

    private static void Mark(FrameworkElement item)
    {
        if (!Items.TryGetValue(item, out _))
        {
            Items.Add(item, item);

            // Content put in while the item was out of the tree is inked as it comes in.
            item.Loaded += OnItemLoaded;
        }

        Ink(item);
    }

    private static void OnItemLoaded(object sender, RoutedEventArgs args)
    {
        if (sender is FrameworkElement item && Items.TryGetValue(item, out _))
        {
            Ink(item);
        }
    }

    /// <summary>Ink <paramref name="node"/> and everything inside it, by its logical content (never a template's parts).</summary>
    private static void Ink(DependencyObject node)
    {
        Watch(node);

        switch (node)
        {
            case Border { Child: { } child }:
                Ink(child);
                break;
            case Panel panel:
                foreach (var child in panel.Children)
                {
                    Ink(child);
                }

                break;
            case Viewbox { Child: { } box }:
                Ink(box);
                break;
            case UserControl { Content: { } inner }:
                Ink(inner);
                break;
            case ContentControl { Content: DependencyObject content }:
                Ink(content);
                break;
            case ContentPresenter { Content: DependencyObject presented }:
                Ink(presented);
                break;
            case TextBlock text:
                // A run's colour goes with its block's: it has no place in the tree to look up from.
                var palette = PaletteAbove(text);
                foreach (var inline in text.Inlines)
                {
                    Retint(inline, TextElement.ForegroundProperty, palette);
                }

                break;
        }
    }

    /// <summary>The colour properties of <paramref name="node"/> that can hold a text colour.</summary>
    private static DependencyProperty[] InkProperties(DependencyObject node) => node switch
    {
        TextBlock => [TextBlock.ForegroundProperty],
        RichTextBlock => [RichTextBlock.ForegroundProperty],
        IconElement => [IconElement.ForegroundProperty],
        Control => [Control.ForegroundProperty],
        ContentPresenter => [ContentPresenter.ForegroundProperty],
        Shape => [Shape.FillProperty, Shape.StrokeProperty],
        _ => [],
    };

    /// <summary>Ink the node's colours now, and again whenever the view sets one.</summary>
    private static void Watch(DependencyObject node)
    {
        var properties = InkProperties(node);
        if (properties.Length == 0)
        {
            return;
        }

        if (!Watched.TryGetValue(node, out _))
        {
            Watched.Add(node, node);
            foreach (var property in properties)
            {
                node.RegisterPropertyChangedCallback(property, OnColourSet);
            }
        }

        var palette = PaletteAbove(node);
        foreach (var property in properties)
        {
            Retint(node, property, palette);
        }
    }

    private static void OnColourSet(DependencyObject sender, DependencyProperty property)
    {
        try
        {
            Retint(sender, property, PaletteAbove(sender));
        }
        catch (Exception ex)
        {
            // A colour that cannot be inked stays as the view set it: legible, only not the item's.
            Log.Error(Scope, "inking a colour on an item failed; it stays as set", ex);
        }
    }

    /// <summary>
    /// [S-6546] A text colour of the theme's, or of any palette's, becomes the same tier of
    /// <paramref name="palette"/> (null: not on an item, so the theme's own). Anything else is left.
    /// </summary>
    private static void Retint(DependencyObject node, DependencyProperty property, ItemPalette? palette)
    {
        if (node.GetValue(property) is not Brush current || TierOf(current) is not { } tier)
        {
            return;
        }

        var target = palette?.Text(tier) ?? DesignApplier.Brush(ItemLook.TextThemeKey(tier), Colors.Gray);
        if (!ReferenceEquals(current, target))
        {
            node.SetValue(property, target);
        }
    }

    private static ItemTextTier? TierOf(Brush brush)
    {
        if (ItemPalette.TierOf(brush) is { } own)
        {
            return own;
        }

        foreach (var tier in Tiers)
        {
            if (ReferenceEquals(brush, DesignApplier.Brush(ItemLook.TextThemeKey(tier), Colors.Gray)))
            {
                return tier;
            }
        }

        return null;
    }

    /// <summary>
    /// The palette whose item <paramref name="node"/> is drawn on: the nearest item at or above it,
    /// short of its card, or null when it stands on none (the card's own text).
    /// </summary>
    private static ItemPalette? PaletteAbove(DependencyObject node)
    {
        for (DependencyObject? at = node; at is not null and not IItemPaletteOwner; at = VisualTreeHelper.GetParent(at) ?? (at as FrameworkElement)?.Parent)
        {
            if (at is FrameworkElement element && Items.TryGetValue(element, out _))
            {
                return (Entries.TryGetValue(element, out var entry) ? entry.Palette : null)
                    ?? Find(element) ?? ItemPalette.BuildingNow ?? ItemPalette.App;
            }
        }

        return null;
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

        // [S-6546] Its text, from the card's palette now that it is known.
        if (Items.TryGetValue(item, out _))
        {
            Ink(item);
        }
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
