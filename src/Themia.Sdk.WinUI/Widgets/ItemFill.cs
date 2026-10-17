using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// [S-6513] The one path an item a widget draws is filled through (a row, a tile, a chip, a track),
/// and [S-6546] the text that stands on one inked. The fills follow the card's item layer
/// ([S-6512]), which is the host's design state: the host installs its engine here once, first
/// thing, and a widget or an SDK part only ever calls these.
/// </summary>
public static class ItemFill
{
    private static IItemFillHost? _host;

    private static IItemFillHost Host
        => Volatile.Read(ref _host) ?? throw new InvalidOperationException("no item fills installed: the host installs them before any part is drawn");

    /// <summary>Fill <paramref name="item"/> (a border, panel, control or shape) as <paramref name="role"/> in <paramref name="state"/>.</summary>
    public static void Paint(FrameworkElement item, ItemRole role, ItemState state = ItemState.Rest) => Host.Paint(item, role, state);

    /// <summary>A button whose fills its template takes from resources (<paramref name="prefix"/><c>Background</c>, …): the item layer's row in each state.</summary>
    public static void Button(FrameworkElement button, string prefix) => Host.Button(button, prefix);

    /// <summary>The brush for many items drawn alike (a year of dots), from the card <paramref name="context"/> stands in.</summary>
    public static Brush For(FrameworkElement context, ItemRole role, ItemState state = ItemState.Rest) => Host.For(context, role, state);

    /// <summary>Stroke <paramref name="shape"/> as <paramref name="role"/>: a ring's track is a stroke, not a fill.</summary>
    public static void PaintStroke(Shape shape, ItemRole role, ItemState state = ItemState.Rest) => Host.PaintStroke(shape, role, state);

    /// <summary>[S-6546] <paramref name="element"/> stands over an item without being inside it: its text is inked as the item's.</summary>
    public static void OnItem(FrameworkElement element) => Host.OnItem(element);

    /// <summary><paramref name="element"/> stops being an item (a tile turned accent): its fill and text are its own again.</summary>
    public static void Release(FrameworkElement element) => Host.Release(element);

    /// <summary>The host's engine. Once per process.</summary>
    internal static void Install(IItemFillHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (Interlocked.CompareExchange(ref _host, host, null) is not null)
        {
            throw new InvalidOperationException("the item fills are installed once per process");
        }
    }
}

/// <summary>What the host's item-layer engine does for <see cref="ItemFill"/>.</summary>
public interface IItemFillHost
{
    void Paint(FrameworkElement item, ItemRole role, ItemState state);

    void Button(FrameworkElement button, string prefix);

    Brush For(FrameworkElement context, ItemRole role, ItemState state);

    void PaintStroke(Shape shape, ItemRole role, ItemState state);

    void OnItem(FrameworkElement element);

    void Release(FrameworkElement element);
}
