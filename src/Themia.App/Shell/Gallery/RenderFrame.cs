#if DEBUG
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-2460] [S-14310] Debug only: the selection's frame in a render, as the surface draws it. The cards it goes round are
/// Core's (<see cref="SelectionRules.Framed{T}"/>: two or more selected, on the locked desktop cards and never an icon), each
/// told so through its host (no marks of its own), and the frame is drawn round their boxes: in edit mode its hairline,
/// markers and grips, on the locked desktop its four corners alone. Fewer than two, no frame and every card its own marks.
/// </summary>
internal static class RenderFrame
{
    /// <summary>
    /// Frame the selected cards of <paramref name="drawn"/> (each with its box in the render's DIPs) on
    /// <paramref name="markers"/> at <paramref name="scale"/>; <paramref name="hosts"/> are every host the cards are on.
    /// </summary>
    public static void Round(
        EditMarkerLayer markers,
        IReadOnlyList<(WidgetCard Card, DipRect Box)> drawn,
        IReadOnlySet<string> selected,
        bool editing,
        double scale,
        IReadOnlyList<PreviewCardHost> hosts)
    {
        var members = SelectionRules.Framed(editing, drawn.Select(d => (d, selected.Contains(d.Card.Model.Id), d.Card.View is IDesktopIconView)));
        var ids = members.Select(m => m.Card.Model.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var host in hosts)
        {
            host.Framed = ids;
        }

        foreach (var (card, _) in drawn)
        {
            card.ApplySelection();
        }

        if (members.Count == 0)
        {
            markers.Frame.Hide();
            return;
        }

        var box = DipRect.FromEdges(
            members.Min(m => m.Box.X),
            members.Min(m => m.Box.Y),
            members.Max(m => m.Box.X + m.Box.Width),
            members.Max(m => m.Box.Y + m.Box.Height));
        var (width, height) = editing ? BlockResize.Free([.. members.Select(m => m.Card.Limits)], members.Count) : (false, false);
        markers.Frame.Show(box, scale, width, height, cornersOnly: !editing);
    }
}
#endif
