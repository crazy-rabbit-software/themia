using System.Text.Json;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Settings;
using Themia.Core.Storage;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>One widget's own content scale ([S-6530]..[S-6533], [S-6536]).</summary>
public sealed class WidgetScaleTests
{
    private static string StateFile(string scale) => $$"""
        {
          "schemaVersion": 1,
          "views": [
            {
              "id": "v1",
              "name": "Desktop",
              "widgets": [
                { "id": "w1", "kind": "Clock", "x": 0.1, "y": 0.1, "w": 0.2, "h": 0.2, "scale": {{scale}}, "config": { "kind": "clock" } }
              ]
            }
          ],
          "activeViewId": "v1",
          "monitorViews": {}
        }
        """;

    private static WidgetInstance Widget(string id, double scale = 1.0)
    {
        var widget = WidgetInstance.Create(new WidgetId("cpu"), new FracRect(0, 0, 0.1, 0.1));
        widget.Id = id;
        widget.Scale = scale;
        return widget;
    }

    private static (AppState State, ViewState View) State(params WidgetInstance[] widgets)
    {
        var view = ViewState.Create("v");
        view.Widgets.AddRange(widgets);
        return (new AppState { Views = [view] }, view);
    }

    private static WidgetSelection Selecting(ViewState view, params string[] ids)
    {
        var selection = new WidgetSelection();
        selection.Replace(view.Id, ids);
        return selection;
    }

    [Fact]
    public void S6530_a_new_widget_is_at_100_percent()
        => Assert.Equal(1.0, WidgetInstance.Create(new WidgetId("clock"), new FracRect(0, 0, 0.1, 0.1)).Scale);

    [Theory]
    [InlineData(1.0, 1.0)]
    [InlineData(0.9, 0.9)]
    [InlineData(1.13, 1.15)]
    [InlineData(0.1, 0.5)]
    [InlineData(9, 2.0)]
    [InlineData(double.NaN, 1.0)]
    [InlineData(double.PositiveInfinity, 1.0)]
    public void S6530_a_scale_is_stored_in_range_on_the_5_percent_grain(double scale, double stored)
        => Assert.Equal(stored, WidgetScale.Normalise(scale));

    [Fact]
    public void S6530_every_step_is_already_stored_as_itself()
    {
        foreach (var step in WidgetScale.Steps)
        {
            Assert.Equal(step, WidgetScale.Normalise(step));
        }

        Assert.Equal([0.75, 0.9, 1.0, 1.1, 1.25, 1.5], WidgetScale.Steps);
    }

    [Fact]
    public void S6530_a_copy_keeps_the_scale()
        => Assert.Equal(1.25, Widget("a", 1.25).Clone().Scale);

    [Fact]
    public void S6530_the_scale_round_trips_through_the_state_file()
    {
        var state = AppStateStore.Parse(StateFile("1.25")).State!;
        var json = JsonSerializer.Serialize(state, StateJson.AppState);
        var back = AppStateStore.Parse(json);

        Assert.Equal(1.25, back.State!.Views[0].Widgets[0].Scale);
        Assert.Empty(back.Repairs);
    }

    [Fact]
    public void S6530_a_file_without_a_scale_reads_as_100_percent()
    {
        var result = AppStateStore.Parse(StateFile("1").Replace("\"scale\": 1, ", string.Empty, StringComparison.Ordinal));

        Assert.Equal(1.0, result.State!.Views[0].Widgets[0].Scale);
        Assert.Empty(result.Repairs);
    }

    [Theory]
    [InlineData("9", 2.0)]
    [InlineData("0.1", 0.5)]
    [InlineData("1.13", 1.15)]
    [InlineData("-1", 0.5)]
    public void S6533_a_hand_edited_scale_is_repaired_and_said(string scale, double repaired)
    {
        var result = AppStateStore.Parse(StateFile(scale));

        Assert.Equal(ParseRefusal.None, result.Refusal);
        Assert.Equal(repaired, result.State!.Views[0].Widgets[0].Scale);
        Assert.Contains(result.Repairs, r => r.Contains("w1", StringComparison.Ordinal) && r.Contains("scale", StringComparison.Ordinal));
    }

