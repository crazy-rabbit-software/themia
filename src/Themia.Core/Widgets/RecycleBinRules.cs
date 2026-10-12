using Themia.Core.Localization;

namespace Themia.Core.Widgets;

/// <summary>[S-4862] What the Recycle Bin widget shows at one size.</summary>
/// <param name="Glyph">The bin's icon, in DIPs (0: none).</param>
/// <param name="Side">The icon and the button stand beside the text (a strip) rather than over and under it.</param>
/// <param name="Count">The item count under the size.</param>
/// <param name="Button">The Empty button.</param>
/// <param name="Large">The size in TitleLarge (a tile with height to spare), else Subtitle.</param>
public readonly record struct RecycleBinPlan(double Glyph, bool Side, bool Count, bool Button, bool Large = false);

/// <summary>
/// [S-4862] The Recycle Bin ladder. The size is the must-show part; then the item count, the Empty
/// button, and the bin's icon over them all, as the height allows. A strip puts the icon at the left
/// and Empty at the right. Below the button's rung Empty stays in the card menu. From
/// <see cref="LargeFrom"/> of height the size and the icon are drawn larger.
/// </summary>
public static class RecycleBinLayout
{
    /// <summary>The Empty button (a standard Windows button).</summary>
    public const double ButtonHeight = 32;

    /// <summary>The Empty button's width beside the text on a strip.</summary>
    public const double ButtonWidth = 96;

    /// <summary>The icon over the text on a tall tile, and beside it on a strip.</summary>
    public const double LargeGlyph = 40;

    public const double Gap = 8;

    /// <summary>The icon on a large tile.</summary>
    public const double HugeGlyph = 72;

    /// <summary>The height from which the size and the icon are drawn larger.</summary>
    public const double LargeFrom = 300;

    /// <summary>The step the size is set in at a size.</summary>
    public static TypeStep SizeStepFor(bool large) => large ? FluentType.TitleLarge : FluentType.Subtitle;

    public static RecycleBinPlan Plan(WidgetArea area)
    {
        if (!area.IsMeasured)
        {
            return new RecycleBinPlan(0, false, false, false);
        }

        if (area.Shape == AreaShape.Wide && area.WidthClass >= SizeClass.Regular)
        {
            var text = SizeStepFor(false).LineHeight + FluentType.Caption.LineHeight;
            var room = new Disclosure(area.Width - LargeGlyph - ButtonWidth - (2 * Gap), area.Height);
            room.Reserve(SizeStepFor(false).LineHeight);
            var count = room.Take(FluentType.Caption.LineHeight);
            var button = ButtonHeight <= area.Height + Disclosure.Tolerance;
            var glyph = text <= area.Height + Disclosure.Tolerance ? LargeGlyph : 0;
            return new RecycleBinPlan(glyph, true, count, button);
        }

        var large = area.Height >= LargeFrom;
        var stack = new Disclosure(area, Gap);
        stack.Reserve(SizeStepFor(large).LineHeight);
        var shown = stack.Take(FluentType.Caption.LineHeight);
        var empty = stack.Take(ButtonHeight);
        var iconSize = large ? HugeGlyph : LargeGlyph;
        var icon = stack.Take(iconSize) ? iconSize : 0;
        return new RecycleBinPlan(icon, false, shown, empty, large);
    }

    /// <summary>[S-4861] "12 items" / "Empty", in <paramref name="locale"/>; null before the first answer.</summary>
    public static string? CountText(Locale locale, RecycleBinState? state)
        => state switch
        {
            null => null,
            { IsEmpty: true } => Strings.Get(locale, Str.RecycleBin_IsEmpty),
            { } bin => Strings.Plural(locale, Str.RecycleBin_Items, bin.Items),
        };

    /// <summary>[S-4861] The size line: the bytes, "0 bytes" when empty, "–" before the first answer.</summary>
    public static string SizeText(Locale locale, RecycleBinState? state)
        => state is { } bin ? Formatting.Bytes(locale, bin.Bytes) : "–";
}
