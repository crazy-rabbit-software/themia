using Themia.Core.Widgets;
using Xunit;

namespace Themia.Widget.Folder.Tests;

/// <summary>[S-6412] Where a press in the folder landed and what it may become: the folder draws no band of its own.</summary>
public sealed class FolderPressTests
{
    [Fact]
    public void S6412_a_press_on_an_items_icon_or_name_is_an_item_press()
        => Assert.Equal(FolderPress.Item, FolderGesture.Classify(onItem: true, onIconOrName: true));

    [Fact]
    public void S6412_a_press_beside_the_name_or_on_the_background_is_not_an_item_press()
    {
        Assert.Equal(FolderPress.ItemBlank, FolderGesture.Classify(onItem: true, onIconOrName: false));
        Assert.Equal(FolderPress.Background, FolderGesture.Classify(onItem: false, onIconOrName: false));
    }

    [Theory]
    [InlineData(FolderPress.ItemBlank)]
    [InlineData(FolderPress.Background)]
    public void S6412_a_drag_from_empty_space_is_neither_a_file_drag_nor_a_band(FolderPress press)
    {
        // The one rubber band is the desktop's ([S-5544]); inside the folder a press off the icon and
        // the name stays a click however far it moves.
        var g = new FolderGesture();
        g.BeginPress(300, 10, press, [@"C:\a"]);

        Assert.False(g.Pressed);
        Assert.Equal(GestureStep.Idle, g.Move(300, 60));
        Assert.Empty(g.DragPaths);
        Assert.False(g.Release());
    }
}