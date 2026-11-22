using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Shell;
using Themia.App.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Sdk.WinUI.Kit;

/// <summary>
/// [S-7037] The check look: a Row block with the check as its lead (a 16 ring of stroke 2 in the item's tone when it is
/// late, else secondary; the accent's filled circle with the tick when done, [S-780]), the title as the name (BodyStrong
/// when emphasised and open; struck through and tertiary when done, [S-186]), the due as its value in the item's tone (a
/// reading in its place, [S-7072]), the subtitle on its second line where the plan gives two. Its rows stand in the
/// list's check layout (<see cref="IKitLook.Checks"/>): column by column, the add row under them, renamed and dragged in
/// place ([S-7038]). The check is the one control that toggles the item ([S-597]); the list hears its click.
/// <para>
/// [S-1426] A list whose rows carry a part at their end (a habit's history dots) hands the look its <see cref="Trail"/>:
/// the part stands in the meter's place, between the name and the value.
/// </para>
/// </summary>
public sealed class KitCheckLook : IKitLook
{
    public bool Checks => true;

    /// <summary>A part at the row's end, in the meter's place, for an item, or null for none.</summary>
    public Func<KitItem, KitTrail?>? Trail { get; init; }

    /// <summary>
    /// [S-15771] The value's column in every row (WR-11): a list whose rows carry a trail and a value some rows lack keeps
    /// the value's room in all of them, so the trail stands in one column. <see cref="double.NaN"/>: the value's own width.
    /// </summary>
    public double ValueWidth { get; set; } = double.NaN;

    public RowBlock Draw(KitItem item, KitDraw draw)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(draw);

        var plan = draw.Plan;
        var done = item.Lead.IsDone;
        var marker = Marker(done, item.Lead.Tone);
        draw.Check(marker);

        var block = new RowBlock(RowKind.List) { Lines = plan.Lines, Strong = item.Emphasis && !done, Height = plan.RowHeight };
        block.Show(draw.Title(item), plan.When ? item.Value ?? DueText(item, plan) : null, plan.Subtitle ? item.Subtitle : null);
        block.Lead(marker);

        if (Trail?.Invoke(item) is { } trail)
        {
            block.Meter(trail.Part, trail.Width);
        }

        // [S-15771] The value's column one width in every row while the plan shows values, so the trail stands in one column.
        block.ValueColumn(plan.When ? ValueWidth : double.NaN);

        // [S-186] Done is struck through, not merely dimmed: the one mark that says "done" without a legend and without
        // relying on colour. Late says so in words as well as in its tone, and only for open work.
        if (done)
        {
            block.NameText.TextDecorations = Windows.UI.Text.TextDecorations.Strikethrough;
            block.NameText.Foreground = Family.Brush(ColourRole.TextTertiary);
        }
        else
        {
            block.ValueText.Foreground = Family.Brush(KitDraw.Tone(item.Tone, ColourRole.TextSecondary));
        }

        return block;
    }

    public void Toggled(FrameworkElement check, KitItem item, bool done)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (check is Border marker)
        {
            Paint(marker, done, item.Lead.Tone);
        }
    }

    private static string? DueText(KitItem item, KitPlan plan)
    {
        if (item.Due is not { } due)
        {
            return null;
        }

        if (plan.UrgentOnly)
        {
            return due.Urgent ? due.Short : null;
        }

        return plan.LongWhen ? due.Long : due.Short;
    }

    /// <summary>
    /// [S-780] The check: drawn rather than taken from the icon font (there is no hollow circle in it), sized from the
    /// row, never from the tile.
    /// </summary>
    private static Border Marker(bool done, KitTone tone)
    {
        var marker = new Border
        {
            Width = FamilyShape.Icon,
            Height = FamilyShape.Icon,
            CornerRadius = Family.Round(FamilyShape.Icon, FamilyShape.Icon),
            VerticalAlignment = VerticalAlignment.Center,
        };

        Paint(marker, done, tone);
        return marker;
    }

    private static void Paint(Border marker, bool done, KitTone tone)
    {
        marker.BorderBrush = Family.Brush(KitDraw.Tone(tone, ColourRole.TextSecondary));
        marker.BorderThickness = new Thickness(done ? 0 : FamilyShape.Stroke);
        marker.Background = Family.Brush(done ? ColourRole.Accent : ColourRole.Clear);
        marker.Child = done ? Family.Glyph(Glyphs.Done, FamilyShape.Icon, ColourRole.TextOnAccent) : null;
    }
}
