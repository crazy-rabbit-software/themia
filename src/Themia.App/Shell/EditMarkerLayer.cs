using Microsoft.UI.Xaml.Controls;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell;

/// <summary>
/// [S-31b] The edit markers' ink of every card on a surface, above all the cards: every card's halos
/// on one canvas, every card's bars on the next, so where two widgets meet on an edge their marks are
/// one line and no neighbour's halo cuts through a bar. Only ink: the grips that take the presses stay
/// on each card. A card's plates follow it on their own; it takes them off as it detaches.
/// </summary>
internal sealed class EditMarkerLayer
{
    /// <summary>Every card's halos, drawn first.</summary>
    public Canvas Halos { get; } = UiLanguage.KeepLeftToRight(new Canvas { IsHitTestVisible = false });

    /// <summary>Every card's bars, over every halo.</summary>
    public Canvas Inks { get; } = UiLanguage.KeepLeftToRight(new Canvas { IsHitTestVisible = false });

    /// <summary>Draw <paramref name="card"/>'s markers here, as it joins the surface.</summary>
    public void Add(WidgetCard card)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (card.MarkerHalos.Parent is null)
        {
            Halos.Children.Add(card.MarkerHalos);
            Inks.Children.Add(card.MarkerInks);
        }
    }
}
