using Themia.Core.Geometry;

namespace Themia.Core.Widgets;

/// <summary>Where one submenu of the add menu stands, and which way the cascade runs from it.</summary>
/// <param name="Box">The submenu's card, in the menu's coordinates.</param>
/// <param name="Backward">It opened against the reading direction (to the left, left to right), so the next level does too.</param>
public readonly record struct AddCascadeSpot(PixelRect Box, bool Backward);

/// <summary>
/// [S-3512] The add menu's submenus as Karpo's add list places them: each its own card beside the
/// row that opened it, never inside the menu, so opening one moves and resizes nothing. Its first
/// row lines up with that row; it overlaps the column it hangs from a little, as Windows' cascading
/// menus do; it opens forward (the reading direction) while there is room, else backward, and a
/// cascade that turned keeps going that way. It stays inside the work area. Coordinates are logical
/// (forward is +X), so right to left needs no rule of its own.
/// </summary>
public static class AddCascade
{
    /// <summary>How far a submenu laps over the column it hangs from.</summary>
    public const double Overlap = 4;

    /// <summary>
    /// Place a submenu of <paramref name="width"/> × <paramref name="height"/> beside a row:
    /// <paramref name="columnLeft"/>..<paramref name="columnRight"/> is the column the row is in,
    /// <paramref name="rowTop"/> the row's top, <paramref name="lead"/> how far the submenu's first
    /// row sits under its card's top (its frame and padding). <paramref name="backward"/> is the way
    /// the cascade already runs (false for a top column). <paramref name="room"/> is the work area.
    /// </summary>
    public static AddCascadeSpot Beside(
        double columnLeft,
        double columnRight,
        double rowTop,
        double lead,
        double width,
        double height,
        bool backward,
        PixelRect room)
    {
        var forwardX = columnRight - Overlap;
        var backwardX = columnLeft + Overlap - width;
        var right = room.X + room.Width;

        var fitsForward = forwardX + width <= right;
        var fitsBackward = backwardX >= room.X;

        // The way the cascade runs while it fits; the other way only when that one does not, and
        // when neither fits, the side with more room, pushed inside the work area.
        var goBackward = backward
            ? fitsBackward || !fitsForward && room.X - backwardX <= forwardX + width - right
            : !fitsForward && (fitsBackward || room.X - backwardX < forwardX + width - right);

        var x = goBackward ? backwardX : forwardX;
        x = Math.Max(room.X, Math.Min(x, right - width));

        var shown = Math.Min(height, room.Height);
        var y = rowTop - lead;
        y = Math.Max(room.Y, Math.Min(y, room.Y + room.Height - shown));

        return new AddCascadeSpot(new PixelRect(x, y, width, shown), goBackward);
    }
}
