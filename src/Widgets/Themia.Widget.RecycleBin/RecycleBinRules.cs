using Themia.Core.Localization;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Widget.RecycleBin;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-4862] [S-11201] The Recycle Bin on the family's blocks ([S-6702], [S-6716]): the size as the
/// family's 28 Readout at the view's top, start-aligned, the count its line under, and Empty, the one
/// main action, in the Controls row at the bottom inset. One fixed size: all three are always there.
/// </summary>
public static class RecycleBinLayout
{
    /// <summary>The size's widest case, with its line under.</summary>
    public static readonly ReadoutText Worst = new("1023", " MB", LineUnder: true);

    /// <summary>[S-11201] The one width: the header's name and the widest size reading, with the insets.</summary>
    public const double CardWidth = 180;

    /// <summary>[S-11201] The one size: the header, the reading and its line under, the Controls row, the bottom inset.</summary>
    public static SizeDip Size { get; } = new(
        CardWidth,
        FamilySpace.ContentTop + ReadoutRules.Height(FamilyType.Big28, Worst) + FamilySpace.S2 + FamilyShape.Button + FamilySpace.Inset);

    /// <summary>[S-4861] "12 items" / "Empty", in <paramref name="locale"/>; null before the first answer.</summary>
    public static string? CountText(Locale locale, RecycleBinState? state)
        => state switch
        {
            null => null,
            { IsEmpty: true } => RecycleBinStr.RecycleBin_IsEmpty.Text(locale),
            { } bin => RecycleBinStr.RecycleBin_Items.Plural(locale, bin.Items),
        };

    /// <summary>[S-4861] The size line: the bytes, "0 bytes" when empty, "–" before the first answer.</summary>
    public static string SizeText(Locale locale, RecycleBinState? state)
        => state is { } bin ? Formatting.Bytes(locale, bin.Bytes) : "–";
}
