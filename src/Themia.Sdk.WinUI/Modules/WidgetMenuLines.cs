using Themia.App.Widgets;

namespace Themia.Sdk.WinUI;

/// <summary>
/// [S-3719] What a module's own lines on the properties menu ([S-3716]) are built into: the lines of
/// the menu's first level (<see cref="Lines"/>), the lines under its Style submenu after the styles
/// (<see cref="StyleLines"/>: what only the look draws with), and the save to call after a change.
/// The lines are the one menu vocabulary (<see cref="ItemMenuEntry"/>): a switch, a choice of one
/// (a submenu of dots), a submenu, a command. The host builds them again after every change, so a
/// line says what the widget holds now and a closure may keep the value it was built with.
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

    /// <summary>The menu's first level, after Style and before the card's own lines (Header, Background, ...).</summary>
    public List<ItemMenuEntry> Lines { get; } = [];

    /// <summary>Under Style, after the styles and a divider: the options only the current look draws with.</summary>
    public List<ItemMenuEntry> StyleLines { get; } = [];

    /// <summary>Store a change made to the config in place, as a settings page stores one.</summary>
    public void Save() => _save();

    /// <summary>[S-3721] Ask for one line of text beside the menu (a web address, a path).</summary>
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
    public ItemMenuEntry Choice<T>(string glyph, string text, IReadOnlyList<(T Value, string Text)> options, T current, Action<T> set)
        where T : struct, Enum
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

    /// <summary>A submenu of <paramref name="lines"/>, opened beside its line.</summary>
    public static ItemMenuEntry Submenu(string glyph, string text, IReadOnlyList<ItemMenuEntry> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        return new(glyph, text, null, Children: lines);
    }
}

/// <summary>
/// [S-3721] One line of text asked for beside the properties menu: its heading, the box's
/// placeholder, the button's words, the check run as it is typed (the reason it cannot be used, or
/// null) and what is done with it. Enter or the button gives it; Escape or a click away drops it.
/// </summary>
public sealed record WidgetTextAsk(string Title, string Placeholder, string Accept, Func<string, string?> Refuse, Action<string> Done);
