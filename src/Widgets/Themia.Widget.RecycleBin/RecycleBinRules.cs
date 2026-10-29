using Themia.Core.Localization;
using Themia.Sdk.Kit;
using Themia.Widget.RecycleBin;

namespace Themia.Core.Widgets;

/// <summary>[S-4862] What the Recycle Bin widget shows at one size.</summary>
/// <param name="Step">The size's Readout step.</param>
/// <param name="Count">The item count as the size's line under.</param>
/// <param name="Button">Empty, the one main action in the Controls row at the bottom inset.</param>
public readonly record struct RecycleBinPlan(TypeStep Step, bool Count, bool Button);

/// <summary>
/// [S-4862] The Recycle Bin on the family's blocks ([S-6702], [S-6716]): the size is a Readout at the
/// view's top, start-aligned, on the step its widest case ("1023 MB") takes; the count is its line
/// under; Empty is the one main action in the Controls row at the bottom inset. The Controls row is
/// kept where it fits under the floor step and its line under, and the size takes the largest step
/// the rest of the box holds; where Empty does not fit it stays in the card menu, with Open Recycle Bin.
/// </summary>
public static class RecycleBinLayout
{
    /// <summary>The size's widest case, with its line under.</summary>
    public static readonly ReadoutText Worst = new("1023", " MB", LineUnder: true);

    public static RecycleBinPlan Plan(WidgetArea area)
    {
        if (!area.IsMeasured)
        {
            return new RecycleBinPlan(FamilyType.Big28, false, false);
        }

        var width = Math.Max(0, area.Width - (2 * FamilySpace.Inset));
        var height = Math.Max(0, area.Height - FamilySpace.Inset);
        var controls = FamilySpace.S2 + FamilyShape.Button;
        var button = ReadoutRules.Height(FamilyType.Big28, Worst) + controls <= height + 0.01;
        var box = button ? height - controls : height;
        var step = ReadoutRules.Step(area, width, box, Worst);
        return new RecycleBinPlan(step, ReadoutRules.Height(step, Worst) <= box + 0.01, button);
    }

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
