using Themia.Sdk.Kit;

namespace Themia.Widget.RemovableDrives;

/// <summary>[S-4662] How the devices' rows look at one size.</summary>
/// <param name="Lines">The lines a row shows: the name, the space line under it ([S-6712]).</param>
/// <param name="Lead">The drive's picture before the name: 32.</param>
/// <param name="Meter">The used share's inline bar between the name and the share.</param>
public readonly record struct RemovableDrivesLook(int Lines, double Lead, double Meter);

/// <summary>
/// [S-4662] [S-11027] The devices' rows on the family's Row block ([S-6711]): the name is what is read
/// (primary), the used share its value (secondary), the space line under the name; the picture a 32
/// lead, as every list's avatar or file icon ([S-6704]); the used share's bar inline, a third of the
/// content at most 140, as the forecast's range bar does ([S-6717]). The same parts at every width in
/// the list's range; its height is its rows ([S-2440]).
/// </summary>
public static class RemovableDrivesPlan
{
    /// <summary>Two lines a row: the name and the space line.</summary>
    public const int Lines = 2;

    /// <summary>A new list's rows.</summary>
    public const int DefaultRows = 4;

    /// <summary>The bar's share of the content, and its most.</summary>
    public const double MeterShare = 0.32;

    public const double MeterMost = 140;

    /// <summary>[S-11027] The rows' look in a view <paramref name="width"/> wide.</summary>
    public static RemovableDrivesLook Of(double width)
    {
        var content = Math.Max(0, width - (2 * FamilySpace.Inset));
        return new RemovableDrivesLook(Lines, FamilyShape.IconLarge, Math.Min(MeterMost, Math.Floor(content * MeterShare)));
    }

    /// <summary>
    /// [S-12807] The config's rows in a view <paramref name="height"/> tall: two lines' 40 each, their text
    /// from the view's top to the foot's inset, at most half again (<see cref="Themia.Core.Widgets.ItemStretch.Rows"/>);
    /// the list is <see cref="Themia.Core.Widgets.RowReach.Length"/> of the config's rows.
    /// </summary>
    public static Themia.Core.Widgets.RowReach Reach(double height, int rows, double scale = 1)
        => Themia.Core.Widgets.ItemStretch.Rows(height, KitSizing.ClampRows(rows), Lines, scale: scale);
}
