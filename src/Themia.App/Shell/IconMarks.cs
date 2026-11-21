using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Shapes;
using Themia.Core.Geometry;

namespace Themia.App.Shell;

/// <summary>
/// [S-31b] [S-11889] [S-13707] [S-13708] Desktop icons' corner marks where no card draws them: the Unplaced icons page's icons
/// and a placement's icons under the pointer, so they look as the icons on the desktop in edit mode do (owner, 2026-11-20:
/// "make sure that the icons look the same as they do on the desktop, right now the corners are not shown"). Each icon's
/// four square-cornered brackets round its box (<see cref="EditMarkers"/>; an icon's size is fixed, so it has no edge bar,
/// [S-2444], as <c>WidgetCard.ApplyResizable</c> leaves it), each over its 1-DIP halo, every halo on one plate under every
/// bar on the next, as the surface's <see cref="EditMarkerLayer"/> draws them: icons that stand box against box share one
/// line where they meet. Neutral, a selected icon's in the accent over its neighbours' ([S-31c]); the card's own brushes.
/// </summary>
internal sealed class IconMarks
{
    private readonly List<Rectangle> _halos = [];
    private readonly List<Rectangle> _inks = [];

    /// <summary>Every icon's halos, under <see cref="Inks"/>. In the flow of what holds it, as the boxes it is given are.</summary>
    public Canvas Halos { get; } = new() { IsHitTestVisible = false };

    /// <summary>Every icon's bars, over every halo.</summary>
    public Canvas Inks { get; } = new() { IsHitTestVisible = false };

    /// <summary>
    /// The marks of <paramref name="icons"/> (each its box in the plates' DIPs, and whether it is selected) at
    /// <paramref name="scale"/> device pixels a DIP; the shapes are pooled, the ones left over hidden.
    /// </summary>
    public void Draw(IReadOnlyList<(DipRect Box, bool Selected)> icons, double scale)
    {
        ArgumentNullException.ThrowIfNull(icons);
        var used = 0;

        // The selected ones last: their bars over a neighbour's where they meet.
        foreach (var (box, selected) in icons.Where(i => !i.Selected).Concat(icons.Where(i => i.Selected)))
        {
            if (box.Width <= 0 || box.Height <= 0)
            {
                continue;
            }

            var ink = selected ? WidgetCard.SelectedInk() : WidgetCard.NeutralInk();
            foreach (var marker in EditMarkers.For(box.Width, box.Height, scale).Where(m => WidgetCard.IsCorner(m.Handle)))
            {
                foreach (var bar in marker.Bars)
                {
                    var at = new DipRect(box.X + marker.Hit.X + bar.X, box.Y + marker.Hit.Y + bar.Y, bar.Width, bar.Height);
                    Place(Shape(_halos, Halos, used, WidgetCard.MarkerHalo), EditMarkers.Haloed(at, scale));
                    var shape = Shape(_inks, Inks, used, ink);
                    shape.Fill = ink;
                    Place(shape, at);
                    used++;
                }
            }
        }

        for (var i = used; i < _inks.Count; i++)
        {
            _halos[i].Visibility = Visibility.Collapsed;
            _inks[i].Visibility = Visibility.Collapsed;
        }
    }

    private static Rectangle Shape(List<Rectangle> pool, Canvas plate, int index, Microsoft.UI.Xaml.Media.Brush fill)
    {
        while (pool.Count <= index)
        {
            var made = new Rectangle { Fill = fill, IsHitTestVisible = false };
            pool.Add(made);
            plate.Children.Add(made);
        }

        var shape = pool[index];
        shape.Visibility = Visibility.Visible;
        return shape;
    }

    private static void Place(Rectangle shape, DipRect box)
    {
        Canvas.SetLeft(shape, box.X);
        Canvas.SetTop(shape, box.Y);
        shape.Width = box.Width;
        shape.Height = box.Height;
    }
}
