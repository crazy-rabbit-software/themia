using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.Sdk.Kit;

namespace Themia.Sdk.WinUI.Kit;

/// <summary>
/// [S-12812] A section's heading inside a card (a day of the agenda, the schedule or the week planner, a list's done
/// group): one row of its list's rows' size ([S-14810]), BodyStrong in the secondary role (primary for the current
/// one: today, the chosen day), the row's side inset. One part, so every list heads its sections alike.
/// [S-12834] The heading is one of the rows that tile its list (owner, WR-6: "all items of a widget are the same size"):
/// as tall as its list's rows (<c>KitPlan.HeadingHeight</c>, whole grid cells), an item of the list's (<c>ItemMark</c>),
/// its title's box filling it with the words centred, so the rows under it touch it (WR-1, WR-2, WR-5: no space over a
/// section's first row, none between a heading and its rows).
/// </summary>
public static class SectionHeading
{
    /// <summary>A heading saying <paramref name="text"/>, <paramref name="height"/> tall (a row's pitch), <paramref name="current"/> in the primary role.</summary>
    public static Grid Make(string text, double height, bool current = false)
    {
        var title = Family.Text(FamilyType.BodyStrong, current ? ColourRole.TextPrimary : ColourRole.TextSecondary, text);
        Fill(title, height);
        var heading = new Grid { Children = { title }, Height = height, Padding = Family.SideInset };
        Themia.App.Widgets.ItemMark.Mark(heading);
        return heading;
    }

    /// <summary>
    /// [S-12834] A line's words in a band <paramref name="height"/> tall: the box fills the band (a text block's box is its
    /// lines and its padding, never stretched), the words centred in it, or on its top where <paramref name="top"/>.
    /// </summary>
    public static void Fill(TextBlock words, double height, bool top = false)
    {
        ArgumentNullException.ThrowIfNull(words);
        var line = words.LineHeight > 0 ? words.LineHeight : FamilyType.LineOf(words.FontSize);
        var spare = double.IsFinite(height) ? Math.Max(0, height - line) : 0;
        var over = top ? 0 : Math.Floor(spare / 2);
        var padding = new Thickness(0, over, 0, spare - over);
        words.VerticalAlignment = VerticalAlignment.Top;
        if (words.Padding != padding)
        {
            words.Padding = padding;
        }
    }
}
