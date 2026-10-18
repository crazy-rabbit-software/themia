using Microsoft.UI.Xaml.Controls;

namespace Themia.App.Widgets;

/// <summary>
/// [S-655] A header's trailing element that is an informing label with extras beside it (the
/// exchange rates' day before the kit's header extras). The card's header gives <see cref="Label"/>
/// the same room rule as a bare trailing label, so on a small tile the label gives way to the title;
/// the extras are left alone. A plain panel stays untouched: only a label named here is the header's.
/// </summary>
public sealed partial class HeaderTrail : StackPanel
{
    public HeaderTrail(TextBlock label, params Microsoft.UI.Xaml.UIElement[] extras)
    {
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(extras);

        Label = label;
        Orientation = Orientation.Horizontal;
        Spacing = Fluent.Space2;
        Children.Add(label);
        foreach (var extra in extras)
        {
            Children.Add(extra);
        }
    }

    /// <summary>The informing label, the first child.</summary>
    public TextBlock Label { get; }
}
