using Themia.Core.Localization;
namespace Themia.Core.Widgets;

/// <summary>One thing that was on the clipboard.</summary>
/// <param name="Text">Exactly what was copied. Never altered — copying it back must give the same string.</param>
/// <param name="CapturedAt">When this app saw it.</param>
public readonly record struct ClipboardEntry(string Text, DateTimeOffset CapturedAt);

/// <summary>
/// The clipboard history a widget shows, as a pure in-memory list.
/// <para>
/// This is the most sensitive data structure in the app: whatever is copied lands here, and that
/// includes passwords, recovery codes and whatever a password manager put on the clipboard three
/// seconds ago. So the rules are deliberately narrow — text only, bounded in both entries and total
/// size, <b>never written to disk</b>, and gone when the process is. There is no persistence option
/// to turn on, because a clipboard history that survives a restart is a credentials file that
/// nobody chose to create.
/// </para>
/// <para>
/// Entries are stored verbatim and displayed through <see cref="ClipboardFormatting.Preview"/>. The
/// two are separate on purpose: a truncated <i>stored</i> entry would mean clicking a row pasted
/// back something different from what was copied, which is a silent data loss, so the shortening
/// happens only where it is looked at.
/// </para>
/// </summary>
public sealed class ClipboardHistory
{
    /// <summary>The fewest entries a history may keep.</summary>
    public const int MinCapacity = 1;

    /// <summary>The most entries a history may keep.</summary>
    public const int MaxCapacity = 200;

    /// <summary>What a history holds by default.</summary>
    public const int DefaultCapacity = 50;

    /// <summary>
    /// The total characters the history holds before it starts dropping its oldest entries,
    /// whatever the entry count says.
    /// <para>
    /// A capacity of 50 sounds bounded until somebody copies a 40MB log file twice. This is the
    /// bound that actually holds: roughly two megabytes of text, after which the oldest entries go
    /// even if there is room for them by count.
    /// </para>
    /// </summary>
    public const int MaxTotalCharacters = 1_000_000;

    /// <summary>
    /// A single entry longer than this is not kept at all.
    /// <para>
    /// Not truncated — dropped. Half of a copied file is not a clipboard entry anybody wants back,
    /// and keeping it would mean a row whose click pastes something other than what it shows.
    /// </para>
    /// </summary>
    public const int MaxEntryLength = 128 * 1024;

    private readonly List<ClipboardEntry> _entries = [];

    private int _capacity = DefaultCapacity;
    private int _characters;

    /// <summary>How many entries this history keeps. Clamped into the supported range on write.</summary>
    public int Capacity
    {
        get => _capacity;
        set
        {
            _capacity = Math.Clamp(value, MinCapacity, MaxCapacity);
            Trim();
        }
    }

    /// <summary>Most recent first.</summary>
    public IReadOnlyList<ClipboardEntry> Entries => _entries;

    public int Count => _entries.Count;

    /// <summary>The text currently at the top, or null when there is nothing.</summary>
    public string? Latest => _entries.Count > 0 ? _entries[0].Text : null;

    /// <summary>
    /// Record something that was copied. Returns whether the list changed, so a caller only redraws
    /// when there is something new to draw.
    /// <para>
    /// Re-copying something already in the history <b>moves it to the top</b> rather than adding a
    /// second copy: the history is a list of distinct things, and a user who copies the same
    /// address four times wants one row, not four.
    /// </para>
    /// </summary>
    public bool Add(string? text, DateTimeOffset at)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        // Whitespace-only is what a stray selection puts on the clipboard. It is never worth a row.
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (text.Length > MaxEntryLength)
        {
            return false;
        }

        var existing = IndexOf(text);

        if (existing == 0)
        {
            // Already the newest. Re-notifying about the same clipboard contents — which Windows
            // does, several times, for a single copy in some apps — must not churn the list.
            return false;
        }

        if (existing > 0)
        {
            _entries.RemoveAt(existing);
            _entries.Insert(0, new ClipboardEntry(text, at));
            return true;
        }

        _entries.Insert(0, new ClipboardEntry(text, at));
        _characters += text.Length;
        Trim();
        return true;
    }

    /// <summary>Forget everything. The only way anything leaves this list other than ageing out.</summary>
    public bool Clear()
    {
        if (_entries.Count == 0)
        {
            return false;
        }

        _entries.Clear();
        _characters = 0;
        return true;
    }

    /// <summary>Drop one entry, for the row-level remove.</summary>
    public bool Remove(string text)
    {
        var index = IndexOf(text);

        if (index < 0)
        {
            return false;
        }

        _characters -= _entries[index].Text.Length;
        _entries.RemoveAt(index);
        return true;
    }

    private int IndexOf(string text)
    {
        for (var i = 0; i < _entries.Count; i++)
        {
            // Ordinal: two strings that differ only by case are two different things to paste.
            if (string.Equals(_entries[i].Text, text, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Enforce both bounds, oldest first.</summary>
    private void Trim()
    {
        while (_entries.Count > _capacity || (_characters > MaxTotalCharacters && _entries.Count > 1))
        {
            var last = _entries.Count - 1;
            _characters -= _entries[last].Text.Length;
            _entries.RemoveAt(last);
        }

        if (_entries.Count == 0)
        {
            _characters = 0;
        }
    }
}

/// <summary>How a clipboard entry reads in a list row.</summary>
public static class ClipboardFormatting
{
    /// <summary>
    /// A single line standing in for an entry: runs of whitespace collapsed to one space, trimmed,
    /// and cut to <paramref name="maxChars"/> with an ellipsis.
    /// <para>
    /// A row is one line high, so a copied paragraph has to become one line somewhere. Doing it
    /// here rather than by letting the text box clip means the second line of a copied address
    /// still shows, instead of a row that says the street and nothing else.
    /// </para>
    /// </summary>
    public static string Preview(string? text, int maxChars)
    {
        if (string.IsNullOrEmpty(text) || maxChars <= 0)
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder(Math.Min(text.Length, maxChars + 1));
        var pendingSpace = false;

        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                if (builder.Length >= maxChars)
                {
                    return builder.ToString() + '…';
                }

                builder.Append(' ');
                pendingSpace = false;
            }

            if (builder.Length >= maxChars)
            {
                return builder.ToString() + '…';
            }

            builder.Append(c);
        }

        return builder.ToString();
    }

    /// <summary>
    /// What an entry is, in the few words a detail column has room for: how much text, and how many
    /// lines if it is more than one. Said rather than shown, because the row cannot show it.
    /// </summary>
    public static string Describe(string? text, Locale locale)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var lines = 1;

        foreach (var c in text)
        {
            if (c == '\n')
            {
                lines++;
            }
        }

        if (lines > 1)
        {
            return Strings.Plural(locale, Str.WidgetsA_Clipboard_Lines, lines);
        }

        return Strings.Plural(locale, Str.WidgetsA_Clipboard_Characters, text.Length);
    }
}
