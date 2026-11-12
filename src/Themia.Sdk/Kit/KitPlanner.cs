using Themia.Core.Widgets;

namespace Themia.Sdk.Kit;

/// <summary>
/// The row looks a kit list draws. Every look is the family's Row block ([S-6711]): one row of 28, 40
/// or 56 for the one, two or three lines its config states ([S-2440], [S-6712]), never by the length
/// of its text; the name (the item's title) primary, Strong for the one emphasised row
/// (unread, today), the value (the time) secondary at the end, a second line in Caption secondary, a
/// third in Caption tertiary; the lead a 16 glyph or the site's icon. No divider.
/// </summary>
public enum KitRowLook
{
    /// <summary>
    /// [S-7031] An article: the title, the time as its value, the source (with its icon as the lead)
    /// on the second line, the preview on the third. Rows in up to the spec's columns. Feeds.
    /// </summary>
    Rich,

    /// <summary>
    /// [S-7032] A message: the sender, the time as its value, the subject (and the account's badge
    /// after it) on the second line, the preview on the third; the lead the message's own glyph. Mail.
    /// </summary>
    TwoLine,

    /// <summary>[S-7032] The same row as <see cref="TwoLine"/> held to one line: the sender and the time.</summary>
    Line,

    /// <summary>
    /// [S-7034] One line of the user's columns in the user's order (<see cref="KitColumn"/>), each at
    /// least its own width, with the columns' titles over them where the config has them. Mail.
    /// </summary>
    Table,

    /// <summary>
    /// [S-7036] A scheduled item: its calendar's colour as a 2 px bar in the lead, the title, the
    /// start as its value, the span and where on the second line. Rows of one height in pages with
    /// day headings ([S-7035]). The agenda.
    /// </summary>
    Schedule,

    /// <summary>
    /// [S-7037] An item with a check: the lead is the check (a ring, the accent's filled tick when done,
    /// the ring in the item's tone when it is late), the title struck through when done, the due as its
    /// value in the item's tone, the subtitle (a list's name) on the second line. Rows in one or two
    /// columns filled column by column, whole rows only, the done group under its heading, reordered
    /// by a drag, renamed in place, an add row under them ([S-7038]). Tasks.
    /// </summary>
    Check,
}

/// <summary>[S-7034] A table look's column: which field of the item it shows.</summary>
public enum KitColumn
{
    Title,
    Subtitle,
    Preview,
    When,

    /// <summary>The item's first badge.</summary>
    Badge,
}

/// <summary>[S-7034] One table column at the width it gets: a fixed width, or a share of what is left.</summary>
public readonly record struct KitColumnFit(KitColumn Column, double Width, double Star);

/// <summary>[S-7030] What the user chose for a kit list: the optional fields on and a table's columns in their order.</summary>
public sealed record KitChoices(KitShow Shows, IReadOnlyList<KitColumn>? Columns = null)
{
    /// <summary>[S-7037] How many items the list holds (two columns only when one would not hold them).</summary>
    public int Count { get; init; }

    /// <summary>[S-7038] An item can be added (the add row has a purpose).</summary>
    public bool CanAdd { get; init; }

    /// <summary>[S-7037] One line per row (a compact style): the subtitle line left out.</summary>
    public bool OneLine { get; init; }

    /// <summary>
    /// [S-2440] [S-11580] The rows the list states (<see cref="IKitRows"/>; a list without its own, the
    /// kit's default six of one line): its plan is the same at every size in its range (its lines, one
    /// column, every field it has on), no tier decides it.
    /// </summary>
    public KitRowsChoice Rows { get; init; } = new(KitSizing.DefaultRows, 1);
}

/// <summary>[S-7030] The optional fields of a kit row, which a spec offers and the user may turn off.</summary>
[Flags]
public enum KitShow
{
    None = 0,

    /// <summary>The lead (an icon, the site's icon).</summary>
    Lead = 1,

    /// <summary>The subtitle line.</summary>
    Subtitle = 2,

    /// <summary>The preview lines.</summary>
    Preview = 4,

