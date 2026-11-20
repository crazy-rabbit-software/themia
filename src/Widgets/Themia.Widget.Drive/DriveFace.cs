using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Widget.Drive;

/// <summary>[S-4671] What the card shows in place of its figures: a problem, or why there are none.</summary>
public readonly record struct DriveStatus(StatusKind Kind, string Sentence);

/// <summary>
/// [S-4671] [S-4672] [S-6274] The drive reading on the family's blocks ([S-6702], [S-6717]): the used
/// share at 28 with the space line ("419 GB free of 931 GB", Caption) under it and the bar 8 under that
/// (Bar); or the share in a ring's hole with the space line 8 under the ring (Ring). The same parts at
/// every size: a wider card is a longer bar, a larger card a larger ring. A drive without figures shows
/// one Status row instead ([S-6714]). Its facts are the Drive facts atom's.
/// </summary>
public static class DriveFace
{
    /// <summary>The used share's widest case.</summary>
    public static readonly ReadoutText Worst = new("100", "%");

    /// <summary>The Bar style's one height: the inset, the share and its line, 8, the bar, the inset.</summary>
    public static double BarHeight => FamilySpace.ContentTop + FamilyType.Big28.LineHeight + FamilyType.Caption.LineHeight + FamilySpace.S2 + FamilyShape.Track + FamilySpace.Inset;

    /// <summary>Under the ring: 8, then the space line (Body).</summary>
    public static double RingLine => FamilySpace.S2 + FamilyType.Body.LineHeight;

    /// <summary>
    /// [S-12833] The bar style's parts (<see cref="FaceParts"/>): the share its main reading, at 9 in its least card, with
    /// the drive's icon before it and the bar under; its unit and space line give way on the way down. The ring is planned
    /// in its box, its share at 28, and keeps the rule it had (its space line's floor): null.
    /// </summary>
    public static FaceParts? Face(DriveStyle style)
        => style != DriveStyle.Bar ? null
            : FaceParts.Readout(FamilyType.Big28, Worst with { LineUnder = true }, 200 - (2 * FamilySpace.Inset))
                .Led(FacePiece.Box(FamilyType.Big28.LineHeight + FamilySpace.GlyphGap, FamilyType.Big28.LineHeight))
                .Then(new FaceRow([FacePiece.Box(0, FamilyShape.Track)], GapOver: FamilySpace.S2));

    /// <summary>[S-4672] [S-6274] [S-11026] The reading's sizing in its style.</summary>
    public static WidgetSizing Sizing(DriveStyle style)
    {
        if (style == DriveStyle.Bar)
        {
            var h = BarHeight;
            return WidgetSizing.Range(new(160, h), new(200, h), new(320, h));
        }

        var (least, start) = MeterSizing.RingDiameters(Worst);
        double Height(double d) => FamilySpace.ContentTop + d + RingLine + FamilySpace.Inset;
        return WidgetSizing.Range(
            new(Math.Max(160, least + (2 * FamilySpace.Inset)), Height(least)),
            new(200, Height(start)),
            new(320, Height(MeterSizing.DialMax)));
    }

    /// <summary>[S-6274] The ring in a content box: its width, and its height less the line under it.</summary>
    public static double Ring(double width, double height) => MeterSizing.RingIn(width, height - RingLine);

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
}
