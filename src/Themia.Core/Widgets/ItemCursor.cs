namespace Themia.Core.Widgets;

/// <summary>
/// [S-1457] The keyboard cursor of a one-dimensional list of items (rows, faces): where an arrow,
/// Home or End moves it. Pure, so the shell's keyboard lease — which decides <i>whether</i> a widget
/// gets a key — only has to call a view's <c>HandleKey</c>, and the movement itself is tested here.
/// </summary>
public static class ItemCursor
{
    /// <summary>
    /// The cursor after <paramref name="key"/>, or null for none. Up/Left step back, Down/Right step
    /// forward, both clamped (a list is not a carousel); Home/End jump to the ends; Escape clears.
    /// With no cursor yet, a forward step or Home lands on the first item, a back step or End on the
    /// last. Enter never moves it (the caller acts on the item). An empty list has no cursor.
    /// [S-1985] Page Up / Page Down jump <see cref="ListKeys.PageStep"/> items, clamped, the same
    /// distance as every <see cref="ListKeys"/> list; with no cursor yet they land on the first item.
    /// </summary>
    public static int? Move(int count, int? current, WidgetKey key)
    {
        if (count <= 0)
        {
            return null;
        }

        int? at = current is { } index && index >= 0 && index < count ? index : null;

        return key switch
        {
            WidgetKey.Escape => null,
            WidgetKey.Home => 0,
            WidgetKey.End => count - 1,
            WidgetKey.Up or WidgetKey.Left => at is { } i ? Math.Max(0, i - 1) : count - 1,
            WidgetKey.Down or WidgetKey.Right => at is { } i ? Math.Min(count - 1, i + 1) : 0,
            WidgetKey.PageUp => at is { } i ? Math.Max(0, i - ListKeys.PageStep) : 0,
            WidgetKey.PageDown => at is { } i ? Math.Min(count - 1, i + ListKeys.PageStep) : 0,
            _ => at,
        };
    }
}
