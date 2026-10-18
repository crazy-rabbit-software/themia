using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// Package PH: the list cursor the trackers share ([S-1457]). The clipboard's own tests moved with it
/// into its module (`src/Widgets/Themia.Widget.Clipboard/Tests`).
/// </summary>
public class TrackersLayoutTests
{
    // ---- Keyboard cursor ---------------------------------------------------------------------

    [Theory]
    [InlineData(5, null, WidgetKey.Down, 0)]
    [InlineData(5, null, WidgetKey.Up, 4)]
    [InlineData(5, 2, WidgetKey.Down, 3)]
    [InlineData(5, 4, WidgetKey.Down, 4)]
    [InlineData(5, 0, WidgetKey.Up, 0)]
    [InlineData(5, 3, WidgetKey.Home, 0)]
    [InlineData(5, 1, WidgetKey.End, 4)]
    [InlineData(5, 2, WidgetKey.Right, 3)]
    [InlineData(5, 2, WidgetKey.Left, 1)]
    [InlineData(5, 2, WidgetKey.Enter, 2)]
    [InlineData(5, 9, WidgetKey.Enter, null)]
    [InlineData(5, 2, WidgetKey.Escape, null)]
    [InlineData(0, 2, WidgetKey.Down, null)]
    public void S1457_cursor_moves_clamped(int count, int? current, WidgetKey key, int? expected)
        => Assert.Equal(expected, ItemCursor.Move(count, current, key));
}
