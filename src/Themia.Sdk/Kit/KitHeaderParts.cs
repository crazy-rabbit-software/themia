namespace Themia.Sdk.Kit;

/// <summary>
/// [S-7064] Which of the header's extras show: the problem glyph while there is a problem line, the
/// busy ring while a fetch is out, the count badge for a positive count. With none of them the header
/// itself is collapsed, so it takes no room and no spacing beside the widget's own trailing text.
/// </summary>
public readonly record struct KitHeaderParts(bool Problem, bool Ring, bool Badge)
{
    /// <summary>Whether the header shows at all.</summary>
    public bool Any => Problem || Ring || Badge;

    public static KitHeaderParts Of(int problems, bool busy, long? count)
        => new(problems > 0, busy, count is > 0);
}
