using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>[S-2150] A level tile at one size: whether its Readout is drawn, on which step, and whether its line under stands.</summary>
/// <param name="Readout">The level's Readout is drawn (the card holds its floor step over the Controls row).</param>
/// <param name="Step">The Readout's step: the data widgets' 28, or 40 once expanded both ways ([S-6702], [S-2220]).</param>
/// <param name="Under">The line under the value (the widget's own fact: the output device's name) stands.</param>
public readonly record struct LevelPlan(bool Readout, TypeStep Step, bool Under);

/// <summary>
/// [S-2150] The ladder of a level tile (the volume, the brightness): a meter whose Controls row (the
/// slider, with its mute button) stands at the card's bottom inset. The level is a Readout at the
/// view's top, start-aligned, on the step its widest case ("100%") takes in the room the Controls row
/// leaves over it, so it takes 28 or 40 exactly as a meter's reading does (the step is chosen on
/// the value alone, never lowered for the line under). The Controls row comes first: a card too short
/// for the floor step's line and the row together drops the Readout, never the controls (the widget
/// is a control first). The line under stands only where the step's line and the line under both fit
/// over the row; otherwise it gives way, never the value.
/// </summary>
public static class LevelFace
{
    /// <summary>The level's widest case: the Readout's step is chosen for it, so a moving level never changes the step.</summary>
    public static readonly ReadoutText Worst = new("100", "%");

    /// <summary>What the Controls row takes: its button's height and the 8 over it.</summary>
    public const double ControlsCost = FamilyShape.Button + FamilySpace.S2;

    /// <summary>[S-2150] The plan for a view of <paramref name="area"/>; <paramref name="hasUnder"/> when the widget has a line under to say.</summary>
    public static LevelPlan Plan(WidgetArea area, bool hasUnder)
    {
        if (!area.IsMeasured)
        {
            return new LevelPlan(false, FamilyType.Big28, false);
        }

        var width = Math.Max(0, area.Width - (2 * FamilySpace.Inset));
        var height = area.Height - FamilySpace.Inset - ControlsCost;
        if (height <= 0)
        {
            return new LevelPlan(false, FamilyType.Big28, false);
        }

        var step = ReadoutRules.Step(area, width, height, Worst);
        var readout = step.LineHeight <= height + Disclosure.Tolerance && ReadoutRules.Width(step, Worst) <= width + Disclosure.Tolerance;
        var under = readout && hasUnder && ReadoutRules.Height(step, Worst with { LineUnder = true }) <= height + Disclosure.Tolerance;
        return new LevelPlan(readout, step, under);
    }
}
