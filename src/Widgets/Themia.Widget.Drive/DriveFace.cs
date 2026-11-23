using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Items;
using Themia.Sdk;
using Themia.Sdk.Devices;
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

    /// <summary>
    /// [S-16645] The Bar style's one height, the device item's (<see cref="ItemLayout.ListNatural"/>): the inset, the share and
    /// its line, 8, the bar, the inset.
    /// </summary>
    public static double BarHeight => ItemLayout.ListNatural(ItemShape.Device);

    /// <summary>Under the ring: 8, then the space line (Body).</summary>
    public static double RingLine => FamilySpace.S2 + FamilyType.Body.LineHeight;

    /// <summary>
    /// [S-15749] The reading's sizing in its style, scaled down as one block to where its smallest text (the space line, or
    /// the reason line in its place: Caption under the bar, Body under the ring) is still read (WR-14: "sized down to where
    /// its smallest text is still read (9 DIP)"): its content box at that scale in the card's inset, which does not scale.
    /// No part gives way (WR-7: "A reading and its caption stay together"; WR-10: the line saying why stays).
    /// </summary>
    public static WidgetSizing Scaled(DriveStyle style)
    {
        if (style == DriveStyle.Bar)
        {
            // [S-16637] The device family's one face: its sizing, never narrower than its widest line.
            return ItemLayout.Face(ItemShape.Device, WidestLine(Strings.Current) - FamilyType.Big28.LineHeight - FamilySpace.GlyphGap, ItemLayout.ReadingWidth);
        }

        var sizing = Sizing(style);
        var k = ScaleFloor.Least(FamilyType.Body.FontSize);
        var natural = sizing.Natural;
        return sizing.ScalesDownTo(new SizeDip(
            ((natural.Width - (2 * FamilySpace.Inset)) * k) + (2 * FamilySpace.Inset),
            ((natural.Height - FamilySpace.ContentTop - FamilySpace.Inset) * k) + FamilySpace.ContentTop + FamilySpace.Inset));
    }

    /// <summary>
    /// [S-15759] The bar face's widest row at its own size: the drive's icon, the glyph gap and the widest line under the share
    /// (Caption: its space line's forms and every reason a drive has no figures), estimated by <see cref="HeroFit.Ems"/>.
    /// </summary>
    public static double WidestLine(Locale locale)
    {
        string[] reasons =
        [
            DriveRules.Description(locale, new DriveEntry("Z:\\", VolumeKind.Network, DriveState.Disconnected)),
            DriveRules.SpaceLine(locale, new DriveEntry("Y:\\", VolumeKind.Network, DriveState.Pending)),
            DriveRules.SpaceLine(locale, new DriveEntry("Q:\\", VolumeKind.Unknown, DriveState.Missing)),
            DriveRules.SpaceLine(locale, new DriveEntry("F:\\", VolumeKind.Optical, DriveState.NoMedia)),
        ];
        var line = SpaceTemplates(locale).Concat(reasons).Max(HeroFit.Ems) * FamilyType.Caption.FontSize;
        return FamilyType.Big28.LineHeight + FamilySpace.GlyphGap + line;
    }

    /// <summary>
    /// [S-15750] The space line's widest forms in <paramref name="locale"/> (WR-15: "A running reading never moves"): the
    /// line as it reads for every unit pair a drive's free and total space may take, its figures as the template's 8s
    /// (<see cref="FigureTemplate"/>). The line stands in the box of the widest, so the block never rescales or moves as the
    /// free space changes ("419 GB free of 931 GB", "92 GB free of 1.8 TB" and "31 GB free of 58 GB" had each drawn the icon
    /// and the share at another size and place).
    /// </summary>
    public static IReadOnlyList<string> SpaceTemplates(Locale locale)
    {
        const ulong Mb = 1024UL * 1024, Gb = Mb * 1024, Tb = Gb * 1024;
        ulong[] sizes = [888 * Mb, 888 * Gb, (88 * Tb) + (Tb * 8 / 10)];
        return [.. sizes.SelectMany(free => sizes.Where(total => total >= free).Select(total =>
            FigureTemplate.Of(DriveRules.SpaceLine(locale, new DriveEntry("C:\\", VolumeKind.Fixed, DriveState.Ready, string.Empty, "NTFS", total, free)))))
            .Distinct()];
    }

    /// <summary>[S-15749] The ring style's ring as it is planned, before the block is scaled to its card: the natural one.</summary>
    public static double PlannedRing => MeterSizing.RingDiameters(Worst).Natural;

    /// <summary>[S-4672] [S-6274] [S-11026] The reading's sizing in its style.</summary>
    public static WidgetSizing Sizing(DriveStyle style)
    {
        if (style == DriveStyle.Bar)
        {
            var h = BarHeight;
            return WidgetSizing.Range(new(160, h), new(220, h), new(360, h));
        }

        var (least, start) = MeterSizing.RingDiameters(Worst);
        double Height(double d) => FamilySpace.ContentTop + d + RingLine + FamilySpace.Inset;
        return WidgetSizing.Range(
            new(Math.Max(160, least + (2 * FamilySpace.Inset)), Height(least)),
            new(200, Height(start)),
            new(320, Height(MeterSizing.DialMax)));
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
}
