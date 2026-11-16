using Microsoft.UI.Xaml;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.App.Widgets;

/// <summary>
/// [S-12410] A view whose parts are one fixed block (a reading, its lines, its controls) that it lays out
/// top to bottom for its natural box: in a larger box (the relaxed range, [S-12411]) the block keeps that
/// height and stands centred in the room under the header, so the spare goes round it and the controls
/// never drift to the foot away from what they act on. At its natural box nothing moves. For a view that
/// does not measure its box ([S-663]): a size-aware view centres an inner part instead, since the card
/// measures the view's own box as its area.
/// </summary>
public static class NaturalBlock
{
    /// <summary>Hold <paramref name="view"/> to the content height its kind's natural box gives <paramref name="config"/>, centred.</summary>
    public static void Hold(FrameworkElement view, WidgetConfig config)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(config);
        var kind = ModuleCatalog.Of(config.Kind);
        var top = kind.Traits.HasFlag(WidgetTraits.StartsHeaderless) ? FamilySpace.Inset : FamilySpace.ContentTop;
        view.Height = Math.Max(0, kind.SizingOf(config).Natural.Height - top);
        view.VerticalAlignment = VerticalAlignment.Center;
    }
}