    [Fact]
    public void S6533_a_stored_scale_needs_no_repair()
    {
        var widget = Widget("a", 0.75);

        Assert.Null(WidgetScale.Repair(widget));
        Assert.Equal(0.75, widget.Scale);
    }

    [Fact]
    public void S6531_the_menu_offers_the_steps_and_a_scale_between_them_in_its_place()
    {
        Assert.Equal(WidgetScale.Steps, WidgetScale.Offered(1.0));
        Assert.Equal(WidgetScale.Steps, WidgetScale.Offered(null));
        Assert.Equal([0.75, 0.9, 1.0, 1.1, 1.25, 1.35, 1.5], WidgetScale.Offered(1.35));
        Assert.Equal([0.5, 0.75, 0.9, 1.0, 1.1, 1.25, 1.5], WidgetScale.Offered(0.5));
        Assert.Equal([0.75, 0.9, 1.0, 1.1, 1.25, 1.5, 2.0], WidgetScale.Offered(2.0));
    }

    [Fact]
    public void S6531_the_offer_reads_the_shared_scale_and_null_when_they_differ()
    {
        var (state, view) = State(Widget("a", 1.25), Widget("b", 1.25), Widget("c", 0.9));

        Assert.Equal(1.25, QuickSet.For(state, Selecting(view, "a"), view.Id).Scale);
        Assert.Equal(1.25, QuickSet.For(state, Selecting(view, "a", "b"), view.Id).Scale);
        Assert.Null(QuickSet.For(state, Selecting(view, "a", "c"), view.Id).Scale);
    }

    [Fact]
    public void S6531_setting_the_scale_reaches_every_selected_widget_as_stored()
    {
        var (_, view) = State(Widget("a"), Widget("b", 1.1), Widget("c"));

        Assert.Equal(1, QuickSet.SetScale(view, ["a", "b"], 1.1)); // b is at 1.1 already
        Assert.Equal(1.1, view.Find("a")!.Scale);
        Assert.Equal(1.0, view.Find("c")!.Scale);

        Assert.Equal(1, QuickSet.SetScale(view, ["a"], 7));
        Assert.Equal(WidgetScale.Max, view.Find("a")!.Scale);
    }

    [Fact]
    public void S6531_a_scale_the_widgets_already_have_changes_nothing()
    {
        var (_, view) = State(Widget("a", 1.5));

        Assert.Equal(0, QuickSet.SetScale(view, ["a"], 1.5));
    }

    [Fact]
    public void S6532_the_effective_scale_is_display_times_app_times_widget()
    {
        Assert.Equal(1.5 * 1.25 * 0.75, WidgetScale.Effective(1.5, 1.25, 0.75), 6);
        Assert.Equal(1.0, WidgetScale.Effective(1, 1, 1));
        Assert.Equal(2.0, WidgetScale.Effective(1, 1, 2));
    }

    [Fact]
    public void S6532_a_factor_that_is_not_a_positive_number_counts_as_one()
    {
        Assert.Equal(1.25, WidgetScale.Effective(double.NaN, 0, 1.25));
        Assert.Equal(1.5, WidgetScale.Effective(-2, double.PositiveInfinity, 1.5));
        Assert.Equal(1.0, WidgetScale.Effective(1, 1, double.NaN));
    }

    [Fact]
    public void S6536_the_settings_page_shows_no_scale_so_a_scale_change_does_not_rebuild_it()
    {
        var (state, view) = State(Widget("a"));
        var before = SettingsPageSync.Signature(state, SettingsScope.Widget, "a");

        view.Find("a")!.Scale = 1.25;

        Assert.Equal(before, SettingsPageSync.Signature(state, SettingsScope.Widget, "a"));
    }
}
