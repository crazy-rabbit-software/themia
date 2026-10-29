using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Widget.Drive;

/// <summary>[S-4672] The Bar style at one size: where the figure, its line under, the bar and the facts go.</summary>
/// <param name="Side">The figure in a column of two fifths and the facts beside it (an expanded width), else the facts under the bar.</param>
/// <param name="Column">On the side: the figure's column, from the card's edge to where the facts begin (the inset included); else 0.</param>
/// <param name="Width">The figure's and the bar's width.</param>
/// <param name="Step">The used share's Readout step.</param>
/// <param name="Under">Whether the space line stands under the figure.</param>
/// <param name="RowsTop">Where the fact rows start in the view: 8 under the bar, or at its top on the side.</param>
/// <param name="Facts">How many fact rows are shown (the first ones).</param>
public readonly record struct DriveBarPlan(bool Side, double Column, double Width, TypeStep Step, bool Under, double RowsTop, int Facts);

/// <summary>[S-6274] The Ring style at one size.</summary>
/// <param name="Side">The ring in a column of two fifths and the facts beside it, else the ring alone with its line.</param>
/// <param name="Column">On the side: the ring's column (the inset included); else 0.</param>
/// <param name="Width">The ring's column's width (the line under it is cut to it).</param>
/// <param name="Ring">The ring's diameter; 0 when it would be under <see cref="DriveFace.RingFloor"/> (the share stands alone).</param>
/// <param name="Line">Whether the space line stands 8 under the ring.</param>
/// <param name="Step">The share's step: in the ring's hole, or alone.</param>
/// <param name="Facts">How many fact rows are shown beside the ring.</param>
public readonly record struct DriveRingPlan(bool Side, double Column, double Width, double Ring, bool Line, TypeStep Step, int Facts);

/// <summary>[S-4671] What the card shows in place of its figures: a problem, or why there are none.</summary>
public readonly record struct DriveStatus(StatusKind Kind, string Sentence);

/// <summary>
/// [S-4671] [S-4672] [S-6274] The drive card on the family's blocks ([S-6702], [S-6711], [S-6717]):
/// the used share is a Readout at the view's top, start-aligned, on the step its widest case
/// ("100%") takes, with the space line ("419 GB free of 931 GB") as its line under and the bar 8 under
/// it; the facts (Used, Free, Capacity, File system) are fact rows. On an expanded width the figure
/// stands in a column of two fifths and the facts beside it from the top, as the detail widgets do
/// (<see cref="DetailFace"/>); otherwise they stand 8 under the bar, whole rows only, room left empty
/// ([S-666]). The Ring style puts the share in a ring's hole instead, the ring as large as its column
/// allows with the space line 8 under it, the facts beside it on an expanded width. A drive without
/// figures shows one Status row instead ([S-6714]).
/// </summary>
public static class DriveFace
{
    /// <summary>The facts a drive has: Used, Free, Capacity, File system.</summary>
    public const int FactCount = 4;

    /// <summary>The least ring worth drawing ([S-6266]'s floor): under it the share stands alone.</summary>
    public const double RingFloor = FamilyShape.IconHuge;

    /// <summary>The used share's widest case, with its line under.</summary>
    public static readonly ReadoutText Worst = new("100", "%", LineUnder: true);

    /// <summary>The bar under the figure: 8 apart, the track's thickness.</summary>
    public static double BarRoom => FamilySpace.S2 + FamilyShape.Track;

    /// <summary>[S-4672] The Bar style for a view of <paramref name="area"/>.</summary>
    public static DriveBarPlan Bar(WidgetArea area)
    {
        var (width, height, side, column, room) = Frame(area);
        var step = ReadoutRules.Step(area, room, height - BarRoom, Worst);
        var under = ReadoutRules.Height(step, Worst) + BarRoom <= height + 0.01;
        var figure = step.LineHeight + (under ? FamilyType.Under(step).LineHeight : 0) + BarRoom;
        var top = side ? 0 : figure + FamilySpace.S2;
        var facts = Math.Min(FactCount, RowRules.Fit(height - top, 1));
        return new DriveBarPlan(side, side ? column : 0, room, step, under, top, facts);
    }

    /// <summary>[S-6274] The Ring style for a view of <paramref name="area"/>.</summary>
    public static DriveRingPlan Ring(WidgetArea area)
    {
        var (width, height, side, column, room) = Frame(area);
        var withLine = Math.Floor(Math.Min(room, height - FamilySpace.S2 - FamilyType.Body.LineHeight));
        var line = withLine >= RingFloor;
        var ring = line ? withLine : Math.Floor(Math.Min(room, height));
        if (ring < RingFloor)
        {
            ring = 0;
            line = false;
        }

        var bare = Worst with { LineUnder = false };
        var hole = ring - (2 * FamilyShape.Stroke);
        var step = ring > 0 ? ReadoutRules.Step(area, hole * 0.75, hole / 2, bare) : ReadoutRules.Step(area, room, height, bare);
        var facts = side ? Math.Min(FactCount, RowRules.Fit(height, 1)) : 0;
        return new DriveRingPlan(side, side ? column : 0, room, ring, line, step, facts);
    }

    /// <summary>
    /// [S-4671] Why a drive has no figures, as one Status row: a share Windows cannot reach is a
    /// problem ("Disconnected network drive"); one still answering, a reader with nothing in it and a
    /// drive that is not there say so plainly. Null for a drive with its figures.
    /// </summary>
    public static DriveStatus? Status(Locale locale, DriveEntry drive)
    {
        ArgumentNullException.ThrowIfNull(drive);
        if (drive.UsedFraction is not null)
        {
            return null;
        }

        return drive.State == DriveState.Disconnected
            ? new DriveStatus(StatusKind.Problem, DriveRules.Description(locale, drive))
            : new DriveStatus(StatusKind.Empty, DriveRules.SpaceLine(locale, drive));
    }

    private static (double Width, double Height, bool Side, double Column, double Room) Frame(WidgetArea area)
    {
        var width = Math.Max(0, area.Width - (2 * FamilySpace.Inset));
        var height = Math.Max(0, area.Height - FamilySpace.Inset);
        var side = area.WidthClass >= SizeClass.Expanded;
        var column = side ? Math.Floor(area.Width * DetailFace.HeroShare) : area.Width;
        return (width, height, side, column, side ? column - FamilySpace.Inset : width);
    }
}
