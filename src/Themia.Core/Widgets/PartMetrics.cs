namespace Themia.Core.Widgets;

/// <summary>
/// [S-668] [S-669] The sizes of the shared widget parts (<c>WidgetParts</c> in the app), so a
/// ladder that budgets for a part and the part that is drawn agree, in every widget that uses it.
/// <para>
/// Before these, the weather widget drew the same reading three ways — a 20px line beside the
/// number, a 28px row under it, a cell — with three icon sizes (12, 14, 16) between them.
/// </para>
/// </summary>
public static class PartMetrics
{
    /// <summary>[S-668] A stat line: glyph, label, value on one Body line, with 4px of air either side.</summary>
    public static double StatLine => FluentType.Body.LineHeight + 8;

    /// <summary>[S-668] A stat cell: a caption label over a strong body value.</summary>
    public static double StatCell => FluentType.Caption.LineHeight + FluentType.BodyStrong.LineHeight;

    /// <summary>The glyph Windows pairs with Body text, on a stat line and a day row.</summary>
    public const double LineIcon = 16;

    /// <summary>The glyph paired with a Caption label, in a stat cell.</summary>
    public const double CaptionIcon = 12;

    /// <summary>[S-669] A day row's condition glyph, a step larger than a line's: the sky is the row's picture.</summary>
    public const double DayIcon = 20;

    /// <summary>[S-669] A day row's weekday column beside the glyph: "Today", "Wed", "Do.".</summary>
    public const double DayLabelWidth = 44;

    /// <summary>[S-669] A day row's figure column ("-18°"), so the lows and highs line up down the list.</summary>
    public const double FigureWidth = 32;
}
