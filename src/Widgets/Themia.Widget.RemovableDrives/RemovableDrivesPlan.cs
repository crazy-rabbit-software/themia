using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Widget.RemovableDrives;

/// <summary>[S-4662] How the devices' rows look at one size.</summary>
/// <param name="Lines">The lines a row shows: the name; the space line; the kind and file system ([S-6712]).</param>
/// <param name="Lead">The drive's picture before the name: 32, or none on a tile under <see cref="RemovableDrivesPlan.LeadNeeds"/>.</param>
/// <param name="Meter">The used share's inline bar between the name and the share, or 0 for none.</param>
public readonly record struct RemovableDrivesLook(int Lines, double Lead, double Meter);

/// <summary>
/// [S-4662] The devices' rows on the family's Row block ([S-6711]): the name is what is read
/// (primary), the used share its value (secondary); the tier gives the rows their lines (the space
/// line second, the kind third); the picture is a 32 lead from a view of 180, as every list's
/// avatar or file icon ([S-6704]); the used share's bar stands inline from a content of 240, a third
/// of it at most 140, as the forecast's range bar does ([S-6717]).
/// </summary>
public static class RemovableDrivesPlan
{
    /// <summary>The view's width from which the rows lead with the drive's picture.</summary>
    public const double LeadNeeds = 180;

    /// <summary>The content's width from which the bar stands in the rows.</summary>
    public const double MeterNeeds = 240;

    /// <summary>The bar's share of the content, and its most.</summary>
    public const double MeterShare = 0.32;

    public const double MeterMost = 140;

    public static RemovableDrivesLook Of(WidgetArea area)
    {
        var content = Math.Max(0, area.Width - (2 * FamilySpace.Inset));
        var meter = content >= MeterNeeds ? Math.Min(MeterMost, Math.Floor(content * MeterShare)) : 0;
        return new RemovableDrivesLook(RowRules.Lines(area, 3), area.Width >= LeadNeeds ? FamilyShape.IconLarge : 0, meter);
    }

    /// <summary>The list's height: the whole rows of <paramref name="count"/> that fit the view less its bottom inset, at least one.</summary>
    public static double ListHeight(WidgetArea area, int count, int lines)
    {
        var room = Math.Max(0, area.Height - FamilySpace.Inset);
        var shown = Math.Max(1, Math.Min(count, RowRules.Fit(room, lines)));
        return count == 0 ? 0 : shown * FamilyShape.RowHeight(lines);
    }
}
