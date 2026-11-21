using System.Globalization;
using System.Numerics;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Settings;

namespace Themia.Sdk.WinUI;

/// <summary>
/// [S-3719] What a module's own lines on the selection's page ([S-3716]) are built into: the lines of
/// the menu's first level (<see cref="Lines"/>), the lines right under its Style line
/// (<see cref="StyleLines"/>: what only the look draws with), and the save to call after a change.
/// The card's own lines are the menu's pinned Appearance foot ([S-3739]), never among these. The lines are the one
/// settings-row model (<see cref="SettingsLine"/>, the settings pages' own): a switch, a choice of one, a submenu, a
/// search, a command, a colour or a fill, a number, a slider, a line of text or a question asked in place, a note,
/// an editable list and a folder; a line's key is its words (the page keys it by where it stands). The host builds
/// them again after every change, so a line says what the widget holds now and a closure may keep the value it was
/// built with. [S-2351] Nothing here opens a page of its own: what a line asks it carries
/// (<see cref="SettingsPrompt"/>, <see cref="SettingsCommand.Confirm"/>), and the page asks it in place, in the line's card.
/// </summary>
public sealed class WidgetMenuLines
{
    private readonly Action _save;

    /// <param name="save">Stores the config after a change (the live widget's, through the host's store).</param>
    public WidgetMenuLines(Action save)
    {
        _save = save ?? throw new ArgumentNullException(nameof(save));
    }

    /// <summary>
    /// [S-3749] Opens where <see cref="SettingLink"/> leads (settings › Accounts, to add or manage an account),
    /// closing the selection's page: what a link row does. A setting the widget merely follows (a format,
    /// an update interval) gets no link row: the widget just follows it ([S-3590]). Null where the host
    /// has no edit bar (a render, a test): the link is greyed.
    /// </summary>
    public Action<SettingLink>? OpenSetting { get; init; }

    /// <summary>
    /// [S-2851] Where Windows' location stands, for a place widget's Use my location line:
    /// <see cref="DeviceLocation.Off"/> where the host has none (a test).
    /// </summary>
    public DeviceLocation Device { get; init; } = DeviceLocation.Off;

    /// <summary>
    /// [S-2851] Opens Windows' location privacy page, where location is turned on for the device and
    /// for Themia. Null where the host has none (a render, a test): the line is greyed.
    /// </summary>
    public Action? OpenLocationSettings { get; init; }

    /// <summary>
    /// [S-3746] Raised when something a line showed while it was still being read has come (an
    /// account's calendars, its lists): the host builds the lines again, so they say what is there.
    /// </summary>
    public event Action? Outdated;

    /// <summary>[S-3746] Say the lines as built are out of date: the host builds them again.</summary>
    public void Outdate() => Outdated?.Invoke();

    /// <summary>The menu's first level, after Style.</summary>
    public List<SettingsLine> Lines { get; } = [];

    /// <summary>[S-3735] On the first level right under the Style line, in its section: the options only the current look draws with.</summary>
    public List<SettingsLine> StyleLines { get; } = [];

    /// <summary>Store a change made to the config in place.</summary>
    public void Save() => _save();

    /// <summary>
    /// [S-1862] Mark <paramref name="line"/> as where what the widget waits for is set (a place, a
    /// feed, a target): the card's Set up button selects the widget and opens its menu at this line,
    /// as a click on it would (its submenu opens, its question is asked). One line per widget; a line
    /// inside a submenu is reached through it.
    /// </summary>
    public static SettingsLine SetsUp(SettingsLine line)
    {
        ArgumentNullException.ThrowIfNull(line);
        return line with { SetsUp = true };
    }

    /// <summary>
    /// [S-11753] Mark <paramref name="line"/> as setting <paramref name="setting"/>, a setting several
    /// kinds hold over one contract: a group's page ([S-11751]) takes this line from one member's lines
    /// and shows it once, setting every member that holds it. The line is the widget's own all the same
    /// ([S-3590]): on its own page it sets the widget alone.
    /// </summary>
    public static SettingsLine Shared(SharedSetting setting, SettingsLine line)
    {
        ArgumentNullException.ThrowIfNull(setting);
        ArgumentNullException.ThrowIfNull(line);
        return line with { Shares = setting.Key };
    }

    /// <summary>[S-11753] Every one of <paramref name="lines"/> marked as setting <paramref name="setting"/>.</summary>
    public static IReadOnlyList<SettingsLine> Shared(SharedSetting setting, IEnumerable<SettingsLine> lines)
    {
        ArgumentNullException.ThrowIfNull(setting);
        ArgumentNullException.ThrowIfNull(lines);
        return [.. lines.Select(line => line with { Shares = setting.Key })];
    }

