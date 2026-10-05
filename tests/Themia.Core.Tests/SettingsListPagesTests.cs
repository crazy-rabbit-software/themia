using Themia.Core.Model;
using Themia.Core.Settings;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-1833] / [S-1834] The Displays and Views pages: open groups and when the page is rebuilt.</summary>
public sealed class SettingsListPagesTests
{
    [Fact]
    public void S1833_a_group_nobody_touched_is_closed_unless_it_is_the_only_one()
    {
        var expansion = new SettingsExpansion();

        Assert.False(expansion.IsOpen(SettingsList.Views, "a", count: 3));
        Assert.True(expansion.IsOpen(SettingsList.Displays, "only", count: 1));
    }

    [Fact]
    public void S1833_what_the_reader_opened_or_closed_survives_a_rebuild()
    {
        var expansion = new SettingsExpansion();

        expansion.Set(SettingsList.Views, "a", true);
        expansion.Set(SettingsList.Displays, "only", false);

        // The page is built again (a change from the desktop, a view added): the same answers.
        Assert.True(expansion.IsOpen(SettingsList.Views, "a", count: 3));
        Assert.False(expansion.IsOpen(SettingsList.Views, "b", count: 3));
        Assert.False(expansion.IsOpen(SettingsList.Displays, "only", count: 1));
    }

    [Fact]
    public void S1833_the_two_lists_keep_their_own_groups()
    {
        var expansion = new SettingsExpansion();

        expansion.Set(SettingsList.Views, "same-id", true);

        Assert.False(expansion.IsOpen(SettingsList.Displays, "same-id", count: 2));
    }

    [Fact]
    public void S1833_a_gone_item_is_forgotten_and_starts_from_the_default_if_it_comes_back()
    {
        var expansion = new SettingsExpansion();
        expansion.Set(SettingsList.Views, "a", true);
        expansion.Set(SettingsList.Views, "b", true);
        expansion.Set(SettingsList.Displays, "a", true);

        expansion.Retain(SettingsList.Views, ["b"]);

        Assert.False(expansion.IsOpen(SettingsList.Views, "a", count: 2));
        Assert.True(expansion.IsOpen(SettingsList.Views, "b", count: 2));

        // Only the list it was asked about.
        Assert.True(expansion.IsOpen(SettingsList.Displays, "a", count: 2));
    }

    [Fact]
    public void S1834_adding_deleting_or_moving_a_view_changes_the_views_page_shape()
    {
        var state = AppState.CreateDefault();
        var shape = SettingsPageSync.Shape(state, SettingsScope.Views);

        var added = ViewState.Create("Work");
        state.Views.Add(added);
        var withAdded = SettingsPageSync.Shape(state, SettingsScope.Views);
        Assert.NotEqual(shape, withAdded);

        ViewOps.MoveBy(state.Views, added.Id, -1);
        Assert.NotEqual(withAdded, SettingsPageSync.Shape(state, SettingsScope.Views));

        state.Views.Remove(added);
        Assert.Equal(shape, SettingsPageSync.Shape(state, SettingsScope.Views));
    }

    [Fact]
    public void S1834_renaming_or_restyling_a_view_keeps_the_shape_so_the_name_box_is_not_torn_down()
    {
        var state = AppState.CreateDefault();
        var shape = SettingsPageSync.Shape(state, SettingsScope.Views);

        state.Views[0].Name = "Renamed";
        state.Views[0].Icon = "\uE80F";
        state.Views[0].Widgets.Clear();

        Assert.Equal(shape, SettingsPageSync.Shape(state, SettingsScope.Views));
    }

    [Fact]
    public void S1834_switching_a_display_off_or_plugging_one_in_changes_the_displays_page_shape()
    {
        var state = AppState.CreateDefault();
        string[] connected = ["A", "B"];
        var shape = SettingsPageSync.Shape(state, SettingsScope.Displays, connected);

        state.MonitorScales["A"] = 1.25;
        Assert.Equal(shape, SettingsPageSync.Shape(state, SettingsScope.Displays, connected));

        state.DisabledMonitors.Add("B");
        Assert.NotEqual(shape, SettingsPageSync.Shape(state, SettingsScope.Displays, connected));

        state.DisabledMonitors.Remove("B");
        Assert.NotEqual(shape, SettingsPageSync.Shape(state, SettingsScope.Displays, ["A", "B", "C"]));
    }

    [Fact]
    public void S1832_the_same_row_in_two_groups_of_a_list_page_is_two_suggestions()
    {
        // Every view's group has "Duplicate this view": one suggestion per view, each naming its group.
        var desktop = new SettingEntry("Views", "Views › Desktop", null, "Duplicate this view", null, []);
        var work = new SettingEntry("Views", "Views › Work", null, "Duplicate this view", null, []);

        var found = SettingsSearch.Find([desktop, work], "duplicate");

        Assert.Equal(2, found.Count);
        Assert.Contains(found, m => m.Entry.PagePath == "Views › Work");
    }

    [Fact]
    public void S1834_other_pages_have_no_shape()
    {
        var state = AppState.CreateDefault();

        Assert.Equal(string.Empty, SettingsPageSync.Shape(state, SettingsScope.App));
        Assert.Equal(string.Empty, SettingsPageSync.Shape(state, SettingsScope.Widget));
    }
}
