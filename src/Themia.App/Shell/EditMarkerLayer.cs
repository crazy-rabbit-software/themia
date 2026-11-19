using Microsoft.UI.Xaml.Controls;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell;

/// <summary>
/// [S-31b] The edit markers' ink of every card on a surface, above all the cards: every card's halos
/// on one canvas, every card's bars on the next, so where two widgets meet on an edge their marks are
/// one line and no neighbour's halo cuts through a bar. [S-1597] Over the ink, the lift plates: a selected
/// card's grips and move area, so a selected card under another takes every press on its handles and its
/// body first (<see cref="Themia.Core.Model.SelectionRules.PressTarget"/>). Every other card keeps its grips.
/// A card's plates follow it on their own; it takes them off as it detaches.
/// </summary>
internal sealed class EditMarkerLayer
{
    /// <summary>Every card's halos, drawn first.</summary>
    public Canvas Halos { get; } = UiLanguage.KeepLeftToRight(new Canvas { IsHitTestVisible = false });

    /// <summary>Every card's bars, over every halo.</summary>
    public Canvas Inks { get; } = UiLanguage.KeepLeftToRight(new Canvas { IsHitTestVisible = false });

    /// <summary>[S-1597] The selected cards' grips and move areas, over every card and its ink; empty room takes no press.</summary>
    public Canvas Lifts { get; } = UiLanguage.KeepLeftToRight(new Canvas());

    /// <summary>
    /// [S-13975] What views draw past their cards (an icon's name in icon only, its rename box), over every card and under
    /// the edit marks; empty room takes no press.
    /// </summary>
    public Canvas Overhangs { get; } = UiLanguage.KeepLeftToRight(new Canvas());

    /// <summary>Draw <paramref name="card"/>'s markers here, as it joins the surface.</summary>
    public void Add(WidgetCard card)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (card.MarkerHalos.Parent is null)
        {
            Halos.Children.Add(card.MarkerHalos);
            Inks.Children.Add(card.MarkerInks);
            Lifts.Children.Add(card.MarkerLift);
        }

        if (card.OverhangPlate.Parent is null)
        {
            Overhangs.Children.Add(card.OverhangPlate);
        }

        // [S-1597] A card selected before it joined the layer lifts its grips now.
        card.ApplySelection();
    }
}
