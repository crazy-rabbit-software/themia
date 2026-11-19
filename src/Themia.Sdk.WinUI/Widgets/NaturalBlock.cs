using Microsoft.UI.Xaml;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.App.Widgets;

/// <summary>
/// [S-12410] A view whose parts are one fixed block (a reading, its lines, its controls) that it lays out
/// top to bottom for its natural box: in a larger box (the relaxed range, [S-12411]) the block keeps that
/// height and stands centred in the room the card gives it, so the spare goes round it and the controls
/// never drift to the foot away from what they act on. At its natural box nothing moves. For a view that
/// does not measure its box ([S-663]): a size-aware view centres an inner part instead, since the card
/// measures the view's own box as its area. [S-12483] The same whether a card joins it or not: a join is
/// the cards' chrome, never the content's place.
/// </summary>
public static class NaturalBlock
{
    /// <summary>Hold <paramref name="view"/> to the content height its kind's natural box gives <paramref name="config"/>.</summary>
    public static void Hold(FrameworkElement view, WidgetConfig config)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(config);

        // The natural box counts the card's inset over the view ([S-12480]); the view's own foot is inside it.
        view.Height = Math.Max(0, ModuleCatalog.Of(config.Kind).SizingOf(config).Natural.Height - FamilySpace.Inset);
        view.VerticalAlignment = VerticalAlignment.Center;
        view.Margin = view.Margin with { Bottom = 0 };
    }
}
