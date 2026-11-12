using Themia.Core.Widgets;

namespace Themia.Sdk.Kit;

/// <summary>
/// The row looks a kit list draws. Every look is the family's Row block ([S-6711]): one row of 28, 40
/// or 64 for one, two or three lines by the tier ([S-6712], <see cref="RowRules.Lines"/>), never by
/// the length of its text; the name (the item's title) primary, Strong for the one emphasised row
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

    /// <summary>[S-7032] The same row as <see cref="TwoLine"/> held to one line at every tier: the sender and the time.</summary>
    Line,

    /// <summary>
    /// [S-7034] One line of the user's columns in the user's order (<see cref="KitColumn"/>), the
    /// least essential shed first, with the columns' titles over them where the height affords it. Mail.
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

    /// <summary>[S-7037] One line per row at every tier (a compact style): the subtitle line left out.</summary>
    public bool OneLine { get; init; }

    /// <summary>
    /// [S-2440] A list that states its rows (<see cref="IKitRows"/>): its plan is the same at every size
    /// in its range (its lines, one column, every field it has on), no tier decides it. Null: the
    /// legacy ladder, rows fitted by height ([S-2443]).
    /// </summary>
    public KitRowsChoice? Rows { get; init; }
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
/// many columns it may grow to, how its time is written, and its sentences. The widget's own data,
/// so its breakpoints live in one place, <see cref="KitLadder.Plan"/>.
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
    /// item's point: kept at every width, where a time gives way below 180; the title ellipsises first.
    /// </summary>
    public bool Readings { get; init; }

    /// <summary>
    /// [S-2270] The rows tell unread from read by their names alone (a mailbox): the emphasised one
    /// (<see cref="KitItem.Emphasis"/>) BodyStrong in the primary role, every other Body in the secondary
    /// (<see cref="RowRules.Roles"/> quiet), so no lead glyph is needed to say it.
    /// </summary>
    public bool Unread { get; init; }
}

/// <summary>[S-7030] What one size of a kit list shows; chosen by <see cref="KitLadder.Plan"/>.</summary>
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
/// [S-7031] The kit's disclosure ladder ([S-660]..[S-666]), the one place a kit list's breakpoints
/// live. Rows are the family's ([S-6711], [S-6712]): a tile shows more lines per row as it grows both
/// ways (one while compact either way, two from regular, three from expanded) and as many whole rows
/// as fit; from 400 wide a spec may go to two columns (three from 720) while a column keeps 320. In a
/// row the title always shows; the lead and the time from a column of 180, the badge from 260.
/// Concise first ([S-666]): no rung adds a control or a pane.
/// </summary>
public static class KitLadder
{
    /// <summary>A column narrower than this cannot hold a one-line title worth reading beside its time.</summary>
    public const double MinColumn = 320;

    /// <summary>From here a row carries its lead.</summary>
    public const double LeadNeedsWidth = 180;

    /// <summary>[S-7032] Below this width the badge has no room after the subtitle.</summary>
    public const double BadgeNeedsWidth = 260;

    /// <summary>[S-7032] Below this width (a compact one) a time beside the title leaves it a handful of letters.</summary>
    public const double WhenNeedsWidth = 180;

    /// <summary>[S-7034] Below this height the table's column titles cost a row.</summary>
    public const double ColumnHeaderNeedsHeight = 140;

    /// <summary>[S-7034] The table's column titles: one Caption line and the gap under it.</summary>
    public static double ColumnHeaderHeight => FamilyType.Caption.LineHeight + FamilySpace.S2;

    /// <summary>[S-7034] From this width the table adds the preview column when the subtitle would hog the line.</summary>
    public const double RoomyPreviewFromWidth = 700;

    /// <summary>[S-7034] The share of the line a lone subtitle column may take before the preview joins it.</summary>
    public const double RoomySubtitleShare = 0.4;

    /// <summary>[S-7034] The gap between table columns: the family's column gap.</summary>
    public const double ColumnGap = FamilySpace.ColumnGap;

    /// <summary>[S-1267] From this width a schedule flows its days into two columns.</summary>
    public const double TwoScheduleColumnsFrom = 720;

    /// <param name="shows">The optional fields the user has on (the spec's <see cref="KitSpec.Optional"/> bounds them).</param>
    public static KitPlan Plan(WidgetArea area, KitSpec spec, KitShow shows) => Plan(area, spec, new KitChoices(shows));

    /// <param name="choices">What the user chose: the optional fields (bounded by the spec's <see cref="KitSpec.Optional"/>), a table's columns.</param>
    public static KitPlan Plan(WidgetArea area, KitSpec spec, KitChoices choices)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(choices);

