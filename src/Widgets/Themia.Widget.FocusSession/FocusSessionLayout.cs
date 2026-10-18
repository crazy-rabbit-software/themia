namespace Themia.Core.Widgets;

/// <summary>[S-4426] What the focus widget shows at a size.</summary>
/// <param name="Button">Start/End, with its word where <paramref name="Words"/>.</param>
/// <param name="Words">The button carries its word, not only its glyph.</param>
/// <param name="Steppers">The −/+ that change the length, while idle.</param>
/// <param name="Status">The line under the time: do not disturb, or Windows' own focus.</param>
/// <param name="Ring">The progress ring around the time.</param>
public readonly record struct FocusPlan(bool Button, bool Words, bool Steppers, bool Status, bool Ring);

/// <summary>[S-4426] The focus widget's ladder.</summary>
public static class FocusSessionLayout
{
    /// <summary>
    /// [S-4426] The ladder: the time alone on the smallest tile (a click on it starts or ends); the
    /// button from 128 high; its word, the steppers and the status line as the tile grows; the ring
    /// from medium both ways.
    /// </summary>
    public static FocusPlan Plan(WidgetArea area)
    {
        if (!area.IsMeasured || (area.HeightClass == SizeClass.Compact && area.WidthClass == SizeClass.Compact))
        {
            return new FocusPlan(Button: false, Words: false, Steppers: false, Status: false, Ring: false);
        }

        var wide = area.WidthClass >= SizeClass.Regular;
        var tall = area.HeightClass >= SizeClass.Regular;
        return new FocusPlan(
            Button: true,
            Words: wide,
            Steppers: wide || tall,
            Status: tall && wide,
            Ring: area.AtLeast(SizeClass.Expanded, SizeClass.Expanded));
    }
}
