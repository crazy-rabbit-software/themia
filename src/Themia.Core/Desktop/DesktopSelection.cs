using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Desktop;

/// <summary>
/// [S-5540] One desktop item widget as it sits on the screen now: its item's key, the display it is on
/// (left to right, so the order runs over the displays as the eye does) and its box in one coordinate
/// space shared by every display (the virtual screen's physical pixels).
/// </summary>
public readonly record struct DesktopBox(string Key, int Display, double X, double Y, double Width, double Height)
{
    public double CenterX => X + (Width / 2);

    public double CenterY => Y + (Height / 2);
}

/// <summary>
/// [S-5540] [S-5541] Where the desktop's items are, as Explorer's desktop orders and walks them: down a
/// column, then the next column, display by display; an arrow goes to the nearest item that way. Pure.
/// </summary>
public static class DesktopNav
{
    /// <summary>
    /// [S-5540] The items in the desktop's order: by display, then by column (left edge), then by row
    /// (top edge), the key breaking a tie. An item shown twice (in the views of two displays) keeps its
    /// first place only: the selection holds an item once.
    /// </summary>
    public static IReadOnlyList<DesktopBox> Order(IEnumerable<DesktopBox> boxes)
    {
        ArgumentNullException.ThrowIfNull(boxes);
        var seen = new HashSet<string>(DesktopItemKeys.Comparer);
        return [.. boxes
            .Where(b => DesktopItemKeys.IsValid(b.Key) && b.Width > 0 && b.Height > 0)
            .OrderBy(b => b.Display)
            .ThenBy(b => Math.Round(b.X, 3))
            .ThenBy(b => Math.Round(b.Y, 3))
            .ThenBy(b => b.Key, DesktopItemKeys.Comparer)
            .Where(b => seen.Add(b.Key))];
    }

    /// <summary>
    /// [S-5541] The item an arrow moves to from <paramref name="from"/>: of the items whose centre lies
    /// that way, the nearest one in the same column (Up, Down) or row (Left, Right), their boxes
    /// overlapping across the arrow; with none there, the nearest centre within 45 degrees of the
    /// arrow (the next display, a column that does not line up); with none, null: the focus stays, as
    /// at the end of Explorer's column. Home and End are the first and last in <see cref="Order"/>.
    /// </summary>
    public static string? Step(IReadOnlyList<DesktopBox> ordered, string from, SelectionMove move)
    {
        ArgumentNullException.ThrowIfNull(ordered);
        if (ordered.Count == 0)
        {
            return null;
        }

        switch (move)
        {
            case SelectionMove.Home:
                return ordered[0].Key;
            case SelectionMove.End:
                return ordered[^1].Key;
        }

        var at = ordered.FirstOrDefault(b => DesktopItemKeys.Comparer.Equals(b.Key, from));
        if (at.Key is null)
        {
            return ordered[0].Key;
        }

        var (dx, dy) = move switch
        {
            SelectionMove.Up or SelectionMove.PageUp => (0, -1),
            SelectionMove.Down or SelectionMove.PageDown => (0, 1),
            SelectionMove.Left => (-1, 0),
            SelectionMove.Right => (1, 0),
            _ => (0, 0),
        };

        if (dx == 0 && dy == 0)
        {
            return null;
        }

        DesktopBox? lined = null;
        var linedDistance = double.MaxValue;
        var linedOffset = double.MaxValue;
        DesktopBox? coned = null;
        var conedDistance = double.MaxValue;

        foreach (var box in ordered)
        {
            if (DesktopItemKeys.Comparer.Equals(box.Key, at.Key))
            {
                continue;
            }

            var along = ((box.CenterX - at.CenterX) * dx) + ((box.CenterY - at.CenterY) * dy);
            if (along <= 0.5)
            {
                continue;
            }

            var across = dx != 0 ? box.CenterY - at.CenterY : box.CenterX - at.CenterX;
            var overlaps = dx != 0
                ? box.Y < at.Y + at.Height && at.Y < box.Y + box.Height
                : box.X < at.X + at.Width && at.X < box.X + box.Width;

            if (overlaps && (along < linedDistance - 0.5 || (Math.Abs(along - linedDistance) <= 0.5 && Math.Abs(across) < linedOffset)))
            {
                lined = box;
                linedDistance = along;
                linedOffset = Math.Abs(across);
            }

            var distance = (along * along) + (across * across);
            if (Math.Abs(across) <= along && distance < conedDistance)
            {
                coned = box;
                conedDistance = distance;
            }
        }

        return (lined ?? coned)?.Key;
    }
}

