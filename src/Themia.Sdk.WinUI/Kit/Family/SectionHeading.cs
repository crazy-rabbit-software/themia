using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.Sdk.Kit;

namespace Themia.Sdk.WinUI.Kit;

/// <summary>
/// [S-12812] A section's heading inside a card (a day of the agenda, the schedule or the week planner, a list's done
/// group): one row of 28 stretched as its list's rows are, BodyStrong in the secondary role (primary for the current
/// one: today, the chosen day), the row's side inset. One part, so every list heads its sections alike.
/// </summary>
public static class SectionHeading
{
    /// <summary>A heading saying <paramref name="text"/>, <paramref name="height"/> tall (a row's pitch), <paramref name="current"/> in the primary role.</summary>
    public static Grid Make(string text, double height, bool current = false)
    {
        var title = Family.Text(FamilyType.BodyStrong, current ? ColourRole.TextPrimary : ColourRole.TextSecondary, text);
        title.VerticalAlignment = VerticalAlignment.Center;
        return new Grid { Children = { title }, Height = height, Padding = Family.SideInset };
    }
}
