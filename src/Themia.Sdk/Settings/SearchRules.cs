using Themia.Core.Localization;
using Themia.Core.Widgets;

namespace Themia.Core.Settings;

/// <summary>[S-13607] What a key does in a search box or on one of its results (<see cref="SearchKeys"/>).</summary>
public enum SearchMove
{
    /// <summary>Not the search's key: it goes on to whatever holds the box.</summary>
    None,

    /// <summary>Enter in the box: the best result (the first) is taken.</summary>
    PickBest,

    /// <summary>Down in the box: the keyboard goes to the first result.</summary>
    ToFirst,

    /// <summary>Escape with words in the box (from a result too): the box is emptied and keeps the keys.</summary>
    Clear,

    /// <summary>Escape with an empty box: the search lets go (its picker, page or box closes).</summary>
    LetGo,

    /// <summary>Up on the first result: the keyboard goes back to the box.</summary>
    ToBox,
}

/// <summary>
/// [S-13607] The one key rule of every search box (Karpo's search field, docs/widgets.md §641, its
/// <c>MenuTextField</c>): Enter takes the best result, Down goes to the first; Escape empties the box, from a
/// result too, and an empty box's Escape lets go; Up from the first result goes back to the box (Backspace and
/// a letter typed on a result go back too, with what they type: that is the box's text input, not a move).
/// </summary>
public static class SearchKeys
{
    /// <summary>A key in the box, with <paramref name="typed"/> words in it and <paramref name="results"/> results under it.</summary>
    public static SearchMove InBox(WidgetKey key, bool typed, int results) => key switch
    {
        WidgetKey.Enter when results > 0 => SearchMove.PickBest,
        WidgetKey.Down when results > 0 => SearchMove.ToFirst,
        WidgetKey.Escape when typed => SearchMove.Clear,
        WidgetKey.Escape => SearchMove.LetGo,
        _ => SearchMove.None,
    };

    /// <summary>A key on the result at <paramref name="index"/> (0 the first), the box holding <paramref name="typed"/> words.</summary>
    public static SearchMove OnResult(WidgetKey key, int index, bool typed) => key switch
    {
        WidgetKey.Up when index == 0 => SearchMove.ToBox,
        WidgetKey.Escape when typed => SearchMove.Clear,
        WidgetKey.Escape => SearchMove.LetGo,
        _ => SearchMove.None,
    };
}

/// <summary>
/// [S-13608] The one status line under every lookup's results: "Searching…" while what is typed is asked,
/// else the answer's own word (no match, offline, why not), else nothing while nothing is typed and nothing
/// is listed (the placeholder says what to type), else "No results" or "N found".
/// </summary>
public static class LookupStatus
{
    public static string Line(string? typed, bool asking, int found, string? note)
    {
        if (asking)
        {
            return Str.Shell_Place_Searching.Text();
        }

        if (!string.IsNullOrWhiteSpace(note))
        {
            return note;
        }

        if (found == 0)
        {
            return string.IsNullOrWhiteSpace(typed) ? string.Empty : Str.Common_NoResults.Text();
        }

        return Str.EditBar_Page_Found.Format(("count", LocaleFormat.Integer(found)));
    }
}

/// <summary>
/// [S-13609] The answers of a page's lookups, by the lookup's key: one question out per lookup, the latest
/// typed; an answer to anything but the latest question is dropped, so a slow answer never replaces a newer
/// one (the debounce's other half: typing settles for <c>MenuSearch.Settle</c> before it is asked).
/// </summary>
public sealed class LookupAnswers<T>
    where T : class
{
    private readonly Dictionary<string, (string Typed, T? Answer)> _asked = new(StringComparer.Ordinal);

    /// <summary>Whether <paramref name="typed"/> has still to be asked for <paramref name="key"/> (never asked, or something else was).</summary>
    public bool Wants(string key, string typed)
        => !_asked.TryGetValue(key, out var asked) || !string.Equals(asked.Typed, typed, StringComparison.Ordinal);

    /// <summary><paramref name="typed"/> is asked for <paramref name="key"/>: what came for anything before it no longer counts.</summary>
    public void Asking(string key, string typed) => _asked[key] = (typed, null);

    /// <summary>The answer to <paramref name="typed"/>; false (and dropped) when a newer question was asked since.</summary>
    public bool Answered(string key, string typed, T answer)
    {
        ArgumentNullException.ThrowIfNull(answer);
        if (Wants(key, typed))
        {
            return false;
        }

        _asked[key] = (typed, answer);
        return true;
    }

    /// <summary>The answer to <paramref name="typed"/> for <paramref name="key"/>, or null while it is out (or never asked).</summary>
    public T? For(string key, string typed)
        => _asked.TryGetValue(key, out var asked) && string.Equals(asked.Typed, typed, StringComparison.Ordinal) ? asked.Answer : null;

    /// <summary>Every answer goes (the page closes).</summary>
    public void Clear() => _asked.Clear();
}