/// <summary>
/// [S-5540]..[S-5544] The one selection of the desktop's items, across every item widget on every
/// display, as Explorer's desktop keeps its icons': the SDK's <see cref="ItemSelection"/> over the
/// items in <see cref="DesktopNav.Order"/>, the arrows by the items' places, type-to-find
/// (<see cref="TypeAhead"/>) and the band. The host feeds it the layout and the gestures and paints
/// what it says. Pure: the clock of type-to-find is passed in.
/// </summary>
public sealed class DesktopSelection
{
    private readonly ItemSelection _selection = new(DesktopItemKeys.Comparer);
    private readonly TypeAhead _typeAhead = new();
    private IReadOnlyList<DesktopBox> _ordered = [];
    private IReadOnlyList<string> _names = [];
    private string? _pendingKey;

    /// <summary>The items in the desktop's order, as last laid out.</summary>
    public IReadOnlyList<DesktopBox> Ordered => _ordered;

    /// <summary>The selected keys, in the desktop's order.</summary>
    public IReadOnlyList<string> Selected => _selection.Selected;

    /// <summary>The focused item's key (the one F2, the Menu key and the arrows start from), or null.</summary>
    public string? Focus => _selection.Focus;

    public int Count => _selection.Count;

    public bool IsSelected(string key) => _selection.IsSelected(key);

    /// <summary>[S-6628] Whether <paramref name="key"/> draws the focus cue: focused, and the keys moved it there.</summary>
    public bool ShowsFocus(string key) => _selection.Cues && _selection.IsFocus(key);

    /// <summary>
    /// The items as they sit now (every display's shown item widgets) and the names type-to-find
    /// reads. Selected items no longer shown leave the selection; the rest keep it. An item asked
    /// for with <see cref="SelectWhenShown"/> is selected alone once it is shown.
    /// </summary>
    public void SetLayout(IEnumerable<DesktopBox> boxes, Func<string, string?> nameOf)
    {
        ArgumentNullException.ThrowIfNull(boxes);
        ArgumentNullException.ThrowIfNull(nameOf);

        _ordered = DesktopNav.Order(boxes);
        _names = [.. _ordered.Select(b => nameOf(b.Key) ?? DesktopItemKeys.FallbackName(b.Key))];
        _selection.SetOrder([.. _ordered.Select(b => b.Key)]);

        if (_pendingKey is { } pending && _selection.SelectOnly(pending))
        {
            _pendingKey = null;
        }
    }

    /// <summary>[S-5543] Select <paramref name="key"/> alone as soon as it is shown (a renamed item under its new key, a pasted one).</summary>
    public void SelectWhenShown(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (!_selection.SelectOnly(key))
        {
            _pendingKey = key;
        }
    }

    /// <summary>[S-6621] A left press on an item: Ctrl toggles, Shift selects the range, a press on a selected item narrows only on release.</summary>
    public void Press(string key, bool ctrl, bool shift)
    {
        _typeAhead.Reset();
        _selection.Press(_selection.IndexOf(key), ctrl, shift);
    }

    /// <summary>[S-6621] The release of a press that was no drag.</summary>
    public void Release(string key) => _selection.Release(_selection.IndexOf(key));

    /// <summary>A drag ran from the press: the selection stays as it was.</summary>
    public void CancelPress() => _selection.CancelPress();

