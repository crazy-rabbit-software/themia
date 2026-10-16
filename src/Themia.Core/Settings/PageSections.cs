namespace Themia.Core.Settings;

/// <summary>
/// [S-1752] A settings page's sections: a heading is kept only while a row follows it before the
/// next heading or the page's end, so a style that offers none of a section's rows leaves no
/// heading over nothing, and a page of headings alone is empty — no page at all ([S-1750]).
/// </summary>
public static class PageSections
{
    /// <summary>The indices to keep of a page's elements, given which of them are headings, in order.</summary>
    public static IReadOnlyList<int> Keep(IReadOnlyList<bool> isHeader)
    {
        ArgumentNullException.ThrowIfNull(isHeader);

        var keep = new List<int>(isHeader.Count);
        for (var i = 0; i < isHeader.Count; i++)
        {
            if (!isHeader[i] || (i + 1 < isHeader.Count && !isHeader[i + 1]))
            {
                keep.Add(i);
            }
        }

        return keep;
    }
}
