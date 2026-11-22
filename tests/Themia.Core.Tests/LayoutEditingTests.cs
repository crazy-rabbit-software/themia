using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Series;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-15526] A container's three states: the locked desktop and edit mode (it is one widget: any press on it stands for it) and
/// its layout being edited (its parts are picked; it and its other days hold still). The rules the surface asks.
/// </summary>
public class LayoutEditingTests
{
    private static WidgetInstance W(string id, WidgetConfig config) => new() { Id = id, Config = config, Rect = new FracRect(0.1, 0.1, 0.1, 0.1) };

    /// <summary>A forecast "fc" holding the parts "day" and "sky", and a free "clock".</summary>
    private static ViewState View()
    {
        var view = new ViewState { Id = "v", Name = "v" };
        view.Widgets.AddRange(
        [
            W("fc", new RepeaterFake { Members = ["day", "sky"], RepeatBy = SeriesType.Day }),
            W("day", new DayFake()),
            W("sky", new DayFake()),
            W("clock", new PlainFake()),
        ]);
        return view;
    }

    [Theory]
    [InlineData(false, null, "fc")] // the locked desktop: a part is its forecast
    [InlineData(true, null, "fc")] // edit mode: still its forecast, selected and moved as one widget
    [InlineData(true, "fc", "day")] // its layout edited: the part itself
    [InlineData(true, "other", "fc")] // another container's layout edited: not this one's parts
    public void S15526_a_press_on_a_part_stands_for_its_container_until_its_layout_is_edited(bool editMode, string? editing, string stands)
        => Assert.Equal(stands, LayoutEditing.Stands(View(), "day", copyOwner: null, editMode, editing));

    [Theory]
    [InlineData(false, null)]
    [InlineData(true, null)]
    [InlineData(true, "fc")]
    public void S15526_a_press_on_another_day_always_stands_for_the_container(bool editMode, string? editing)
        => Assert.Equal("fc", LayoutEditing.Stands(View(), "day", copyOwner: "fc", editMode, editing));

    [Fact]
    public void S15526_a_free_widget_and_a_container_stand_for_themselves()
    {
        Assert.Equal("clock", LayoutEditing.Stands(View(), "clock", null, editMode: true, editing: "fc"));
        Assert.Equal("fc", LayoutEditing.Stands(View(), "fc", null, editMode: true, editing: null));
        Assert.Equal("fc", LayoutEditing.Stands(View(), "fc", null, editMode: false, editing: null));
    }

    [Fact]
    public void S15526_the_first_click_selects_a_container_and_the_second_edits_its_layout()
    {
        var view = View();

        // Not selected yet: the click selects it.
        Assert.False(LayoutEditing.Enters(view, "fc", editMode: true, editing: null, "v", []));
        Assert.False(LayoutEditing.Enters(view, "fc", true, null, "v", ["clock"]));

        // Selected alone on its view: the click edits its layout.
        Assert.True(LayoutEditing.Enters(view, "fc", true, null, "v", ["fc"]));

        // Never from a selection of several, another view's selection, the locked desktop, a widget that is no container,
        // or while a layout is edited already.
        Assert.False(LayoutEditing.Enters(view, "fc", true, null, "v", ["fc", "clock"]));
        Assert.False(LayoutEditing.Enters(view, "fc", true, null, "other", ["fc"]));
        Assert.False(LayoutEditing.Enters(view, "fc", false, null, "v", ["fc"]));
        Assert.False(LayoutEditing.Enters(view, "clock", true, null, "v", ["clock"]));
        Assert.False(LayoutEditing.Enters(view, "fc", true, "fc", "v", ["fc"]));
    }

    [Fact]
    public void S15526_while_its_layout_is_edited_the_container_holds_still_and_its_parts_do_not()
    {
        Assert.True(LayoutEditing.HeldStill("fc", "fc"));
        Assert.False(LayoutEditing.HeldStill("fc", "day"));
        Assert.False(LayoutEditing.HeldStill(null, "fc"));
    }

    [Fact]
    public void S15526_the_editing_goes_on_with_nothing_or_its_own_parts_selected_and_ends_with_anything_else()
    {
        var view = View();

        Assert.True(LayoutEditing.GoesOn(view, "fc", editMode: true, "v", []));
        Assert.True(LayoutEditing.GoesOn(view, "fc", true, "v", ["day"]));
        Assert.True(LayoutEditing.GoesOn(view, "fc", true, "v", ["day", "sky"]));

        Assert.False(LayoutEditing.GoesOn(view, "fc", true, "v", ["clock"]));
        Assert.False(LayoutEditing.GoesOn(view, "fc", true, "v", ["day", "clock"]));
        Assert.False(LayoutEditing.GoesOn(view, "fc", true, "v", ["fc"])); // the container itself picked: a widget again
        Assert.False(LayoutEditing.GoesOn(view, "fc", true, "other", ["day"]));
        Assert.False(LayoutEditing.GoesOn(view, "fc", editMode: false, "v", []));
        Assert.False(LayoutEditing.GoesOn(view, "gone", true, "v", []));
        Assert.False(LayoutEditing.GoesOn(view, "clock", true, "v", []));
    }

    [Fact]
    public void S15526_a_part_dragged_out_of_its_day_is_removed_and_one_moved_inside_it_stays()
    {
        Assert.Equal(["sky"], LayoutEditing.Removed(["day", "sky"], ["sky"], ["day"]));
        Assert.Empty(LayoutEditing.Removed(["day", "sky"], ["sky"], ["day", "sky"]));
        Assert.Empty(LayoutEditing.Removed(["day", "sky"], ["clock"], ["day", "sky"])); // what moved was never a part
        Assert.Empty(LayoutEditing.Removed(["day"], ["sky"], ["day", "sky"])); // a part that joined
    }
}
