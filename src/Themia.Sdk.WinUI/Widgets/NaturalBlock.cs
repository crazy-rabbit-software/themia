using System.Runtime.CompilerServices;
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
/// measures the view's own box as its area.
/// <para>
/// [S-12483] In a group the block stands against a side a mate joins instead (<see cref="JoinedSides.Block"/>),
/// and where a mate joins its foot the view's own foot inset (inside the block's height, the family's
/// [S-11600]) goes past the card's edge, so its last line meets the mate under it. The card tells it
/// through <see cref="Join"/>.
/// </para>
/// </summary>
public static class NaturalBlock
{
    /// <summary>A held view's natural height and the sides the card last said are joined.</summary>
    private sealed class Held
    {
        public double Height;
        public JoinedSides Sides;
    }

    private static readonly ConditionalWeakTable<FrameworkElement, Held> Views = [];

    /// <summary>Hold <paramref name="view"/> to the content height its kind's natural box gives <paramref name="config"/>.</summary>
    public static void Hold(FrameworkElement view, WidgetConfig config)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(config);
        var held = Views.GetOrCreateValue(view);

        // The natural box counts the card's inset over the view ([S-12480]); the view's own foot is inside it.
        held.Height = Math.Max(0, ModuleCatalog.Of(config.Kind).SizingOf(config).Natural.Height - FamilySpace.Inset);
        Stand(view, held);
    }

    /// <summary>[S-12483] The card's word on the sides a mate joins: a held view stands against them; any other is left alone.</summary>
    public static void Join(FrameworkElement view, JoinedSides sides)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (Views.TryGetValue(view, out var held) && held.Sides != sides)
        {
            held.Sides = sides;
            Stand(view, held);
        }
    }

    /// <summary>[S-12483] [S-12800] The alignment that stands a part at <paramref name="edge"/> of its box.</summary>
    public static VerticalAlignment Alignment(BlockEdge edge) => edge switch
    {
        BlockEdge.Top => VerticalAlignment.Top,
        BlockEdge.Bottom => VerticalAlignment.Bottom,
        _ => VerticalAlignment.Center,
    };

    private static void Stand(FrameworkElement view, Held held)
    {
        view.Height = held.Height;
        view.VerticalAlignment = Alignment(held.Sides.Block);

        // The top is the card's (its margin's top); the foot past the edge where a mate joins it.
        view.Margin = view.Margin with { Bottom = held.Sides.Bottom ? -FamilySpace.Inset : 0 };
    }
}