    /// <summary>[S-6622] A right press on an item: outside the selection it becomes the selection; inside, the selection stays.</summary>
    public void Context(string key) => _selection.Context(_selection.IndexOf(key));

    /// <summary>
    /// [S-5541] [S-6623] The selection's keys: the arrows by the items' places (Shift extends, Ctrl moves
    /// the focus only), Home and End, Page Up and Down as Up and Down, Space and Ctrl+Space, Ctrl+A.
    /// False for any other key (Enter, F2, Delete, the clipboard: the host's commands).
    /// </summary>
    public bool Key(WidgetKey key, bool shift)
    {
        var (move, extend, focusOnly) = key switch
        {
            WidgetKey.Up => (SelectionMove.Up, shift, false),
            WidgetKey.Down => (SelectionMove.Down, shift, false),
            WidgetKey.PageUp => (SelectionMove.Up, shift, false),
            WidgetKey.PageDown => (SelectionMove.Down, shift, false),
            WidgetKey.Left => (SelectionMove.Left, false, false),
            WidgetKey.Right => (SelectionMove.Right, false, false),
            WidgetKey.ShiftLeft => (SelectionMove.Left, true, false),
            WidgetKey.ShiftRight => (SelectionMove.Right, true, false),
            WidgetKey.Home => (SelectionMove.Home, shift, false),
            WidgetKey.End => (SelectionMove.End, shift, false),
            WidgetKey.FocusUp => (SelectionMove.Up, false, true),
            WidgetKey.FocusDown => (SelectionMove.Down, false, true),
            WidgetKey.FocusLeft => (SelectionMove.Left, false, true),
            WidgetKey.FocusRight => (SelectionMove.Right, false, true),
            _ => ((SelectionMove?)null, false, false),
        };

        if (move is { } step)
        {
            if (_ordered.Count == 0)
            {
                return false;
            }

            _typeAhead.Reset();
            var target = _selection.Focus is { } from
                ? DesktopNav.Step(_ordered, from, step) ?? from
                : step == SelectionMove.End ? _ordered[^1].Key : _ordered[0].Key;
            return _selection.MoveTo(_selection.IndexOf(target), extend, focusOnly);
        }

        return key switch
        {
            WidgetKey.Space or WidgetKey.ToggleSelection => _selection.ToggleFocus(),
            WidgetKey.SelectAll => SelectAll(),
            _ => false,
        };
    }

    /// <summary>[S-5542] A character typed: the next item whose name starts with what was typed is selected alone. False when it does not type or nothing matches.</summary>
    public bool Type(char character, long nowMs)
    {
        if (_typeAhead.Type(character, nowMs, _names, _selection.FocusIndex) is not { } found)
        {
            return false;
        }

        _selection.Type(found);
        return true;
    }

    /// <summary>[S-6623] Ctrl+A: every item shown on every display.</summary>
    public bool SelectAll()
    {
        if (_ordered.Count == 0)
        {
            return false;
        }

        _selection.SelectAll();
        return true;
    }

    /// <summary>[S-5544] [S-6625] A band starts on empty desktop: Ctrl toggles, Shift adds, neither starts from nothing.</summary>
    public void BeginBand(bool ctrl, bool shift)
    {
        _typeAhead.Reset();
        _selection.BeginBand(SelectionBand.Mode(ctrl, shift));
    }

    /// <summary>[S-5544] The band covers <paramref name="keys"/> now (live: an item it leaves goes back to how it was).</summary>
    public void Band(IEnumerable<string> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        _selection.Band(keys.Select(_selection.IndexOf).Where(i => i >= 0).Order());
    }

    public void EndBand() => _selection.EndBand();

    /// <summary>[S-5502] A press on empty desktop: nothing selected. Returns whether anything was.</summary>
    public bool Clear()
    {
        _typeAhead.Reset();
        return _selection.Clear();
    }

    /// <summary>[S-6626] The keyboard went elsewhere: the selection stays, the focus cue goes.</summary>
    public void Deactivate()
    {
        _typeAhead.Reset();
        _selection.Deactivate();
    }
}
