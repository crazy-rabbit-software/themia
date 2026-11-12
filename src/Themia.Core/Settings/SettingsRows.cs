using Themia.Sdk.Kit;

namespace Themia.Core.Settings;

/// <summary>
/// [S-2302] What a row of a settings page is, Karpo's row vocabulary (karpo-app, docs/widgets.md §575,
/// §594, §670): the page draws every row by its kind, and a kind is decided here once for the
/// settings' lines and the selection's entries alike, so two rows that do the same are drawn alike.
/// </summary>
public enum SettingsRowKind
{
    /// <summary>A switch with On / Off before it.</summary>
    Toggle,

    /// <summary>A number box: 240 × 32, − and + inside, a typed value held to the range.</summary>
    Number,

    /// <summary>A text box: 240 × 32; Enter or leaving keeps it, Escape restores it.</summary>
    Text,

    /// <summary>An action button at the row's end.</summary>
    Action,

    /// <summary>A link row: a setting another page owns, a chevron at its end.</summary>
    Link,

    /// <summary>One of the user's own things (a view): icon buttons at its end, dragged or Ctrl+Up / Down to reorder.</summary>
    Item,

    /// <summary>Words only (a reading, a note, what went wrong).</summary>
    Note,

    /// <summary>A heading inside a card (a group of a sub-page).</summary>
    Heading,

    /// <summary>An option of a choice standing open on the card (a radio row, 36 tall).</summary>
    Radio,

    /// <summary>A choice of one as an inline picker: an expander whose radio rows open inside the card.</summary>
    Choice,

    /// <summary>A long choice as an inline lookup: a search box over at most eight results and a status line.</summary>
    Lookup,

    /// <summary>A colour or a fill as an inline picker: the swatches, then the custom picker.</summary>
    Colour,

    /// <summary>A font as an inline picker.</summary>
    Font,

    /// <summary>An icon as an inline picker.</summary>
    Icon,

    /// <summary>A group of rows opened inside its card (the one second level a card has).</summary>
    Expander,

    /// <summary>A page of its own under this one, behind a back head (an account).</summary>
    SubPage,

    /// <summary>Where one card ends and the next begins.</summary>
    Break,
}

/// <summary>[S-2302] The rules that sort a page's lines into rows and cards. Pure.</summary>
public static class SettingsRows
{
    /// <summary>
    /// [S-2302] The kind of <paramref name="line"/>. A submenu opens inside its card (an expander) unless it
    /// is a page of its own (<see cref="SettingsSubmenu.Page"/>) or holds a submenu itself: a card has one
    /// level opened inside it and never a third ([S-2303], Karpo's §594).
    /// </summary>
    public static SettingsRowKind KindOf(SettingsLine line)
    {
        ArgumentNullException.ThrowIfNull(line);
        return line switch
        {
            SettingsToggle => SettingsRowKind.Toggle,
            SettingsSlider => SettingsRowKind.Number,
            SettingsField or SettingsAsk => SettingsRowKind.Text,
            SettingsCommand => SettingsRowKind.Action,
            SettingsLink => SettingsRowKind.Link,
            SettingsItem => SettingsRowKind.Item,
            SettingsNote or SettingsReading => SettingsRowKind.Note,
            SettingsHeading => SettingsRowKind.Heading,
            SettingsRadio => SettingsRowKind.Radio,
            SettingsChoice { Filter: not null } => SettingsRowKind.Lookup,
            SettingsChoice => SettingsRowKind.Choice,
            SettingsColour or SettingsFill => SettingsRowKind.Colour,
            SettingsFont => SettingsRowKind.Font,
            SettingsIcon => SettingsRowKind.Icon,
            SettingsSubmenu submenu => IsPage(submenu) ? SettingsRowKind.SubPage : SettingsRowKind.Expander,
            SettingsSeparator => SettingsRowKind.Break,
            _ => throw new ArgumentOutOfRangeException(nameof(line), line.GetType().Name, "a settings line no page draws"),
        };
    }

    /// <summary>[S-2303] A submenu that is a page of its own: marked so, or one whose own lines open further.</summary>
    public static bool IsPage(SettingsSubmenu submenu)
    {
        ArgumentNullException.ThrowIfNull(submenu);
        return submenu.Page || submenu.Lines.Any(l => l is SettingsSubmenu);
    }

    /// <summary>
    /// [S-2304] The cards of a section, Windows' settings cards: each row a card of its own, with the notes
    /// under it (what holds against it, how a try went: they only matter after it); options of a choice
    /// standing open, and the user's own things of one list, share one card; a heading's card holds the
    /// rows under it to the next break. A break ends a card; no card is empty.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<SettingsLine>> Cards(IReadOnlyList<SettingsLine> lines) => Cards(lines, KindOf);

    /// <summary>[S-2304] The same parting for any rows whose kinds are known (a page's rows built from the selection's lines).</summary>
    public static IReadOnlyList<IReadOnlyList<T>> Cards<T>(IReadOnlyList<T> rows, Func<T, SettingsRowKind> kind)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(kind);

        var cards = new List<IReadOnlyList<T>>();
        var card = new List<T>();
        var headed = false;
        SettingsRowKind? last = null;
        void Close()
        {
            if (card.Count > 0)
            {
                cards.Add(card);
                card = [];
            }

            headed = false;
            last = null;
        }

        foreach (var row in rows)
        {
            var now = kind(row);
            switch (now)
            {
                case SettingsRowKind.Break:
                    Close();
                    continue;
                case SettingsRowKind.Heading:
                    Close();
                    headed = true;
                    break;
                case SettingsRowKind.Note:
                    break;
                case SettingsRowKind.Radio or SettingsRowKind.Item when last == now:
                    break;
                default:
                    if (!headed)
                    {
                        Close();
                    }

                    break;
            }

            card.Add(row);
            last = now;
        }

        Close();
        return cards;
    }

    /// <summary>
    /// [S-2305] A row is 60 DIPs tall where it has a caption under its name, else 48 (Karpo's one line
    /// and two). A note is as tall as its words; that is the page's to measure.
    /// </summary>
    public static double Height(bool caption) => caption ? ShellMetrics.Page.TwoLines : ShellMetrics.Page.OneLine;
}
