namespace Themia.Core.Settings;

/// <summary>
/// [S-2351] One line of text or one question asked **in place** (owner, 2026-11-20: "fence name setting opens a new
/// subpage where i can enter the name, this should be a section not an own page. this type of settings component should
/// not exist anywhere in themia settings"): the section a page has open, named by the key of what asked it (an asking
/// row's, or a tool's at a card's head), and the rules it keeps. Its card opens it as its one inner level; no page of its
/// own is ever opened for one input. Pure: the page draws what this says.
/// </summary>
public sealed class AskInPlace
{
    /// <summary>The key of the one section open on the page; null while none is.</summary>
    public string? Open { get; private set; }

    /// <summary>Whether the section of <paramref name="key"/> is the one open.</summary>
    public bool IsOpen(string key) => Open is not null && string.Equals(Open, key, StringComparison.Ordinal);

    /// <summary>
    /// A click on what asks (its row's head, its tool): its section opens, any other closing without keeping anything
    /// (one at a time); a click on it while it is open closes it, keeping nothing. True when it is open after.
    /// </summary>
    public bool Toggle(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        Open = IsOpen(key) ? null : key;
        return Open is not null;
    }

    /// <summary>Opened by what asked it after a step of its own (an import's question once its file is read); any other closes.</summary>
    public void Show(string key) => Open = key ?? throw new ArgumentNullException(nameof(key));

    /// <summary>
    /// Escape, a press elsewhere, another card opened, a tab, the page going: the section closes and nothing is kept.
    /// False where none was open.
    /// </summary>
    public bool Cancel()
    {
        var was = Open is not null;
        Open = null;
        return was;
    }

    /// <summary>
    /// A press on the page while a section is open: inside what holds the section (its row, its card's head) nothing
    /// changes; anywhere else it closes, nothing kept. True where it closed.
    /// </summary>
    public bool PressedOutside(bool inside) => !inside && Cancel();

    /// <summary>
    /// [S-3721] Why <paramref name="typed"/> (trimmed) cannot be given: empty words for an empty box that may not be
    /// empty (the accept greyed, nothing said while nothing is typed), else what <paramref name="refuse"/> says; null
    /// where it can be given.
    /// </summary>
    public static string? Reason(string typed, bool allowEmpty, Func<string, string?>? refuse)
    {
        var text = (typed ?? string.Empty).Trim();
        return !allowEmpty && text.Length == 0 ? string.Empty : refuse?.Invoke(text);
    }

    /// <summary>
    /// Enter, or the accept: the section of <paramref name="key"/> gives what is typed (trimmed) where it can be given,
    /// and closes; refused, it stays open with why. Nothing is given for a section that is not the one open.
    /// </summary>
    public AskAnswer Accept(string key, string typed, bool allowEmpty, Func<string, string?>? refuse)
    {
        if (!IsOpen(key))
        {
            return new AskAnswer(false, string.Empty, null);
        }

        var text = (typed ?? string.Empty).Trim();
        if (Reason(text, allowEmpty, refuse) is { } why)
        {
            return new AskAnswer(false, text, why);
        }

        Open = null;
        return new AskAnswer(true, text, null);
    }

    /// <summary>A question's answer (its action): it closes; true where it was the one open, so only then is anything done.</summary>
    public bool Answer(string key)
    {
        if (!IsOpen(key))
        {
            return false;
        }

        Open = null;
        return true;
    }
}

/// <summary>[S-2351] What an accept did: whether the text was given, the text (trimmed), and why not (empty words: nothing said).</summary>
public readonly record struct AskAnswer(bool Kept, string Text, string? Why);
