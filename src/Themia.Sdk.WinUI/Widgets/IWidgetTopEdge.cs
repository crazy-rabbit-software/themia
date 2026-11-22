namespace Themia.App.Widgets;

/// <summary>
/// [S-5700] A view that owns its card's top edge. A card insets the view's top by the family's inset
/// (joined or alone, [S-12483]), so its content starts where its sides and foot already
/// stand (each view keeps those itself). A view says it owns the edge when it draws to it on purpose (a picture, a
/// stream, a spectrum, a cover filling the card) or pads all four sides itself (the clock, the quick
/// toggles, the power buttons), and the card then hands it its whole box as before.
/// <para>
/// Optional: a view without it does not own the edge. Read whenever the card lays its view out (a
/// config change, a resize, a join), so a style can switch it.
/// </para>
/// </summary>
public interface IWidgetTopEdge
{
    bool OwnsTopEdge { get; }

    /// <summary>
    /// [S-15814] A plot that owns the edge with nothing written at its top (the sun's arc and day bar): the card's compact
    /// Set up line stands over it, inside the plot, never a band that pushes it down (owner, 2026-11-19, of graphs: "Plot edge
    /// to edge": "the labels stand inside it, over the plot"). A view with words at its top keeps the band (false).
    /// </summary>
    bool SetupOver => false;
}
