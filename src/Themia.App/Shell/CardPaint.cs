using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Shell;

/// <summary>
/// [S-823] [S-824] The brushes a card's background is painted with, in one place: a lone
/// <see cref="WidgetCard"/> and a group's shared background ([S-2103], <see cref="CardGroupLayer"/>)
/// paint from the same answers, so a group can never drift from the card it replaces.
/// </summary>
internal static class CardPaint
{
    /// <summary>The opaque base: the user's own colour, or the Fluent surface the card tint expects ([S-27a]).</summary>
    public static Brush Base(DesignState design)
        => (Brush?)DesignApplier.UserBrush(design.WidgetBackgroundColor)
            ?? FluentLayout.Themed("SolidBackgroundFillColorBaseBrush", Colors.Black);

    /// <summary>[S-214] The Fluent lift over the base; none over a colour of the user's own, which is meant exactly.</summary>
    public static Brush? Tint(DesignState design)
        => DesignApplier.UserBrush(design.WidgetBackgroundColor) is null
            ? FluentLayout.Themed("CardBackgroundFillColorDefaultBrush", Colors.Transparent)
            : null;

    /// <summary>[S-824] The border's colour: the user's own, or the theme's card stroke.</summary>
    public static Brush Stroke(DesignState design)
        => (Brush?)DesignApplier.UserBrush(design.BorderColor)
            ?? FluentLayout.Themed("CardStrokeColorDefaultBrush", Colors.Gray);

    /// <summary>[S-824] A border look's dash pattern as a shape takes it (multiples of the stroke width).</summary>
    public static DoubleCollection Dashes(BorderLook border)
    {
        var dashes = new DoubleCollection();
        foreach (var dash in border.Dashes)
        {
            dashes.Add(dash);
        }

        return dashes;
    }
}