    /// <summary>
    /// [S-2351] [S-3721] A line that asks for one line of text (a web address, a path, a name, Rename…, Add…), in place:
    /// its row says <paramref name="text"/> and what it holds at its end (<paramref name="value"/>, else what the field
    /// starts with) with the expand chevron, and opens in its own card on the field (holding <see cref="WidgetTextAsk.Initial"/>,
    /// selected), why it is refused under it, and the accept in <see cref="WidgetTextAsk.Accept"/>'s words, greyed while refused.
    /// Enter gives it too; Escape or a press elsewhere closes it, keeping nothing. Never a page of its own.
    /// </summary>
    public static SettingsPrompt Ask(string? glyph, string text, WidgetTextAsk ask, string? value = null, bool disabled = false)
    {
        ArgumentNullException.ThrowIfNull(ask);
        return new SettingsPrompt(text, text, glyph, ask)
        {
            Value = value ?? (string.IsNullOrEmpty(ask.Initial) ? null : ask.Initial),
            Disabled = disabled,
        };
    }

    /// <summary>
    /// [S-2332] A line of text the widget holds (a title, a caption, an address), typed in place in its box at the
    /// line's end: what it holds in the box, <paramref name="placeholder"/> while empty; a text <paramref name="refuse"/>
    /// takes (null: any) is stored through <paramref name="set"/> and saved, the text that holds changes nothing. A
    /// line of text asked for once, to make something (Add…, Rename… of a list's item), is <see cref="Ask"/>.
    /// </summary>
    public SettingsAsk Text(string? glyph, string text, string current, Action<string> set, string? placeholder = null, Func<string, string?>? refuse = null, bool allowEmpty = true)
    {
        ArgumentNullException.ThrowIfNull(set);
        var holds = current ?? string.Empty;
        return new SettingsAsk(text, text, glyph, placeholder ?? text, holds, refuse ?? (_ => null), typed =>
        {
            if (!string.Equals(typed, holds, StringComparison.Ordinal))
            {
                set(typed);
                _save();
            }
        })
        {
            AllowEmpty = allowEmpty,
            Value = holds.Length > 0 ? holds : null,
        };
    }

    /// <summary>
    /// A switch: on while on; a click turns it the other way and saves. Under a submenu it is
    /// best without a <paramref name="glyph"/>: Explorer's switches are a tick and words.
    /// </summary>
    public SettingsToggle Toggle(string? glyph, string text, bool on, Action<bool> set)
    {
        ArgumentNullException.ThrowIfNull(set);
        return new SettingsToggle(text, text, glyph, on, _ =>
        {
            set(!on);
            _save();
        });
    }

    /// <summary>
    /// [S-3718] A choice of one: its options, the current one with Windows' dot. Picking the current one changes
    /// nothing and stores nothing.
    /// </summary>
    public SettingsChoice Choice<T>(string? glyph, string text, IReadOnlyList<(T Value, string Text)> options, T current, Action<T> set)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(set);

        var radios = new List<SettingsRadio>(options.Count);
        foreach (var (value, label) in options)
        {
            var isCurrent = EqualityComparer<T>.Default.Equals(value, current);
            radios.Add(new SettingsRadio(label, label, isCurrent, () =>
            {
                if (!isCurrent)
                {
                    set(value);
                    _save();
                }
            }));
        }

