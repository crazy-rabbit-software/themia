using Microsoft.UI.Xaml;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Sdk.Kit;

namespace Themia.Sdk.WinUI.Kit;

/// <summary>
/// [S-7036] The schedule look: a Row block, its calendar's colour as a 2 px bar in the lead ([S-566], a data colour,
/// [S-6705]) or its lead glyph, the title as the name, the start as the value ("All day", "Now" Strong), the span, where
/// and who (or the item's note) on the second line where the plan gives two. [S-7035] Its day headings: one row of the
/// list's rows, BodyStrong secondary, primary for the day the widget has chosen; "Today" and "Tomorrow" with their date
/// where the line holds it whole ([S-2716]).
/// </summary>
public sealed class KitScheduleLook : IKitLook
{
    public RowBlock Draw(KitItem item, KitDraw draw)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(draw);

        var schedule = item.Schedule ?? new KitSchedule(null);
        var block = new RowBlock(RowKind.List) { Lines = draw.Plan.Lines, Strong = item.Emphasis, Height = draw.Plan.RowHeight };

        var second = schedule.Note ?? string.Join(" · ", new[] { schedule.Span, schedule.Where, schedule.Who }.Where(s => !string.IsNullOrWhiteSpace(s)));
        block.Show(draw.Title(item), schedule.Start, second is { Length: > 0 } ? second : null);
        if (schedule.Strong)
        {
            Family.Step(block.ValueText, FamilyType.BodyStrong);
        }

        block.ValueText.Foreground = Family.Brush(KitDraw.Tone(item.Tone, ColourRole.TextSecondary));

        // [S-566] The calendar's colour as a 2 px bar at the lead's start; with none, the item's glyph.
        if (schedule.Mark is { } mark)
        {
            block.Lead(new Microsoft.UI.Xaml.Controls.Border
            {
                Width = FamilyShape.Stroke,
                Height = FamilyShape.Icon,
                CornerRadius = Family.Round(FamilyShape.Stroke, FamilyShape.Icon),
                Background = Family.Data(mark),
                HorizontalAlignment = HorizontalAlignment.Left,
            });
        }
        else if (draw.Lead(item) is { } lead)
        {
            block.Lead(lead);
        }

        return block;
    }

    public FrameworkElement Heading(KitGroup group, KitDraw draw)
    {
        ArgumentNullException.ThrowIfNull(draw);
        var today = DateOnly.FromDateTime(draw.Now.DateTime);
        var dated = KitDays.Dated(Strings.Current, draw.Plan.ColumnWidth - (2 * FamilySpace.Inset));
        var text = group.Heading ?? (group.Day is { } day ? KitDays.Heading(Strings.Current, day, today, dated) : string.Empty);
        return SectionHeading.Make(text, draw.Plan.HeadingHeight, group.Selected);
    }
}
