using System.Globalization;
using System.Numerics;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Settings;

namespace Themia.Sdk.WinUI;

/// <summary>
/// [S-3719] What a module's own lines on the properties menu ([S-3716]) are built into: the lines of
/// the menu's first level (<see cref="Lines"/>), the lines under its Style submenu after the styles
/// (<see cref="StyleLines"/>: what only the look draws with), where the card's own lines go
/// (<see cref="PlaceCard"/>), and the save to call after a change. The lines are the one menu
/// vocabulary (<see cref="ItemMenuEntry"/>): a switch, a choice of one (a submenu of dots), a
/// submenu, a search, a command, a colour or a fill, a number, a confirmed command, a note, an
/// editable list and a folder. The host builds them again after every change, so a line says what
/// the widget holds now and a closure may keep the value it was built with.
/// </summary>
public sealed class WidgetMenuLines
{
    /// <summary>[S-3726] The most steps a number offers as dots; more is asked for as typed text.</summary>
    public const int MaxNumberSteps = 20;

    private const string Scope = "menu-lines";

    private readonly Action _save;
    private readonly Action<WidgetTextAsk> _ask;
    private readonly Action<WidgetConfirm>? _confirm;

    /// <param name="save">Stores the config after a change (the live widget's, through the host's store).</param>
    /// <param name="ask">[S-3721] Shows a question for one line of text beside the menu.</param>
    /// <param name="confirm">[S-3727] Shows an "are you sure?" beside the menu; without one a confirmed line does nothing.</param>
    public WidgetMenuLines(Action save, Action<WidgetTextAsk> ask, Action<WidgetConfirm>? confirm = null)
    {
        _save = save ?? throw new ArgumentNullException(nameof(save));
        _ask = ask ?? throw new ArgumentNullException(nameof(ask));
        _confirm = confirm;
    }

    /// <summary>
    /// [S-3745] Opens Settings › Accounts, where an account comes into being: what an account widget's
    /// Manage accounts line does. Null where the host has no settings window (a render, a test).
    /// </summary>
    public Action? ShowAccounts { get; init; }

    /// <summary>
    /// [S-3746] Raised when something a line showed while it was still being read has come (an
    /// account's calendars, its lists): the host builds the lines again, so they say what is there.
    /// </summary>
    public event Action? Outdated;

    /// <summary>[S-3746] Say the lines as built are out of date: the host builds them again.</summary>
    public void Outdate() => Outdated?.Invoke();

    /// <summary>The menu's first level, after Style.</summary>
    public List<ItemMenuEntry> Lines { get; } = [];

    /// <summary>Under Style, after the styles and a divider: the options only the current look draws with.</summary>
    public List<ItemMenuEntry> StyleLines { get; } = [];

    /// <summary>
    /// [S-3723] Where in <see cref="Lines"/> the card's own lines (header, alignment, the colours,
    /// scale) stand, as one Appearance submenu; null (nothing placed) puts it first, right after Style.
    /// </summary>
    public int? CardAt { get; private set; }

    /// <summary>[S-3723] The card's own lines go here, as one Appearance submenu among the widget's.</summary>
    public void PlaceCard() => CardAt = Lines.Count;

    /// <summary>Store a change made to the config in place.</summary>
    public void Save() => _save();

    /// <summary>
    /// [S-1862] Mark <paramref name="line"/> as where what the widget waits for is set (a place, a
    /// feed, a target): the card's Set up button selects the widget and opens its menu at this line,
    /// as a click on it would (its submenu opens, its question is asked). One line per widget; a line
    /// inside a submenu is reached through it.
    /// </summary>
    public static ItemMenuEntry SetsUp(ItemMenuEntry line) => line with { SetsUp = true };

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

