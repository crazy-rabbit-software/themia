using Themia.App.Widgets;

namespace Themia.Sdk.WinUI;

/// <summary>
/// [S-3719] What a module's own lines on the properties menu ([S-3716]) are built into: the lines of
/// the menu's first level (<see cref="Lines"/>), the lines under its Style submenu after the styles
/// (<see cref="StyleLines"/>: what only the look draws with), where the card's own lines go
/// (<see cref="PlaceCard"/>), and the save to call after a change. The lines are the one menu
/// vocabulary (<see cref="ItemMenuEntry"/>): a switch, a choice of one (a submenu of dots), a
/// submenu, a search, a command. The host builds them again after every change, so a line says what
/// the widget holds now and a closure may keep the value it was built with.
/// </summary>
public sealed class WidgetMenuLines
{
    private readonly Action _save;
    private readonly Action<WidgetTextAsk> _ask;

    public WidgetMenuLines(Action save, Action<WidgetTextAsk> ask)
    {
        _save = save ?? throw new ArgumentNullException(nameof(save));
        _ask = ask ?? throw new ArgumentNullException(nameof(ask));
    }

    /// <summary>The menu's first level, after Style.</summary>
    public List<ItemMenuEntry> Lines { get; } = [];

    /// <summary>Under Style, after the styles and a divider: the options only the current look draws with.</summary>
    public List<ItemMenuEntry> StyleLines { get; } = [];

    /// <summary>
    /// [S-3723] Where in <see cref="Lines"/> the card's own lines (header, alignment, the colours,
    /// scale) stand, as one Appearance submenu; null (nothing placed) leaves them as their own group
    /// after the widget's lines.
    /// </summary>
    public int? CardAt { get; private set; }

    /// <summary>[S-3723] The card's own lines go here, as one Appearance submenu among the widget's.</summary>
    public void PlaceCard() => CardAt = Lines.Count;

    /// <summary>Store a change made to the config in place, as a settings page stores one.</summary>
    public void Save() => _save();

    /// <summary>[S-3721] Ask for one line of text beside the menu (a web address, a path, a name).</summary>
    public void Ask(WidgetTextAsk ask)
    {
        ArgumentNullException.ThrowIfNull(ask);
        _ask(ask);
    }

    /// <summary>
    /// A switch: ticked while on; a click turns it the other way and saves. Under a submenu it is
    /// best without a <paramref name="glyph"/>: Explorer's switches are a tick and words.
    /// </summary>
    public ItemMenuEntry Toggle(string? glyph, string text, bool on, Action<bool> set)
    {
        ArgumentNullException.ThrowIfNull(set);
        return new(glyph, text, () =>
        {
            set(!on);
            _save();
        }, Checked: on);
    }

    /// <summary>
    /// [S-3718] A choice of one: a submenu of the options, the current one with Windows' dot. Picking
    /// the current one changes nothing and stores nothing.
    /// </summary>
    public ItemMenuEntry Choice<T>(string? glyph, string text, IReadOnlyList<(T Value, string Text)> options, T current, Action<T> set)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(set);

        var lines = new List<ItemMenuEntry>(options.Count);
        foreach (var (value, label) in options)
        {
            var isCurrent = EqualityComparer<T>.Default.Equals(value, current);
            lines.Add(new(null, label, () =>
            {
                if (!isCurrent)
                {
                    set(value);
                    _save();
                }
            }, Checked: isCurrent, Group: text));
        }

        return Submenu(glyph, text, lines);
    }

    /// <summary>A command that changes the config and saves: one step.</summary>
    public ItemMenuEntry Command(string? glyph, string text, Action change, bool disabled = false)
    {
        ArgumentNullException.ThrowIfNull(change);
        return new(glyph, text, () =>
        {
            change();
            _save();
        }, Disabled: disabled);
    }

    /// <summary>
    /// [S-3722] A search: its line shows <paramref name="value"/> (what is chosen) and opens a
    /// submenu with a box on top, the answers as lines under it and <see cref="MenuSearch.Fixed"/>
    /// after them.
    /// </summary>
    public static ItemMenuEntry Search(string? glyph, string text, string? value, MenuSearch search)
    {
        ArgumentNullException.ThrowIfNull(search);
        return new(glyph, text, null, Search: search with { Value = value });
    }

    /// <summary>A submenu of <paramref name="lines"/>, opened beside its line.</summary>
    public static ItemMenuEntry Submenu(string? glyph, string text, IReadOnlyList<ItemMenuEntry> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        return new(glyph, text, null, Children: lines);
    }
}

/// <summary>
/// [S-3721] One line of text asked for beside the properties menu: its heading, the box's
/// placeholder, the button's words, the check run as it is typed (the reason it cannot be used, or
/// null) and what is done with it. Enter or the button gives it; Escape or a click away drops it.
/// <paramref name="Initial"/> is what the box starts with (a name being changed);
/// <paramref name="AllowEmpty"/> lets an empty box be given (a name cleared).
/// </summary>
public sealed record WidgetTextAsk(string Title, string Placeholder, string Accept, Func<string, string?> Refuse, Action<string> Done, string? Initial = null, bool AllowEmpty = false);

/// <summary>
/// [S-3722] A search on the properties menu: the box's placeholder, the answer for what is typed
/// (the empty text included, asked as the submenu opens), how long typing settles before it is
/// asked (zero for an answer in hand, the geocoder's pause for a network one), and the lines that
/// always follow the answers (clear the choice, a switch about it).
/// </summary>
public sealed record MenuSearch(
    string Placeholder,
    Func<string, CancellationToken, Task<MenuSearchAnswer>> Find,
    IReadOnlyList<ItemMenuEntry> Fixed,
    TimeSpan Settle = default)
{
    /// <summary>What is chosen now, at the end of the search's line.</summary>
    public string? Value { get; init; }
}

/// <summary>[S-3722] What a search found: its lines (a click picks), or a word in their place (searching, no match, offline).</summary>
public sealed record MenuSearchAnswer(IReadOnlyList<ItemMenuEntry> Lines, string? Note = null);
