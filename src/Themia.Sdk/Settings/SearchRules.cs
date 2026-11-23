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
/// [S-16409] What a change of a lookup's box does (owner, 2026-11-23: "when searching for a place it can go into an infinite
/// reload loop"): only the box the page shows now types, and only words that differ from what the page holds. A page draws
/// its lookup's box anew on every answer, with the words it holds; the box it replaced, and the one drawn anew, raise their
/// words later, after the page's next drawing. Taken as typing, the old box's words and the new box's flipped what was typed
/// between the two for ever: every flip drew the box anew and asked again, and no answer stayed.
/// </summary>
public static class LookupTyping
{
    /// <summary>
    /// Whether the words <paramref name="text"/> a box raised are typing over what the page holds (<paramref name="held"/>):
    /// the box is the one it <paramref name="shows"/> now, and they differ. A box the page has drawn over says nothing.
    /// </summary>
    public static bool Types(bool shows, string text, string? held) => shows && !string.Equals(text ?? string.Empty, held ?? string.Empty, StringComparison.Ordinal);
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

    /// <summary>
    /// [S-16409] The question <paramref name="typed"/> for <paramref name="key"/> went unanswered (given up, or cut short by the
    /// page): forgotten while it is still the latest, so the lookup asks it again rather than saying "Searching…" for good.
    /// </summary>
    public void Drop(string key, string typed)
    {
        if (_asked.TryGetValue(key, out var asked) && asked.Answer is null && string.Equals(asked.Typed, typed, StringComparison.Ordinal))
        {
            _asked.Remove(key);
        }
    }

    /// <summary>The answer to <paramref name="typed"/> for <paramref name="key"/>, or null while it is out (or never asked).</summary>
    public T? For(string key, string typed)
        => _asked.TryGetValue(key, out var asked) && string.Equals(asked.Typed, typed, StringComparison.Ordinal) ? asked.Answer : null;

    /// <summary>
    /// [S-16720] The answer to <paramref name="typed"/> for <paramref name="key"/> changed in place (a pick dots its row):
    /// false when it is not the answer held.
    /// </summary>
    public bool Change(string key, string typed, Func<T, T> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (For(key, typed) is not { } answer)
        {
            return false;
        }

        _asked[key] = (typed, change(answer));
        return true;
    }

    /// <summary>Every answer goes (the page closes).</summary>
    public void Clear() => _asked.Clear();
}

/// <summary>
/// [S-16720] A pick among a lookup's answers (owner, 2026-11-23: "when i click a city like Gais in the dropdown select once
/// it does select it in the background but it does not show the blue selected dot"). An answer says which row is in force
/// when it comes; a pick writes the setting and the page is drawn again from the answer it holds, so the answer itself
/// moves its dot to the row picked: that one holds, no other.
/// </summary>
public static class SearchPicks
{
    /// <summary><paramref name="answer"/> with the choice at <paramref name="index"/> of its lines holding and every other choice not.</summary>
    public static SettingsSearchAnswer Picked(SettingsSearchAnswer answer, int index)
    {
        ArgumentNullException.ThrowIfNull(answer);
        if (index < 0 || index >= answer.Lines.Count || answer.Lines[index] is not SettingsRadio)
        {
            return answer;
        }

        return answer with
        {
            Lines = [.. answer.Lines.Select((line, at) => line is SettingsRadio radio && radio.Holds != (at == index) ? radio with { Holds = at == index } : line)],
        };
    }
}