    /// <summary>
    /// [S-3745] The line that opens Settings › Accounts (Manage accounts, Add an account): it changes
    /// nothing here and saves nothing; greyed where the host has no settings window.
    /// </summary>
    public ItemMenuEntry AccountsLine(string? glyph, string text)
        => new(glyph, text, () => ShowAccounts?.Invoke(), Disabled: ShowAccounts is null);

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

    /// <summary>
    /// [S-3724] A colour setting (<c>#RRGGBB</c>, or empty to follow the default): the colour's swatch
    /// in the line's icon column (so it takes no glyph) and its words at the end; its submenu is the
    /// one colour submenu (Default with the dot, the swatches, Custom colour…, Pick a colour from the
    /// screen). <paramref name="options"/>: Default's label (null when a colour is required) and
    /// colour, the swatches (<see cref="ColorPresets.Surfaces"/> unless given, e.g.
    /// <see cref="ColorPresets.Windows"/> for an accent), and whether Custom colour is offered. A pick
    /// sets and saves; the colour that holds changes nothing.
    /// </summary>
    public ItemMenuEntry Colour(string text, string current, Action<string> set, ColorChoiceOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(set);
        options ??= new ColorChoiceOptions { DefaultLabel = Str.Common_Default.Text(), Presets = ColorPresets.Surfaces };
        return new(null, text, null, Colour: MenuColour.Of(current ?? string.Empty, value =>
        {
            set(value);
            _save();
        }, options));
    }

    /// <summary>
    /// [S-3724] A fill setting (<see cref="FillChoice"/>), drawn as the card's Background is: Default
    /// and Transparent with the dot, the swatches, Opacity (a colour only), Custom colour… and Pick a
    /// colour from the screen. <paramref name="set"/> gets a fresh choice; a pick saves.
    /// </summary>
    public ItemMenuEntry Fill(string text, FillChoice current, Action<FillChoice> set, FillChoiceOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(set);
        options ??= new FillChoiceOptions { DefaultLabel = Str.Common_Default.Text() };
        return new(null, text, null, Colour: MenuColour.OfFill(current, value =>
        {
            set(value);
            _save();
        }, options));
    }

    /// <summary>
    /// [S-3726] A number from <paramref name="min"/> to <paramref name="max"/> in steps of
    /// <paramref name="step"/>, shown at the end of its line as <paramref name="format"/> writes it.
    /// Up to <see cref="MaxNumberSteps"/> steps it is a choice of one (a submenu of dots, one per
    /// step); more is a line that asks for the number as text, refused outside the range (the range is
    /// the reason) and rounded to the nearest step. A pick or a typed number sets and saves; the value
    /// that holds changes nothing.
    /// </summary>
    public ItemMenuEntry Number<T>(string? glyph, string text, T value, T min, T max, T step, Func<T, string> format, Action<T> set)
        where T : INumber<T>
    {
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(set);
        if (step <= T.Zero || max < min)
        {
            throw new ArgumentOutOfRangeException(nameof(step), "a number needs a positive step and min ≤ max");
        }

        var steps = NumberSteps(min, max, step);
        if (steps.Count <= MaxNumberSteps)
        {
            return Choice(glyph, text, [.. steps.Select(s => (s, format(s)))], value, set) with { Value = format(value) };
        }

        var reason = Str.Menu_NumberRange.Format(("min", format(min)), ("max", format(max)));
        return new(glyph, text, () => _ask(new WidgetTextAsk(
            text,
            Str.Menu_NumberAsk.Text(),
            Str.Common_Save.Text(),
            typed => ParseNumber(typed, min, max, step, out T _) ? null : reason,
            typed =>
            {
                if (ParseNumber(typed, min, max, step, out var number) && number != value)
                {
                    set(number);
                    _save();
                }
            },
            Initial: value.ToString(null, CultureInfo.InvariantCulture))), Value: format(value));
    }

