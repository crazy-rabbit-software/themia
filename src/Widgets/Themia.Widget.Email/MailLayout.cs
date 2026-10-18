using Themia.Core.Localization;
using Themia.Core.Model;

namespace Themia.Core.Mail;

/// <summary>Which lines a list or compact row draws at the widget's current size.</summary>
/// <param name="Inline">[S-1283] One line per message — sender, subject and preview, account, time side by side.</param>
/// <param name="Envelope">[S-1282] The closed or open envelope in front of the sender.</param>
public readonly record struct MailRowLayout(
    bool Subject,
    bool Preview,
    int PreviewLines,
    bool Account,
    bool Time,
    bool Inline = false,
    bool Envelope = false);

/// <summary>
/// [S-1284] A one-line row's column widths, leading to trailing: the sender, the subject line, the
/// account (0 when it is not shown) and the trailing slot that holds the time or, revealed, the
/// quick actions in its place.
/// </summary>
public readonly record struct MailInlineBoxes(double Sender, double Line, double Account, double Slot);

/// <summary>One row-view column at the width it gets: a fixed width, or a share of what is left.</summary>
public readonly record struct MailColumnFit(MailColumn Column, double Width, double Star);

/// <summary>
/// [S-556] Shedding detail to fit the tile, decided in one place. A row never clips or overlaps:
/// the account tag goes first, then the preview, then extra preview lines; in the row view whole
/// columns go, the least essential first, and the first column the user chose always stays.
/// </summary>
public static class MailLayout
{
    /// <summary>Below this width the account tag has no room beside the subject.</summary>
    public const double AccountNeedsWidth = 260;

    /// <summary>Below this width a time beside the sender leaves the name a handful of letters.</summary>
    public const double TimeNeedsWidth = 110;

    /// <summary>Below this content height one more line per message costs a whole message.</summary>
    public const double PreviewNeedsHeight = 120;

    /// <summary>[S-1283] From this width a list row is one line whatever the height: the line is long enough to say it all.</summary>
    public const double InlineFromWidth = 640;

    /// <summary>[S-1283] A strip at least this wide and shorter than <see cref="InlineStripBelowHeight"/> goes one line per message.</summary>
    public const double InlineStripWidth = 420;

    /// <summary>[S-1283] Under this height a wide tile is a strip: two-line rows would show two messages.</summary>
    public const double InlineStripBelowHeight = 220;

    /// <summary>[S-1283] On one line the preview needs this much width beside the subject to say anything.</summary>
    public const double InlinePreviewNeedsWidth = 440;

    /// <summary>[S-1283] On one line the account column needs this much width.</summary>
    public const double InlineAccountNeedsWidth = 560;

    /// <summary>[S-1282] From this width the envelope glyph costs the sender nothing it needs.</summary>
    public const double EnvelopeNeedsWidth = 300;

    /// <summary>[S-1287] From this width the row view adds the preview column when the subject would hog the line.</summary>
    public const double RoomyPreviewFromWidth = 700;

    /// <summary>[S-1287] The share of the line a lone subject column may take before the preview joins it.</summary>
    public const double RoomySubjectShare = 0.4;

    /// <summary>[S-1283] The narrowest a one-line row's time is drawn, so "now" and "12:45" line up.</summary>
    public const double InlineTimeWidth = 36;

    /// <summary>[S-1284] How wide a row of <paramref name="count"/> quick-action buttons is.</summary>
    public static double QuickActionsWidth(int count, double button, double gap)
        => count <= 0 ? 0 : (count * button) + ((count - 1) * gap);

    /// <summary>
    /// [S-1284] A one-line row's columns at <paramref name="inner"/> (the row's inner width). The
    /// trailing slot is as wide as the wider of the time and the quick actions whether or not the
    /// actions are showing, so revealing them under the pointer or the keyboard swaps the slot's
    /// content and moves nothing else: the sender, the subject line and the account keep their
    /// boxes, and the subject truncates in the same place.
    /// </summary>
    /// <param name="account">The account column's width; 0 when it is not shown.</param>
    /// <param name="actions">The quick actions' width (<see cref="QuickActionsWidth"/>); 0 when the row has none.</param>
    public static MailInlineBoxes InlineBoxes(double inner, double gap, double account, double actions)
    {
        var sender = Math.Clamp(inner * 0.24, 96, 200);
        var slot = Math.Max(InlineTimeWidth, actions);
        var gaps = (account > 0 ? 3 : 2) * gap;
        var line = Math.Max(0, inner - sender - account - slot - gaps);
        return new MailInlineBoxes(sender, line, account, slot);
    }