        return spec.Look switch
        {
            KitRowLook.Schedule => Scheduled(area),
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

        if (choices.Rows is { } sized && spec.Look != KitRowLook.Table)
        {
            return Sized(area, spec, sized, Has);
        }

        var width = Math.Max(0, area.Width);
        var columns = Math.Min(Math.Max(1, spec.MaxColumns), width >= 720 ? 3 : width >= 400 ? 2 : 1);
        while (columns > 1 && width / columns < MinColumn)
        {
            columns--;
        }

        var column = width / columns;
        var measured = area.IsMeasured;
        var lead = Has(KitShow.Lead) && (!measured || column >= LeadNeedsWidth);
        var when = Has(KitShow.When) && (!measured || column >= WhenNeedsWidth || spec.Readings);

        if (spec.Look == KitRowLook.Table)
        {
            var chosen = choices.Columns is { Count: > 0 } picked ? picked : [KitColumn.Title];
            var fits = TableColumns(chosen, InnerWidth(column, lead), ColumnGap);
            var header = measured && area.Height >= ColumnHeaderNeedsHeight;
            return new KitPlan(KitRowLook.Table, 1, column, 1, lead, false, false, false, ColumnHeader: header, Table: fits);
        }

        var badges = Has(KitShow.Badges) && measured && column >= BadgeNeedsWidth;
        var second = Has(KitShow.Subtitle) || badges;
        var preview = Has(KitShow.Preview);
        var wanted = spec.Look == KitRowLook.Line || choices.OneLine ? 1 : 1 + (second ? 1 : 0) + (preview ? 1 : 0);
        var lines = measured ? RowRules.Lines(area, wanted) : 1;
        var subtitle = second && lines >= 2;
        var shown = preview && lines >= (subtitle ? 3 : 2);

        return new KitPlan(spec.Look, columns, column, lines, lead, subtitle, shown, when, badges && subtitle, RowHeight: FamilyShape.RowHeight(lines));
    }

    /// <summary>
    /// [S-2440] A list that states its rows: one column, the lines it chose (a one-line look keeps one),
    /// every field it has on whatever the width (text is cut with its ellipsis, never dropped), the
    /// subtitle on the second line where it has two, the preview where the lines leave room.
    /// </summary>
    private static KitPlan Sized(WidgetArea area, KitSpec spec, KitRowsChoice rows, Func<KitShow, bool> has)
    {
        var lines = spec.Look == KitRowLook.Line ? 1 : KitSizing.ClampLines(rows.Lines);
        var subtitle = (has(KitShow.Subtitle) || has(KitShow.Badges)) && lines >= 2;
        var preview = has(KitShow.Preview) && lines >= (subtitle ? 3 : 2);
        return new KitPlan(spec.Look, 1, Math.Max(0, area.Width), lines, has(KitShow.Lead), subtitle, preview, has(KitShow.When), has(KitShow.Badges) && subtitle, RowHeight: FamilyShape.RowHeight(lines));
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

    /// <summary>The order columns are given up in when the tile is too narrow for all of them.</summary>
    private static int Expendability(KitColumn column) => column switch
    {
        KitColumn.Preview => 0,
        KitColumn.Badge => 1,
        KitColumn.When => 2,
        KitColumn.Subtitle => 3,
        _ => 4,
    };

    /// <summary>
    /// [S-7034] A table's columns at <paramref name="width"/> (the row's inner width): the title, the
    /// time and the badge have fixed widths, the subtitle and the preview share what is left. The most
    /// essential columns are kept first as long as their minimums fit, in the user's order, so a narrow
    /// tile keeps the title and the time rather than the title alone; the most essential always stays.
    /// A wide row with the subtitle as its only flexible column gives the rest of the line to the
    /// preview, after the subtitle.
    /// </summary>
    public static IReadOnlyList<KitColumnFit> TableColumns(IReadOnlyList<KitColumn> chosen, double width, double gap)
    {
        ArgumentNullException.ThrowIfNull(chosen);

        var distinct = chosen.Distinct().ToList();
        var keep = new HashSet<KitColumn>();
        var used = 0.0;

        foreach (var column in distinct.OrderByDescending(Expendability))
        {
            var cost = MinWidth(column) + (keep.Count > 0 ? gap : 0);
            if (keep.Count == 0 || used + cost <= width)
            {
                keep.Add(column);
                used += cost;
            }
        }

        var kept = distinct.Where(keep.Contains).ToList();

        if (width >= RoomyPreviewFromWidth && !kept.Contains(KitColumn.Preview) && kept.Contains(KitColumn.Subtitle))
        {
            var fixedWidth = kept.Where(c => c != KitColumn.Subtitle).Sum(c => c == KitColumn.Title ? Math.Clamp(width * 0.28, MinWidth(c), 200) : MinWidth(c));
            var share = (width - fixedWidth - ((kept.Count - 1) * gap)) / width;

            if (share > RoomySubtitleShare && share * width >= MinWidth(KitColumn.Subtitle) + MinWidth(KitColumn.Preview) + gap)
            {
                kept.Insert(kept.IndexOf(KitColumn.Subtitle) + 1, KitColumn.Preview);
            }
        }

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
    /// [S-7036] The schedule's plan: two columns from 720, rows of two lines (the title and its start,
    /// the span and where under them) from regular both ways and of one on a compact tile, headings
    /// one row of 28.
    /// </summary>
    private static KitPlan Scheduled(WidgetArea area)
    {
        var width = Math.Max(0, area.Width);
        var columns = width >= TwoScheduleColumnsFrom ? 2 : 1;
        var lines = area.IsMeasured ? RowRules.Lines(area, 2) : 1;
        return new KitPlan(KitRowLook.Schedule, columns, width / columns, lines, true, lines >= 2, false, true, RowHeight: FamilyShape.RowHeight(lines));
    }
}