    /// <summary>[S-3726] The steps a number offers, from <paramref name="min"/> up to <paramref name="max"/>.</summary>
    public static IReadOnlyList<T> NumberSteps<T>(T min, T max, T step)
        where T : INumber<T>
    {
        var low = double.CreateChecked(min);
        var size = double.CreateChecked(step);
        var count = (int)Math.Floor(((double.CreateChecked(max) - low) / size) + 1e-9) + 1;
        var steps = new List<T>(Math.Min(count, MaxNumberSteps + 1));
        for (var i = 0; i < count && steps.Count <= MaxNumberSteps; i++)
        {
            steps.Add(T.CreateChecked(Math.Round(low + (i * size), 10)));
        }

        // Past the dots' limit only the count matters: the line asks instead.
        return steps;
    }

    /// <summary>
    /// [S-3726] A typed number: invariant digits (a decimal comma read as a point), inside the range,
    /// rounded to the nearest step. False for anything else, so nothing half-read is stored.
    /// </summary>
    public static bool ParseNumber<T>(string typed, T min, T max, T step, out T value)
        where T : INumber<T>
    {
        value = min;
        var text = (typed ?? string.Empty).Trim().Replace(',', '.');
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number))
        {
            return false;
        }

        var low = double.CreateChecked(min);
        var high = double.CreateChecked(max);
        if (number < low || number > high)
        {
            return false;
        }

        var size = double.CreateChecked(step);
        var snapped = Math.Clamp(Math.Round(low + (Math.Round((number - low) / size) * size), 10), low, high);
        value = T.CreateChecked(snapped);
        return true;
    }

    /// <summary>
    /// [S-3727] A command that destroys something (clear a history): its click asks beside the menu
    /// first — <paramref name="question"/>, Cancel and <paramref name="confirmWords"/> — and only the
    /// answer runs <paramref name="action"/> and saves. Danger lines are drawn in the critical colour.
    /// </summary>
    public ItemMenuEntry Confirm(string? glyph, string text, string question, string confirmWords, Action action, bool danger = true, bool disabled = false)
    {
        ArgumentNullException.ThrowIfNull(action);
        var confirm = new WidgetConfirm(question, confirmWords, () =>
        {
            action();
            _save();
        }, danger);

        return new(glyph, text, () =>
        {
            if (_confirm is { } ask)
            {
                ask(confirm);
            }
            else
            {
                // Refusing beats destroying something unasked.
                Log.Warn(Scope, "a confirmed line was clicked with nothing to ask; nothing was changed");
            }
        }, Danger: danger, Disabled: disabled);
    }

    /// <summary>[S-3728] A line of words only (a data source's credit): greyed, never clickable.</summary>
    public static ItemMenuEntry Note(string text) => new(null, text, null, Disabled: true, Note: true);

    /// <summary>
    /// [S-3729] An editable list setting (feeds, symbols, hosts, shortcuts) as a submenu: each item a
    /// submenu of its own (Rename… where <see cref="MenuList{T}.Rename"/> is given, the
    /// <see cref="MenuList{T}.Extra"/> lines, Move up and Move down with the ends greyed, Remove), the
    /// <see cref="MenuList{T}.Empty"/> sentence while there are none, then Add… (asked for and checked
    /// by <see cref="MenuList{T}.Add"/>) or the caller's own <see cref="MenuList{T}.AddLines"/>. Every
    /// change is made in <paramref name="items"/> and saved.
    /// </summary>
    public ItemMenuEntry List<T>(string? glyph, string text, IList<T> items, MenuList<T> list)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(list);

        var lines = new List<ItemMenuEntry>(items.Count + 3);
        if (items.Count == 0)
        {
            lines.Add(Note(list.Empty));
        }

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var at = i;
            var actions = new List<ItemMenuEntry>();
            if (list.Rename is { } rename)
            {
                actions.Add(new(Glyphs.Rename, Str.Menu_Rename.Text(), () => _ask(new WidgetTextAsk(
                    Str.Common_Rename.Text(),
                    rename.Placeholder?.Invoke(item) ?? list.Name(item),
                    Str.Common_Rename.Text(),
                    rename.Refuse ?? (_ => null),
                    typed =>
                    {
                        if (rename.Set(item, typed))
                        {
                            _save();
                        }
                    },
                    Initial: rename.Current?.Invoke(item) ?? list.Name(item),
                    AllowEmpty: rename.AllowEmpty))));
            }

            if (list.Extra?.Invoke(item) is { } extra)
            {
                actions.AddRange(extra);
            }

            if (list.Reorder)
            {
                actions.Add(ItemMenuEntry.Separator);
                actions.Add(Command(Glyphs.MoveUp, Str.Menu_MoveUp.Text(), () => Move(items, item, at, -1), disabled: i == 0));
                actions.Add(Command(Glyphs.MoveDown, Str.Menu_MoveDown.Text(), () => Move(items, item, at, 1), disabled: i == items.Count - 1));
            }

            actions.Add(ItemMenuEntry.Separator);
            actions.Add(Command(Glyphs.Delete, Str.Common_Remove.Text(), () =>
            {
                if (IndexOf(items, item, at) is var found and >= 0)
                {
                    items.RemoveAt(found);
                }
            }, disabled: items.Count <= list.Min));

            lines.Add(Submenu(list.Glyph?.Invoke(item), list.Name(item), actions) with { IconFile = list.IconFile?.Invoke(item) });
        }

        var full = list.Max is { } max && items.Count >= max;
        if (list.Add is { } add)
        {
            lines.Add(ItemMenuEntry.Separator);
            lines.Add(new(Glyphs.Add, Str.Menu_Add.Text(), () => _ask(new WidgetTextAsk(
                add.Title,
                add.Placeholder,
                Str.Common_Add.Text(),
                add.Refuse,
                typed =>
                {
                    items.Add(add.Create(typed));
                    _save();
                })), Disabled: full));
        }

        if (list.AddLines is { Count: > 0 } addLines)
        {
            lines.Add(ItemMenuEntry.Separator);
            lines.AddRange(full ? addLines.Select(l => l with { Disabled = true }) : addLines);
        }

        return Submenu(glyph, text, lines);
    }

    /// <summary>
    /// [S-3730] A folder setting: its line shows the folder's name at its end (the whole path as it
    /// is otherwise long), and a click opens Windows' folder picker; a folder picked there sets and
    /// saves. Nobody types a path they can point at.
    /// </summary>
    public ItemMenuEntry PickFolder(string? glyph, string text, string currentPath, Action<string> set)
    {
        ArgumentNullException.ThrowIfNull(set);
        var current = currentPath ?? string.Empty;
        return new(glyph, text, () => _ = FolderBrowse.PickAsync(path =>
        {
            if (!string.Equals(path, current, StringComparison.OrdinalIgnoreCase))
            {
                set(path);
                _save();
            }
        }), Value: FolderName(current));
    }

    /// <summary>[S-3730] What a folder's line shows of its path: the last part, a drive as itself, nothing for none.</summary>
    public static string? FolderName(string? path)
    {
        var trimmed = (path ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        var name = Path.GetFileName(trimmed.TrimEnd('\\', '/'));
        return name.Length > 0 ? name : trimmed;
    }

    /// <summary>[S-3729] Move the item a step; where the list changed under the line, the item is found again.</summary>
    private static void Move<T>(IList<T> items, T item, int at, int by)
    {
        var from = IndexOf(items, item, at);
        var to = from + by;
        if (from < 0 || to < 0 || to >= items.Count)
        {
            return;
        }

        (items[from], items[to]) = (items[to], items[from]);
    }

    /// <summary>Where <paramref name="item"/> is: at <paramref name="at"/> as built, else found again, else -1.</summary>
    private static int IndexOf<T>(IList<T> items, T item, int at)
        => at < items.Count && EqualityComparer<T>.Default.Equals(items[at], item) ? at : items.IndexOf(item);
}