    public static MailRowLayout Rows(EmailConfig config, double width, double height)
    {
        ArgumentNullException.ThrowIfNull(config);

        var envelope = width >= EnvelopeNeedsWidth;

        if (config.Style != EmailStyle.Table
            && (width >= InlineFromWidth || (width >= InlineStripWidth && height < InlineStripBelowHeight)))
        {
            var account = config.Shows(MailListField.Account) && width >= InlineAccountNeedsWidth;
            return new MailRowLayout(
                config.Shows(MailListField.Subject),
                config.ShowsPreview && width >= InlinePreviewNeedsWidth,
                1,
                account,
                Time: true,
                Inline: true,
                Envelope: envelope);
        }

        var preview = config.ShowsPreview && height >= PreviewNeedsHeight && width >= 180;
        var lines = preview ? (height < 2 * PreviewNeedsHeight ? 1 : config.EffectivePreviewLines) : 0;

        var tag = config.Shows(MailListField.Account) && width >= AccountNeedsWidth;
        return new MailRowLayout(
            config.Shows(MailListField.Subject),
            preview,
            lines,
            tag,
            width >= TimeNeedsWidth,
            Envelope: envelope);
    }

    /// <summary>The narrowest each column can be and still say anything.</summary>
    public static double MinWidth(MailColumn column) => column switch
    {
        MailColumn.Date => 56,
        MailColumn.Account => 84,
        MailColumn.From => 80,
        _ => 96,
    };

    /// <summary>The order columns are given up in when the tile is too narrow for all of them.</summary>
    private static int Expendability(MailColumn column) => column switch
    {
        MailColumn.Preview => 0,
        MailColumn.Account => 1,
        MailColumn.Date => 2,
        MailColumn.Subject => 3,
        _ => 4,
    };

    /// <summary>
    /// The row view's columns at <paramref name="width"/> (the row's inner width): From, Date and
    /// Account have fixed widths, Subject and Preview share what is left. The most essential columns
    /// are kept first as long as their minimums fit — so a narrow tile keeps From and Date rather
    /// than From alone — and the user's order is kept. The most essential column always stays.
    /// </summary>
    public static IReadOnlyList<MailColumnFit> Columns(IReadOnlyList<MailColumn> chosen, double width, double gap)
    {
        ArgumentNullException.ThrowIfNull(chosen);

        var distinct = chosen.Distinct().ToList();
        var keep = new HashSet<MailColumn>();
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

        // [S-1287] A wide row with the subject as its only flexible column leaves most of the line
        // to a subject that is rarely that long; the preview fills it instead, after the subject.
        if (width >= RoomyPreviewFromWidth && !kept.Contains(MailColumn.Preview) && kept.Contains(MailColumn.Subject))
        {
            var fixedWidth = kept.Where(c => c != MailColumn.Subject).Sum(c => c == MailColumn.From ? Math.Clamp(width * 0.28, MinWidth(c), 200) : MinWidth(c));
            var share = (width - fixedWidth - ((kept.Count - 1) * gap)) / width;

            if (share > RoomySubjectShare && share * width >= MinWidth(MailColumn.Subject) + MinWidth(MailColumn.Preview) + gap)
            {
                kept.Insert(kept.IndexOf(MailColumn.Subject) + 1, MailColumn.Preview);
            }
        }

        var hasStar = kept.Any(c => c is MailColumn.Subject or MailColumn.Preview);

        return
        [
            .. kept.Select(c => c switch
            {
                MailColumn.Subject or MailColumn.Preview => new MailColumnFit(c, 0, 1),

                // With no flexible column the first one takes the slack, so the row still fills.
                _ when !hasStar && c == kept[0] => new MailColumnFit(c, 0, 1),
                MailColumn.From => new MailColumnFit(c, Math.Clamp(width * 0.28, MinWidth(c), 200), 0),
                _ => new MailColumnFit(c, MinWidth(c), 0),
            }),
        ];
    }
}