    /// <summary>The time.</summary>
    When = 8,

    /// <summary>The first badge (an account's name).</summary>
    Badges = 16,

    All = Lead | Subtitle | Preview | When | Badges,
}

/// <summary>What happens to items that do not fit.</summary>
public enum KitOverflow
{
    /// <summary>A scroll away, under the shared fade ([S-1281]).</summary>
    Scroll,

    /// <summary>[S-7035] Pages of the rows that fit whole, turned by the widget's own controls and keys; never scrolled.</summary>
    Page,
}

/// <summary>
/// [S-7030] A kit list widget's shape: which look its rows have, which optional fields exist, how
/// many columns it may have, how its time is written, and its sentences. The widget's own data,
/// planned in one place, <see cref="KitPlanner.Plan"/>.
/// </summary>
/// <param name="Untitled">The title of an item that has none, in the current language.</param>
/// <param name="Empty">What the widget says when its sources answered with nothing ([S-7040]).</param>
public sealed record KitSpec(
    KitRowLook Look,
    KitShow Optional,
    Func<string> Untitled,
    Func<string> Empty,
    KitTime Time = KitTime.Age,
    KitOverflow Overflow = KitOverflow.Scroll,
    int MaxColumns = 1)
{
    /// <summary>[S-7034] A table column's title, in the current language.</summary>
    public Func<KitColumn, string>? ColumnHeading { get; init; }

    /// <summary>[S-7034] How a table's time column writes it.</summary>
    public KitTime ColumnTime { get; init; } = KitTime.Stamp;

    /// <summary>[S-7038] The check look's add row ("Add a task"), in the current language.</summary>
    public Func<string>? AddLabel { get; init; }

    /// <summary>[S-7038] The add row's box, empty ("Task name").</summary>
    public Func<string>? AddPlaceholder { get; init; }

    /// <summary>
    /// [S-7073] The row's value is a reading (<see cref="KitItem.Value"/>: a battery's level), the
    /// item's point: the title ellipsises first.
    /// </summary>
    public bool Readings { get; init; }

    /// <summary>
    /// [S-2270] The rows tell unread from read by their names alone (a mailbox): the emphasised one
    /// (<see cref="KitItem.Emphasis"/>) BodyStrong in the primary role, every other Body in the secondary
    /// (<see cref="RowRules.Roles"/> quiet), so no lead glyph is needed to say it.
    /// </summary>
    public bool Unread { get; init; }
}

/// <summary>[S-7030] What a kit list shows, the same at every size of its range; made by <see cref="KitPlanner.Plan"/>.</summary>
/// <param name="Columns">How many columns of rows, read left to right, then down (a check look's: top to bottom, then across).</param>
/// <param name="ColumnWidth">The width one column is laid out in (0 before the box is measured).</param>
/// <param name="Lines">[S-6712] The lines every row shows (1 to 3); its height is <see cref="RowHeight"/>.</param>
/// <param name="Lead">The row carries its lead.</param>
/// <param name="Subtitle">The subtitle (with the badge after it, <see cref="Badges"/>) on the second line.</param>
/// <param name="Preview">The preview: on the third line under a subtitle, else on the second.</param>
/// <param name="When">The time, as the row's value.</param>
/// <param name="Badges">[S-7032] The first badge after the subtitle.</param>
/// <param name="ColumnHeader">[S-7034] The table's column titles over its rows.</param>
/// <param name="Table">[S-7034] The table's columns at their widths, in the user's order; null for any other look.</param>
/// <param name="RowHeight">The rows' one height (<see cref="FamilyShape.RowHeight"/> of <see cref="Lines"/>).</param>
/// <param name="HeadingHeight">[S-7035] A group heading's height: one row of 28.</param>
public readonly record struct KitPlan(
    KitRowLook Look,
    int Columns,
    double ColumnWidth,
    int Lines,
    bool Lead,
    bool Subtitle,
    bool Preview,
    bool When,
    bool Badges = false,
    bool ColumnHeader = false,
    IReadOnlyList<KitColumnFit>? Table = null,
    double RowHeight = FamilyShape.Row1,
    double HeadingHeight = FamilyShape.Row1)
{
    /// <summary>[S-7038] The add row under the list.</summary>
    public bool AddRow { get; init; }

    /// <summary>[S-7037] A group's heading over its rows (the done ones).</summary>
    public bool Headings { get; init; }

    /// <summary>[S-7037] The time only when it is urgent (overdue, today).</summary>
    public bool UrgentOnly { get; init; }

    /// <summary>[S-7037] The time in its long words, with its date.</summary>
    public bool LongWhen { get; init; }
}