/// <summary>
/// [S-3729] How <see cref="WidgetMenuLines.List{T}"/> shows and edits a list: an item's name (its
/// line), the sentence while there are none, and optionally its icon, a rename, lines of its own, an
/// add asked for as text or the caller's add lines, a most-items limit, and whether it is ordered.
/// </summary>
/// <param name="Name">The words of an item's line.</param>
/// <param name="Empty">What the list says while it has no item (a note: "No feeds yet").</param>
public sealed record MenuList<T>(Func<T, string> Name, string Empty)
{
    /// <summary>An item's line's glyph.</summary>
    public Func<T, string?>? Glyph { get; init; }

    /// <summary>A file whose shell icon is an item's icon in place of its glyph (a shortcut to a file).</summary>
    public Func<T, string?>? IconFile { get; init; }

    /// <summary>Rename… on each item, asked for as text.</summary>
    public MenuListRename<T>? Rename { get; init; }

    /// <summary>Lines of an item's own, after Rename… (Change target…, Edit layout…).</summary>
    public Func<T, IReadOnlyList<ItemMenuEntry>>? Extra { get; init; }

    /// <summary>Add…: a line of text asked for, checked as it is typed, made into an item.</summary>
    public MenuListAdd<T>? Add { get; init; }

    /// <summary>The caller's own add lines (pickers, a submenu of kinds), after the items.</summary>
    public IReadOnlyList<ItemMenuEntry>? AddLines { get; init; }

