using System.Globalization;

namespace Themia.Core.Widgets;

/// <summary>A view as a list of views sees it: its id and its name, nothing to edit.</summary>
public readonly record struct ViewSummary(string Id, string Name);

/// <summary>
/// The rules a list of views follows wherever it is shown — the view bar, the Views page: what a
/// view without an icon is drawn as, and which views a whitelist offers. Pure, so the decisions are
/// tested without a window.
/// </summary>
public static class ViewListRules
{
    /// <summary>
    /// [S-911] The letters a view without an icon is drawn as: the first letter of each of the first
    /// two words, or the first two letters of a single word, upper-cased. "?" for a blank name, so a
    /// chip is never empty.
    /// <para>
    /// Counted in text elements rather than UTF-16 units, so a name that starts with an emoji or an
    /// accented letter built from two code points is not cut in half.
    /// </para>
    /// </summary>
    public static string Initials(string? name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return "?";
        }

        var words = trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        var raw = words.Length >= 2
            ? FirstElements(words[0], 1) + FirstElements(words[1], 1)
            : FirstElements(trimmed, 2);

        return raw.ToUpperInvariant();
    }

    private static string FirstElements(string text, int count)
    {
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        var result = string.Empty;

        for (var i = 0; i < count && enumerator.MoveNext(); i++)
        {
            result += enumerator.GetTextElement();
        }

        return result;
    }

    /// <summary>
    /// [S-908] The ids a whitelist actually offers: the whitelist, or every view when it is empty or
    /// names none that exist.
    /// </summary>
    public static HashSet<string> EffectiveVisible(IReadOnlyList<ViewSummary> views, IReadOnlyCollection<string>? visible)
    {
        ArgumentNullException.ThrowIfNull(views);

        var all = new HashSet<string>(views.Select(v => v.Id), StringComparer.Ordinal);

        if (visible is null || visible.Count == 0)
        {
            return all;
        }

        var chosen = new HashSet<string>(visible.Where(all.Contains), StringComparer.Ordinal);
        return chosen.Count == 0 ? all : chosen;
    }

    /// <summary>
    /// [S-908] The whitelist after ticking or unticking one view.
    /// <para>
    /// The last view cannot be unticked — the list would offer nothing — so that returns the
    /// whitelist unchanged. Ticking every view collapses back to the empty "all" whitelist, so the
    /// list goes on following views that are added later.
    /// </para>
    /// </summary>
    public static List<string> Toggle(
        IReadOnlyList<ViewSummary> views,
        IReadOnlyCollection<string>? visible,
        string viewId,
        bool on)
    {
        ArgumentNullException.ThrowIfNull(views);

        var effective = EffectiveVisible(views, visible);

        if (on)
        {
            effective.Add(viewId);
        }
        else if (effective.Count > 1)
        {
            effective.Remove(viewId);
        }

        // Back in view order, and back to "all" when nothing is left out.
        var ordered = views.Select(v => v.Id).Where(effective.Contains).ToList();
        return ordered.Count == views.Count ? [] : ordered;
    }
}
