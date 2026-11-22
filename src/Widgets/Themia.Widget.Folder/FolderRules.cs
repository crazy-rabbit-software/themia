using Themia.Core.Model;

namespace Themia.Core.Widgets;

/// <summary>
/// The order a folder listing is drawn in, as a pure function of the entries and the settings.
/// <para>
/// Separate from the enumeration so it can be tested against a fixed list rather than against
/// whatever happens to be on the disk of whoever runs the suite. The enumeration is disk work and
/// belongs on a background thread; this is a comparison and belongs where it can be checked.
/// </para>
/// </summary>
public static class FolderRules
{
    /// <summary>
    /// [S-15777] Where the list scrolls to bring item <paramref name="index"/> into sight (items <paramref name="extent"/>
    /// apart, <paramref name="columns"/> a line, a viewport <paramref name="viewport"/> tall now at <paramref name="offset"/>):
    /// on a whole line, so the first line in sight stands whole from the list's top and only the last one is cut by the
    /// card's foot (WR-4: "the last item will be cutoff"); the least scroll that shows the item's line whole, or null where
    /// it is in sight whole already. A line taller than the viewport stands from its top.
    /// </summary>
    public static double? ScrollTo(int index, int columns, double extent, double offset, double viewport)
    {
        if (index < 0 || !double.IsFinite(extent) || extent <= 0 || !double.IsFinite(viewport) || viewport <= 0)
        {
            return null;
        }

        var line = index / Math.Max(1, columns);
        var top = line * extent;
        var bottom = top + extent;
        if (top >= offset - LayoutMath.Tolerance && bottom <= offset + viewport + LayoutMath.Tolerance)
        {
            return null;
        }

        if (top < offset)
        {
            return top;
        }

        // The fewest whole lines scrolled that bring its foot into the viewport, never past its top.
        var lines = Math.Ceiling(((bottom - viewport) / extent) - LayoutMath.Tolerance);
        return Math.Min(top, Math.Max(0, lines) * extent);
    }

    /// <summary>
    /// Filter, order and cap a listing, the way Explorer would.
    /// </summary>
    /// <param name="entries">Everything the enumeration returned, in whatever order it returned it.</param>
    /// <param name="sort">Which column orders the list.</param>
    /// <param name="descending">Reverse the chosen column. Never reverses the folders-first grouping.</param>
    /// <param name="showHidden">Include entries Windows marks hidden or system.</param>
    /// <param name="maxCount">How many rows to keep. At least one; a cap of nothing is a blank tile.</param>
    /// <remarks>[S-4121] Folders always come first, as in Explorer: the switch for it is gone.</remarks>
    public static IReadOnlyList<FolderEntry> Arrange(
        IReadOnlyList<FolderEntry> entries,
        FolderSort sort,
        bool descending,
        bool showHidden,
        int maxCount)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var cap = Math.Max(1, maxCount);
        var comparer = new EntryComparer(sort, descending);

        // OrderBy is a stable sort, so two entries this comparer calls equal keep the order the
        // enumeration gave them. That matters: an unstable sort would let a tick that changed
        // nothing reshuffle rows under the pointer.
        return
        [
            .. entries
                .Where(entry => showHidden || !entry.Hidden)
                .OrderBy(entry => entry, comparer)
                .Take(cap),
        ];
    }

    /// <summary>
    /// Whether two arranged listings would draw the same rows.
    /// <para>
    /// The widget re-reads its folder on a slow timer, and most of those reads find exactly what
    /// the last one found. Rebuilding the list anyway would throw away the scroll position and the
    /// row under the pointer every time, which on a long listing is the widget scrolling itself
    /// back to the top while somebody is reading it.
    /// </para>
    /// </summary>
    public static bool SameContent(IReadOnlyList<FolderEntry> left, IReadOnlyList<FolderEntry> right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left.Count != right.Count)
        {
            return false;
        }

        for (var i = 0; i < left.Count; i++)
        {
            // FolderEntry is a record struct, so this compares every field a row draws — including
            // the size and the timestamp, which is what makes an edited file redraw.
            if (left[i] != right[i])
            {
                return false;
            }
        }

        return true;
    }

    private sealed class EntryComparer(FolderSort sort, bool descending) : IComparer<FolderEntry>
    {
        public int Compare(FolderEntry x, FolderEntry y)
        {
            // Grouping first, and outside the direction: Explorer keeps folders on top whether the
            // sort runs up or down, because the grouping is not part of the column being sorted.
            if (x.IsDirectory != y.IsDirectory)
            {
                return x.IsDirectory ? -1 : 1;
            }

            var direction = descending ? -1 : 1;

            var primary = sort switch
            {
                FolderSort.Modified => x.Modified.CompareTo(y.Modified),
                FolderSort.Size => x.Size.CompareTo(y.Size),
                _ => CompareNames(x.Name, y.Name),
            };

            if (primary != 0)
            {
                return primary * direction;
            }

            // Two files of the same size, or written in the same second, are ordered by name —
            // ascending whichever way the column runs, because the tie-break is not the column.
            // Name sort has already compared names, so there is nothing further to try and equal
            // entries fall through to the stable order.
            return sort == FolderSort.Name ? 0 : CompareNames(x.Name, y.Name);
        }

        /// <summary>
        /// Case-insensitive first, so "apple" and "Banana" read alphabetically rather than in code
        /// point order, then case-sensitive so names differing only in case have one fixed order
        /// instead of depending on the enumeration.
        /// </summary>
        private static int CompareNames(string x, string y)
        {
            var insensitive = CompareNatural(x, y);
            return insensitive != 0 ? insensitive : string.CompareOrdinal(x, y);
        }
    }

    /// <summary>
    /// [S-1338] Explorer's name order (its StrCmpLogicalW, issue 194): a run of digits compares by
    /// its number, so "Week 2" comes before "Week 10", and everything else compares ignoring case.
    /// Written out because the app runs with invariant globalization, where culture compares are
    /// ordinal. Runs equal in value ("7" and "007") tie here; the caller's ordinal tie-break orders them.
    /// </summary>
    internal static int CompareNatural(string x, string y)
    {
        int i = 0, j = 0;
        while (i < x.Length && j < y.Length)
        {
            if (char.IsAsciiDigit(x[i]) && char.IsAsciiDigit(y[j]))
            {
                int iEnd = i, jEnd = j;
                while (iEnd < x.Length && char.IsAsciiDigit(x[iEnd])) iEnd++;
                while (jEnd < y.Length && char.IsAsciiDigit(y[jEnd])) jEnd++;

                // Compare the values without parsing (a run can be longer than any integer):
                // drop leading zeros, then the longer run is the larger, else digit by digit.
                var a = x.AsSpan(i, iEnd - i).TrimStart('0');
                var b = y.AsSpan(j, jEnd - j).TrimStart('0');
                var byValue = a.Length != b.Length ? a.Length.CompareTo(b.Length) : a.SequenceCompareTo(b);
                if (byValue != 0)
                {
                    return Math.Sign(byValue);
                }

                i = iEnd;
                j = jEnd;
                continue;
            }

            var byChar = char.ToUpperInvariant(x[i]).CompareTo(char.ToUpperInvariant(y[j]));
            if (byChar != 0)
            {
                return Math.Sign(byChar);
            }

            i++;
            j++;
        }

        return (x.Length - i).CompareTo(y.Length - j);
    }
}