        return new SettingsChoice(text, text, glyph, radios);
    }

    /// <summary>
    /// [S-3745] [S-3749] The line that opens settings › Accounts (Manage accounts, Add an account): a link
    /// row, never a copy of what settings › Accounts sets; it changes nothing here and saves nothing; greyed
    /// where the host has no edit bar.
    /// </summary>
    public SettingsLink AccountsLine(string? glyph, string text)
        => new(text, text, glyph, new SettingLink(OwnedSetting.Accounts).Owner, () => OpenSetting?.Invoke(new SettingLink(OwnedSetting.Accounts)))
        {
            Disabled = OpenSetting is null,
        };

    /// <summary>A command that changes the config and saves: one step.</summary>
    public SettingsCommand Command(string? glyph, string text, Action change, bool disabled = false)
    {
        ArgumentNullException.ThrowIfNull(change);
        return new SettingsCommand(text, text, glyph, () =>
        {
            change();
            _save();
        })
        {
            Disabled = disabled,
        };
    }

    /// <summary>
    /// [S-3722] A search: its line shows <paramref name="value"/> (what is chosen) and opens on a box, the answers
    /// <paramref name="find"/> gives for what is typed as lines under it and <paramref name="fixedLines"/> after them;
    /// typing settles for <paramref name="settle"/> before it is asked (zero for an answer in hand, the geocoder's
    /// pause for a network one).
    /// </summary>
    public static SettingsSearch Search(
        string? glyph,
        string text,
        string? value,
        string placeholder,
        Func<string, CancellationToken, Task<SettingsSearchAnswer>> find,
        IReadOnlyList<SettingsLine> fixedLines,
        TimeSpan settle = default)
    {
        ArgumentNullException.ThrowIfNull(find);
        ArgumentNullException.ThrowIfNull(fixedLines);
        return new SettingsSearch(text, text, glyph, placeholder, find, fixedLines, settle) { Value = value };
    }

    /// <summary>
    /// [S-13606] One answer of a <see cref="SettingsSearch"/>: its words alone (no glyph: the search's line says what it
    /// finds), whether it is the one in force (dotted), and what a pick does. Every search answers with these, so
    /// every lookup lists the same rows.
    /// </summary>
    public static SettingsRadio Found(string text, bool holds, Action pick)
    {
        ArgumentNullException.ThrowIfNull(pick);
        return new SettingsRadio(text, text, holds, pick);
    }

    /// <summary>
    /// A submenu of <paramref name="lines"/>, opened inside its card ([S-3729]: a list's items one level further in);
    /// [S-3718] one of options alone (dividers aside) is a choice of one. What it says at its end is the option that
    /// holds among its lines, where one does, unless <paramref name="value"/> says otherwise.
    /// </summary>
    public static SettingsLine Submenu(string? glyph, string text, IReadOnlyList<SettingsLine> lines, string? value = null)
    {
        ArgumentNullException.ThrowIfNull(lines);
        if (lines.Count > 0 && lines.All(l => l is SettingsRadio or SettingsSeparator) && lines.Any(l => l is SettingsRadio))
        {
            var choice = new SettingsChoice(text, text, glyph, [.. lines.OfType<SettingsRadio>()]);
            return value is null ? choice : choice with { Value = value };
        }

        return new SettingsSubmenu(text, text, glyph, lines)
        {
            Inline = true,
            Value = value ?? lines.OfType<SettingsRadio>().FirstOrDefault(r => r.Holds)?.Text,
        };
    }

    /// <summary>
    /// [S-9602] An icon setting: a glyph from the one icon picker ([S-1803]), inline in the line's card, or
    /// none (the picker's first cell, worded <paramref name="noneName"/>). Its line shows the glyph chosen.
    /// A pick sets and saves; the glyph that holds changes nothing.
    /// </summary>
    public SettingsIcon Icon(string text, string? current, string noneName, Action<string?> set)
    {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(noneName);
        var held = string.IsNullOrEmpty(current) ? null : current;
        return new SettingsIcon(text, text, held, held, null, noneName, value =>
        {
            if (!string.Equals(value, current, StringComparison.Ordinal))
            {
                set(value);
                _save();
            }
        })
        {
            // [S-2317] Its icon is the one it chose: kept on a row inside a card.
            Marked = true,
        };
    }

    /// <summary>
    /// [S-3724] A colour setting (<c>#RRGGBB</c>, or empty to follow the default): the colour's swatch
    /// in the line's icon column (so it takes no glyph) and its words at the end; it opens on the
    /// one colour picker (Default with the dot, the swatches, Custom colour…, Pick a colour from the
    /// screen). <paramref name="defaultText"/>: Default's words (null when a colour is required; Default
    /// unless given), the swatches (<see cref="ColorPresets.Surfaces"/> unless given, e.g.
    /// <see cref="ColorPresets.Windows"/> for an accent) and Default's own colour (<paramref name="defaultHex"/>).
    /// A pick sets and saves; the colour that holds changes nothing.
    /// </summary>
    public SettingsColour Colour(string text, string current, Action<string> set, IReadOnlyList<ColorPreset>? presets = null, string? defaultText = "", string? defaultHex = null)
    {
        ArgumentNullException.ThrowIfNull(set);
        return new SettingsColour(text, text, current ?? string.Empty, value =>
        {
            set(value);
            _save();
        }, defaultText is { Length: 0 } ? Str.Common_Default.Text() : defaultText, presets ?? ColorPresets.Surfaces)
        {
            DefaultHex = defaultHex,
        };
    }

    /// <summary>
    /// [S-3724] A fill setting (<see cref="FillChoice"/>), drawn as the card's Background is: Default
    /// and Transparent with the dot, the swatches, Opacity (a colour only), Custom colour… and Pick a
    /// colour from the screen. <paramref name="set"/> gets a fresh choice; a pick saves.
    /// </summary>
    public SettingsFill Fill(string text, FillChoice current, Action<FillChoice> set)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(set);
        return new SettingsFill(text, text, current, value =>
        {
            set(value);
            _save();
        }, Str.Common_Default.Text());
    }

    /// <summary>
    /// [S-3726] [S-2370] A number from <paramref name="min"/> to <paramref name="max"/> in steps of
    /// <paramref name="step"/>, written in its unit as <paramref name="format"/> writes it ("7 days", "30 min"): Karpo's
    /// number box (its §521, §302: "a number a stepper in the row", typed in), 240 × 32 with − and + inside, whatever
    /// its range: Up, Down, the wheel and − / + step it, a number typed is read in the box's unit, held to the range
    /// and rounded to the step ([S-2309]). A step or a typed number sets and saves; the value that holds changes
    /// nothing. Never a choice of dots and never a page of its own.
    /// </summary>
    public SettingsSlider Number<T>(string? glyph, string text, T value, T min, T max, T step, Func<T, string> format, Action<T> set)
        where T : INumber<T>
    {
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(set);
        if (step <= T.Zero || max < min)
        {
            throw new ArgumentOutOfRangeException(nameof(step), "a number needs a positive step and min ≤ max");
        }

        return new SettingsSlider(
            text,
            text,
            glyph,
            double.CreateChecked(value),
            double.CreateChecked(min),
            double.CreateChecked(max),
            double.CreateChecked(step),
            v => format(T.CreateChecked(Math.Round(v, 10))),
            v =>
            {
                set(T.CreateChecked(Math.Round(v, 10)));
                _save();
            })
        {
            ValueText = format(value),
        };
    }

    /// <summary>
    /// [S-3731] A number set by feel (a volume, a size) as an inline slider: one line of its words,
    /// the slider and the value as <paramref name="format"/> writes it, from <paramref name="min"/>
    /// to <paramref name="max"/> on the grain of <paramref name="step"/>. A drag shows the value at
    /// the line's end and stores once on the release; a key steps it and stores the step. The value
    /// that holds changes nothing. A number the user types or picks from a few values is
    /// <see cref="Number{T}"/>.
    /// </summary>
    public SettingsSlider Slider<T>(string? glyph, string text, T value, T min, T max, T step, Func<T, string> format, Action<T> set)
        where T : INumber<T>
    {
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(set);
        if (step <= T.Zero || max < min)
        {
            throw new ArgumentOutOfRangeException(nameof(step), "a slider needs a positive step and min ≤ max");
        }

        return new SettingsSlider(
            text,
            text,
            glyph,
            double.CreateChecked(value),
            double.CreateChecked(min),
            double.CreateChecked(max),
            double.CreateChecked(step),
            v => format(T.CreateChecked(v)),
            v =>
            {
                set(T.CreateChecked(v));
                _save();
            })
        {
            ValueText = format(value),
        };
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
    /// [S-3727] [S-2351] A command that destroys something (clear a history): its click asks first, in place in its own
    /// card — <paramref name="question"/>, under it <paramref name="detail"/> (what goes with it, when there is more to say
    /// than the question), <paramref name="confirmWords"/> and Cancel — and only the answer runs
    /// <paramref name="action"/> and saves. Danger lines are drawn in the critical colour.
    /// </summary>
    public SettingsCommand Confirm(string? glyph, string text, string question, string confirmWords, Action action, bool danger = true, bool disabled = false, string? detail = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        var confirm = new SettingsConfirm(question, confirmWords, () =>
        {
            action();
            _save();
        }, detail, danger);

        return SettingsCommand.Asking(text, text, glyph, confirm) with { Danger = danger, Disabled = disabled };
    }

    /// <summary>[S-3728] A line of words only (a data source's credit): greyed, never clickable.</summary>
    public static SettingsNote Note(string text) => new(text, text);

    /// <summary>
    /// [S-3729] An editable list setting (feeds, symbols, hosts) as a submenu: each item a
    /// submenu of its own (Rename… where <see cref="MenuList{T}.Rename"/> is given, the
    /// <see cref="MenuList{T}.Extra"/> lines, Remove), its line dragged up and down to reorder
    /// ([S-3732], <see cref="SettingsSubmenu.Order"/>; Alt+Shift+Up / Down on it), the
    /// <see cref="MenuList{T}.Empty"/> sentence while there are none, then Add… (asked for and checked
    /// by <see cref="MenuList{T}.Add"/>) or the caller's own <see cref="MenuList{T}.AddLines"/>. Every
    /// change is made in <paramref name="items"/> and saved.
    /// </summary>
    public SettingsLine List<T>(string? glyph, string text, IList<T> items, MenuList<T> list)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(list);

        // [S-3732] The items as built: a drop names places in this list, so the move finds the item
        // that stood there even where the list changed under the lines.
        var built = items.ToList();
        void Reorder(int from, int to)
        {
            if (from >= 0 && from < built.Count && Move(items, built[from], from, to))
            {
                _save();
            }
        }

        var lines = new List<SettingsLine>(items.Count + 3);
        if (items.Count == 0)
        {
            lines.Add(Note(list.Empty));
        }

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var at = i;
            var order = new SettingsOrder(i, items.Count, Reorder);
            var actions = new List<SettingsLine>();
            if (list.Rename is { } rename)
            {
                actions.Add(Ask(Glyphs.Rename, Str.Menu_Rename.Text(), new WidgetTextAsk(
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
                    AllowEmpty: rename.AllowEmpty)) with
                {
                    // [S-2351] The name it starts from is the item's head's, right above it: not said twice.
                    Value = null,
                });
            }

            if (list.Extra?.Invoke(item) is { } extra)
            {
                actions.AddRange(extra);
            }

            actions.Add(SettingsLine.Separator);
            actions.Add(Command(Glyphs.Delete, Str.Common_Remove.Text(), () =>
            {
                if (IndexOf(items, item, at) is var found and >= 0)
                {
                    items.RemoveAt(found);
                }
            }, disabled: items.Count <= list.Min));

            var name = list.Name(item);
            lines.Add(new SettingsSubmenu(name, name, list.Glyph?.Invoke(item), actions)
            {
                Inline = true,
                IconFile = list.IconFile?.Invoke(item),
                Order = list.Reorder ? order : null,
            });
        }

        var full = list.Max is { } max && items.Count >= max;
        if (list.Add is { } add)
        {
            lines.Add(SettingsLine.Separator);
            lines.Add(Ask(Glyphs.Add, Str.Menu_Add.Text(), new WidgetTextAsk(
                add.Title,
                add.Placeholder,
                Str.Common_Add.Text(),
                add.Refuse,
                typed =>
                {
                    items.Add(add.Create(typed));
                    _save();
                }), disabled: full));
        }

        if (list.AddLines is { Count: > 0 } addLines)
        {
            lines.Add(SettingsLine.Separator);
            lines.AddRange(full ? addLines.Select(l => l with { Disabled = true }) : addLines);
        }

        return Submenu(glyph, text, lines);
    }

    /// <summary>
    /// [S-3730] A folder setting: its line shows the folder's name at its end (the whole path as it
    /// is otherwise long), and a click opens Windows' folder picker; a folder picked there sets and
    /// saves. Nobody types a path they can point at.
    /// </summary>
    public SettingsCommand PickFolder(string? glyph, string text, string currentPath, Action<string> set)
    {
        ArgumentNullException.ThrowIfNull(set);
        var current = currentPath ?? string.Empty;
        return new SettingsCommand(text, text, glyph, () => _ = FolderBrowse.PickAsync(path =>
        {
            if (!string.Equals(path, current, StringComparison.OrdinalIgnoreCase))
            {
                set(path);
                _save();
            }
        }))
        {
            Value = FolderName(current),
        };
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

    /// <summary>
    /// [S-3732] Move the item to <paramref name="to"/>, the others closing up behind it; where the
    /// list changed under the lines, the item is found again. False when nothing moved (the same
    /// place, a place outside the list, an item gone).
    /// </summary>
    private static bool Move<T>(IList<T> items, T item, int at, int to)
    {
        var from = IndexOf(items, item, at);
        if (from < 0 || to < 0 || to >= items.Count || to == from)
        {
            return false;
        }

        items.RemoveAt(from);
        items.Insert(to, item);
        return true;
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
    public Func<T, IReadOnlyList<SettingsLine>>? Extra { get; init; }

    /// <summary>Add…: a line of text asked for, checked as it is typed, made into an item.</summary>
    public MenuListAdd<T>? Add { get; init; }

    /// <summary>The caller's own add lines (pickers, a submenu of kinds), after the items.</summary>
    public IReadOnlyList<SettingsLine>? AddLines { get; init; }

    /// <summary>The most items the list takes: the add lines are greyed at it.</summary>
    public int? Max { get; init; }

    /// <summary>[S-3729] The fewest items the list keeps (a table's last column): Remove is greyed at it.</summary>
    public int Min { get; init; }

    /// <summary>[S-3732] Whether the order means something (the item lines are dragged to reorder). On by default.</summary>
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