/// <summary>
/// [S-11580] The kit's planner, the one place a kit list's plan is made: the rows, lines, columns and
/// fields its config states ([S-2440]), the same at every size of its range ([S-11800]). Rows are the
/// family's ([S-6711], [S-6712]).
/// </summary>
public static class KitPlanner
{
    /// <summary>[S-7034] The table's column titles: one Caption line and the gap under it.</summary>
    public static double ColumnHeaderHeight => FamilyType.Caption.LineHeight + FamilySpace.S2;

    /// <summary>[S-7034] The gap between table columns: the family's column gap.</summary>
    public const double ColumnGap = FamilySpace.ColumnGap;

    /// <param name="shows">The optional fields the user has on (the spec's <see cref="KitSpec.Optional"/> bounds them).</param>
    public static KitPlan Plan(WidgetArea area, KitSpec spec, KitShow shows) => Plan(area, spec, new KitChoices(shows));

    /// <param name="choices">What the user chose: the optional fields (bounded by the spec's <see cref="KitSpec.Optional"/>), a table's columns.</param>
    public static KitPlan Plan(WidgetArea area, KitSpec spec, KitChoices choices)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(choices);

        return spec.Look switch
        {
            KitRowLook.Schedule => Scheduled(area, choices),
            KitRowLook.Check => KitChecks.Plan(area, spec, choices),
            _ => Rows(area, spec, choices),
        };
    }

    /// <summary>The height a list has for its rows in <paramref name="area"/>: the view's, less the card's bottom inset.</summary>
    public static double ListHeight(WidgetArea area) => Math.Max(0, area.Height - FamilySpace.Inset);

    /// <summary>[S-6712] Whole rows of <paramref name="plan"/> in a list <paramref name="height"/> tall, under the table's column titles when it has them.</summary>
    public static int Fit(KitPlan plan, double height)
        => RowRules.Fit(height - (plan.ColumnHeader ? ColumnHeaderHeight : 0), plan.Lines);

    private static KitPlan Rows(WidgetArea area, KitSpec spec, KitChoices choices)
    {
        var on = choices.Shows & spec.Optional;
        bool Has(KitShow field) => (on & field) == field;

        return spec.Look == KitRowLook.Table ? SizedTable(area, choices, choices.Rows, Has) : Sized(area, spec, choices.Rows, Has);
    }

    /// <summary>
    /// [S-2440] A list that states its rows: one column, the lines it chose (a one-line look keeps one),
    /// every field it has on whatever the width (text is cut with its ellipsis, never dropped), the
    /// subtitle on the second line where it has two, the preview where the lines leave room.
    /// </summary>
    private static KitPlan Sized(WidgetArea area, KitSpec spec, KitRowsChoice rows, Func<KitShow, bool> has)
    {
        var lines = spec.Look == KitRowLook.Line ? 1 : KitSizing.ClampLines(rows.Lines, KitSizing.MaxLines);
        var subtitle = (has(KitShow.Subtitle) || has(KitShow.Badges)) && lines >= 2;
        var preview = has(KitShow.Preview) && lines >= (subtitle ? 3 : 2);
        var columns = KitSizing.ClampColumns(rows.Columns);
        return new KitPlan(spec.Look, columns, Math.Max(0, area.Width) / columns, lines, has(KitShow.Lead), subtitle, preview, has(KitShow.When), has(KitShow.Badges) && subtitle, RowHeight: FamilyShape.RowHeight(lines));
    }

    /// <summary>
    /// [S-11401] A table that states its rows: one line a row, every one of the user's columns
    /// (<see cref="TableColumns"/>; its width range holds them, <see cref="TableLeast"/>), its column
    /// titles where the user has them on.
    /// </summary>
    private static KitPlan SizedTable(WidgetArea area, KitChoices choices, KitRowsChoice rows, Func<KitShow, bool> has)
    {
        var width = Math.Max(0, area.Width);
        var lead = has(KitShow.Lead);
        var chosen = choices.Columns is { Count: > 0 } picked ? picked : [KitColumn.Title];
        var fits = TableColumns(chosen, InnerWidth(width, lead), ColumnGap);
        return new KitPlan(KitRowLook.Table, 1, width, 1, lead, false, false, false, ColumnHeader: rows.ColumnTitles, Table: fits);
    }

    /// <summary>A row's width inside its side insets and past its lead: what its text has to share.</summary>
    public static double InnerWidth(double width, bool lead)
        => Math.Max(0, width - (2 * FamilySpace.Inset) - (lead ? RowRules.NameStart(FamilyShape.Icon) : 0));

    /// <summary>[S-7034] The narrowest each column can be and still say anything.</summary>
    public static double MinWidth(KitColumn column) => column switch
    {
        KitColumn.When => 56,
        KitColumn.Badge => 84,
        KitColumn.Title => 80,
        _ => 96,
    };

    /// <summary>
    /// [S-11401] The least card width a table of <paramref name="chosen"/> columns takes: every column at
    /// its <see cref="MinWidth"/>, the gaps between them, the row's insets and its lead. A table's width
    /// range starts there, so no width drops a column.
    /// </summary>
    public static double TableLeast(IReadOnlyList<KitColumn> chosen, bool lead)
    {
        ArgumentNullException.ThrowIfNull(chosen);
        var columns = chosen.Distinct().ToList();
        var text = columns.Sum(MinWidth) + (Math.Max(0, columns.Count - 1) * ColumnGap);
        return Math.Ceiling(text + (2 * FamilySpace.Inset) + (lead ? RowRules.NameStart(FamilyShape.Icon) : 0));
    }

    /// <summary>
    /// [S-7034] [S-11401] A table's columns at <paramref name="width"/> (the row's inner width), every one the
    /// user chose, in the user's order: the title, the time and the badge have fixed widths, the subtitle and
    /// the preview share what is left (with neither, the first column takes the slack).
    /// </summary>
    public static IReadOnlyList<KitColumnFit> TableColumns(IReadOnlyList<KitColumn> chosen, double width, double gap)
    {
        ArgumentNullException.ThrowIfNull(chosen);

        var kept = chosen.Distinct().ToList();
        var hasStar = kept.Any(c => c is KitColumn.Subtitle or KitColumn.Preview);

        return
        [
            .. kept.Select(c => c switch
            {
                KitColumn.Subtitle or KitColumn.Preview => new KitColumnFit(c, 0, 1),

                // With no flexible column the first one takes the slack, so the row still fills.
                _ when !hasStar && c == kept[0] => new KitColumnFit(c, 0, 1),
                KitColumn.Title => new KitColumnFit(c, Math.Clamp(width * 0.28, MinWidth(c), 200), 0),
                _ => new KitColumnFit(c, MinWidth(c), 0),
            }),
        ];
    }

    // ---- the schedule look ([S-7035], [S-7036]) -------------------------------------------------

    /// <summary>
    /// [S-7036] The schedule's plan: one column, rows of the lines its config states (the title and its
    /// start, the span and where under them), headings one row of 28.
    /// </summary>
    private static KitPlan Scheduled(WidgetArea area, KitChoices choices)
    {
        // [S-11541] A schedule that states its rows: one column, its lines (the span and where on the
        // second) at every size; the pages hold the rows its height was made for.
        var chosen = KitSizing.ClampLines(choices.Rows.Lines);
        return new KitPlan(KitRowLook.Schedule, 1, Math.Max(0, area.Width), chosen, true, chosen >= 2, false, true, RowHeight: FamilyShape.RowHeight(chosen));
    }
}