    /// <summary>The most items the list takes: the add lines are greyed at it.</summary>
    public int? Max { get; init; }

    /// <summary>[S-3729] The fewest items the list keeps (a table's last column): Remove is greyed at it.</summary>
    public int Min { get; init; }

    /// <summary>Whether the order means something (Move up and Move down). On by default.</summary>
    public bool Reorder { get; init; } = true;
}

/// <summary>
/// [S-3729] Rename… on a list's items: <paramref name="Set"/> stores what was typed and says whether
/// anything changed (nothing is saved otherwise); the box starts from <see cref="Current"/> (the
/// name shown when null) with <see cref="Placeholder"/> as its hint (the name again when null).
/// </summary>
public sealed record MenuListRename<T>(Func<T, string, bool> Set)
{
    public Func<T, string>? Current { get; init; }

    public Func<T, string>? Placeholder { get; init; }

    /// <summary>An empty name may be given (it clears a label back to the derived name).</summary>
    public bool AllowEmpty { get; init; }

    /// <summary>Why a typed name cannot be used, or null when it can.</summary>
    public Func<string, string?>? Refuse { get; init; }
}

/// <summary>[S-3729] Add… on a list: the question's heading and hint, the check run as it is typed, and the item made of what was given.</summary>
public sealed record MenuListAdd<T>(string Title, string Placeholder, Func<string, string?> Refuse, Func<string, T> Create);

/// <summary>
/// [S-3721] One line of text asked for beside the properties menu: its heading, the box's
/// placeholder, the button's words, the check run as it is typed (the reason it cannot be used, or
/// null) and what is done with it. Enter or the button gives it; Escape or a click away drops it.
/// <paramref name="Initial"/> is what the box starts with (a name being changed);
/// <paramref name="AllowEmpty"/> lets an empty box be given (a name cleared).
/// </summary>
public sealed record WidgetTextAsk(string Title, string Placeholder, string Accept, Func<string, string?> Refuse, Action<string> Done, string? Initial = null, bool AllowEmpty = false);

/// <summary>
/// [S-3727] An "are you sure?" beside the properties menu: the question, the words of the button
/// that does it (in the critical colour's line where <paramref name="Danger"/>), and what it does.
/// Cancel, Escape or a click away do nothing.
/// </summary>
public sealed record WidgetConfirm(string Question, string Accept, Action Done, bool Danger = true);

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
