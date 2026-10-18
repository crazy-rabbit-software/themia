namespace Themia.Sdk.Kit;

/// <summary>
/// [S-7064] Which of the header's extras show: the problem glyph while there is a problem line, the
/// busy ring while a fetch is out, the widget's own view buttons (Mail's compose and sort) that fit,
/// the count badge for a positive count. With none of them the header itself is collapsed, so it
/// takes no room and no spacing beside the widget's own trailing text.
/// </summary>
public readonly record struct KitHeaderParts(bool Problem, bool Ring, bool Badge, bool Buttons = false)
{
    /// <summary>Whether the header shows at all.</summary>
    public bool Any => Problem || Ring || Badge || Buttons;

    public static KitHeaderParts Of(int problems, bool busy, long? count, int buttons = 0)
        => new(problems > 0, busy, count is > 0, buttons > 0);

    /// <summary>[S-7064] A view button shows from its width on, so it never squeezes the title to a letter; none before the card is measured.</summary>
    public static bool Fits(double needsWidth, double width) => width >= needsWidth;
}
