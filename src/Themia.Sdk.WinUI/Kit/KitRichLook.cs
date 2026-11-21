using Microsoft.UI.Xaml;
using Themia.App.Widgets;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Sdk.WinUI.Kit;

/// <summary>
/// [S-7031] [S-7032] The rich, two-line and one-line rows: the item's title as the name (BodyStrong for the emphasised
/// one: unread, today; a read row of a list that tells unread by its names in the secondary role, [S-2270]), the time as
/// the value (secondary, or its tone's Caution or Critical; never the accent; a reading in its place, [S-7072]), the
/// subtitle (and the badge after it) on the second line, the preview on the third, the lead a 16 glyph or the site's
/// icon; the lines the plan chose ([S-6712]).
/// <para>
/// [S-7060] A widget that moved onto the kit keeps what is its own through the look's parts, never a row of its own: its
/// lead (a file's icon, an app's, a colour's swatch, a drive's picture; <see cref="Lead"/> at <see cref="LeadSize"/>), a
/// part at the row's end in the meter's place (a drive's used bar, [S-6717]: <see cref="Trail"/>) and a last touch of
/// the row's roles (<see cref="Touch"/>). A row is rewritten in place for a new reading of the same item, its lead kept
/// while the item's lead is the same, so an icon already drawn never blinks.
/// </para>
/// </summary>
public sealed class KitRichLook : IKitLook
{
    /// <summary>The widget's own lead for an item, or null for the kit's (<see cref="KitDraw.Lead"/>).</summary>
    public Func<KitItem, FrameworkElement?>? Lead { get; init; }

    /// <summary>The lead's side: 16, or a picture's 32 ([S-6704]).</summary>
    public double LeadSize { get; init; } = FamilyShape.Icon;

    /// <summary>[S-11510] The lead grows to a picture's 32 on rows of two lines or more (an app's icon beside its figures).</summary>
    public bool LargeOnTwoLines { get; init; }

    /// <summary>A part at the row's end, in the meter's place, for an item, or null for none.</summary>
    public Func<KitItem, KitTrail?>? Trail { get; init; }

    /// <summary>The widget's last touch of a drawn row (a stand-in name in the secondary role).</summary>
    public Action<KitItem, RowBlock>? Touch { get; init; }

    /// <summary>[S-4402] A group heading's lead (an app's icon before its name), or null for a heading of text alone.</summary>
    public Func<KitGroup, FrameworkElement?>? HeadingLead { get; init; }

    public RowBlock Draw(KitItem item, KitDraw draw)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(draw);

        var plan = draw.Plan;
        var block = new RowBlock(RowKind.List, plan.Lead ? LeadOf(plan) : 0);
        Write(block, item, draw);

        if (plan.Lead && (Lead?.Invoke(item) ?? draw.Lead(item)) is { } lead)
        {
            block.Lead(lead);
        }

        return block;
    }

    /// <summary>The lead's side on rows of <paramref name="plan"/>'s lines.</summary>
    public double LeadOf(KitPlan plan) => LargeOnTwoLines && plan.Lines > 1 ? FamilyShape.IconLarge : LeadSize;

    public bool Update(RowBlock row, KitItem was, KitItem item, KitDraw draw)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(was);
        ArgumentNullException.ThrowIfNull(item);

        // Another item in this place, or the same one led otherwise: drawn again.
        if (was.Id != item.Id || was.Lead != item.Lead)
        {
            return false;
        }

        Write(row, item, draw);
        return true;
    }

    public FrameworkElement Heading(KitGroup group, KitDraw draw)
    {
        ArgumentNullException.ThrowIfNull(draw);
        var heading = SectionHeading.Make(group.Heading ?? string.Empty, draw.Plan.HeadingHeight, group.Selected);
        if (HeadingLead?.Invoke(group) is { } lead)
        {
            SectionHeading.Lead(heading, lead);
        }

        return heading;
    }

    /// <summary>The row's words, roles and end part for the item.</summary>
    private void Write(RowBlock block, KitItem item, KitDraw draw)
    {
        var plan = draw.Plan;
        block.Lines = plan.Lines;
        block.Strong = item.Emphasis;
        block.Quiet = draw.Spec.Unread && !item.Emphasis;
        block.Height = draw.Height;

        var second = plan.Subtitle ? SubtitleLine(item, plan) : plan.Preview ? item.Preview : null;
        var third = plan.Subtitle && plan.Preview ? item.Preview : null;

        // [S-7072] A reading stands in the time's place, and is not rewritten on the ticker.
        var when = plan.When ? item.Value ?? draw.When(item.When, draw.Spec.Time) : null;
        block.Show(draw.Title(item), when, second, third);
        block.ValueText.Foreground = Family.Brush(KitDraw.Tone(item.Tone, ColourRole.TextSecondary));

        if (plan.When && item.Value is null)
        {
            draw.Times(block.ValueText, item.When, draw.Spec.Time);
        }

        if (Trail is not null)
        {
            var trail = Trail(item);
            block.Meter(trail?.Part, trail?.Width ?? 0);
        }

        Touch?.Invoke(item, block);
    }

    /// <summary>The second line: the subtitle, the first badge after it where the plan has room.</summary>
    private static string? SubtitleLine(KitItem item, KitPlan plan)
    {
        var badge = plan.Badges ? KitDraw.Badge(item) : null;
        var subtitle = item.Subtitle is { Length: > 0 } s ? s : null;
        return (subtitle, badge) switch
        {
            ({ } a, { } b) => $"{a} · {b}",
            ({ } a, null) => a,
            (null, { } b) => b,
            _ => null,
        };
    }
}

/// <summary>[S-6717] A part at a row's end, in the meter's place, and its width.</summary>
public readonly record struct KitTrail(FrameworkElement Part, double Width);
