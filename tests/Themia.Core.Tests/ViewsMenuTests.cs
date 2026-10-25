using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Settings;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-3541] [S-3542] The edit bar's View menu as data: this display's views in the user's order, the
/// one shown here holding, each line showing its view here and opening what the view offers, the
/// views dragged to reorder; then New view and Duplicate this view. Every write goes through the host.
/// </summary>
[Collection(GlobalLocaleCollection.Name)]
public sealed class ViewsMenuTests
{
    private const string Here = "display-here";

    /// <summary>A host over three views of its own (Work shown here), recording what was asked of it.</summary>
    private sealed class FakeHost : IViewsMenuHost
    {
        public FakeHost()
        {
            foreach (var (id, name) in new[] { ("home", "Home"), ("work", "Work"), ("evening", "Evening") })
            {
                var view = ViewState.Create(name);
                view.Id = id;
                State.Views.Add(view);
            }

            State.ActiveViewId = "home";
            State.MonitorViews[Here] = "work";
            State.Views[1].Icon = Glyphs.Mail;
            State.Views[1].Color = "#0099BC";
        }

        public AppState State { get; } = new() { Views = [] };

        public string DisplayId => Here;

        public int Mutations { get; private set; }

        public List<string> Log { get; } = [];

        public List<SettingsConfirm> Asked { get; } = [];

        public void Mutate(Action<AppState> change, bool relayout = true)
        {
            change(State);
            Mutations++;
        }

        public void Show(string viewId) => Log.Add("show " + viewId);

        public void Add() => Log.Add("add");

        public void Duplicate(string viewId) => Log.Add("duplicate " + viewId);

        public void Delete(string viewId) => Log.Add("delete " + viewId);

        public void Reorder(string viewId, string targetId) => Log.Add($"reorder {viewId} {targetId}");

        public void Confirm(SettingsConfirm confirm) => Asked.Add(confirm);
    }

    private static T At<T>(FakeHost host, params string[] path)
        where T : SettingsLine
        => Assert.IsType<T>(SettingsMenu.Find(ViewsMenu.Root(host), path));

    private static readonly string Work = ViewsMenu.Keys.View("work");

    [Fact]
    public void S3541_the_views_in_their_order_then_new_view_and_duplicate_this_view()
    {
        var keys = ViewsMenu.Root(new FakeHost()).Select(l => l is SettingsSeparator ? "-" : l.Key).ToArray();

        Assert.Equal(["view:home", "view:work", "view:evening", "-", ViewsMenu.Keys.New, ViewsMenu.Keys.DuplicateThis], keys);
    }

    [Fact]
    public void S3541_the_view_this_display_shows_holds_and_wears_its_icon_in_its_colour()
    {
        var host = new FakeHost();
        var lines = ViewsMenu.Root(host).OfType<SettingsSubmenu>().ToList();

        Assert.Equal([false, true, false], lines.Select(l => l.Holds));
        var work = lines[1];
        Assert.Equal("Work", work.Text);
        Assert.Equal(Glyphs.Mail, work.Glyph);
        Assert.Equal("#0099BC", work.GlyphColour);

        // A view without an icon wears the shared view glyph, in the theme's colour.
        Assert.Equal(Glyphs.View, lines[0].Glyph);
        Assert.Null(lines[0].GlyphColour);
    }

    [Fact]
    public void S3541_a_click_on_another_view_shows_it_here_and_the_shown_one_only_opens()
    {
        var host = new FakeHost();

        At<SettingsSubmenu>(host, "view:evening").Run!();
        Assert.Equal(["show evening"], host.Log);

        Assert.Null(At<SettingsSubmenu>(host, Work).Run);
    }

    [Fact]
    public void S3541_a_display_with_no_view_of_its_own_holds_the_active_one()
    {
        var host = new FakeHost();
        host.State.MonitorViews.Remove(Here);

        Assert.True(At<SettingsSubmenu>(host, "view:home").Holds);
        Assert.Equal("home", ViewsMenu.ShownOn(host.State, Here));
    }

    [Fact]
    public void S3541_new_view_and_duplicate_this_view_ask_the_host_for_this_display()
    {
        var host = new FakeHost();

        At<SettingsCommand>(host, ViewsMenu.Keys.New).Run();
        At<SettingsCommand>(host, ViewsMenu.Keys.DuplicateThis).Run();

        Assert.Equal(["add", "duplicate work"], host.Log);
        Assert.Equal(0, host.Mutations);
    }

    [Fact]
    public void S3732_every_views_line_is_one_ordered_list_and_a_drop_moves_by_id()
    {
        // Regression: each line carried a move of its own, so the engine saw three lists of one and
        // a dragged view had nowhere to go.
        var host = new FakeHost();
        var lines = ViewsMenu.Root(host).OfType<SettingsSubmenu>().ToList();

        Assert.All(lines, l => Assert.NotNull(l.Order));
        Assert.Equal([0, 1, 2], lines.Select(l => l.Order!.Index));
        Assert.All(lines, l => Assert.Equal(3, l.Order!.Count));
        Assert.Single(lines.Select(l => l.Order!.Move).Distinct());

        lines[0].Order!.Move(0, 2);
        lines[0].Order!.Move(1, 1);
        Assert.Equal(["reorder home evening"], host.Log);
    }

    [Fact]
    public void S3802_a_move_past_either_end_moves_nothing()
    {
        var host = new FakeHost();
        var order = ViewsMenu.Root(host).OfType<SettingsSubmenu>().First().Order!;

        order.Move(0, -1);
        order.Move(2, 3);
        Assert.Empty(host.Log);
    }

    [Fact]
    public void S3542_a_views_submenu_says_its_widgets_then_rename_icon_colour_duplicate_delete()
    {
        var level = At<SettingsSubmenu>(new FakeHost(), Work).Lines;

        Assert.Equal(
            [Work + "/count", Work + "/rename", Work + "/icon", Work + "/colour", "-", Work + "/duplicate", Work + "/delete"],
            level.Select(l => l is SettingsSeparator ? "-" : l.Key));
        Assert.Equal(Str.Shell_Views_WidgetCount.Plural(0), Assert.IsType<SettingsNote>(level[0]).Text);
    }

    [Fact]
    public void S3542_rename_asks_with_the_name_and_stores_it_trimmed()
    {
        var host = new FakeHost();
        var rename = At<SettingsAsk>(host, Work, Work + "/rename");

        Assert.Equal("Work", rename.Initial);
        rename.Done("  Office  ");

        Assert.Equal("Office", host.State.FindView("work")!.Name);
        Assert.Equal(1, host.Mutations);
    }

    [Fact]
    public void S3542_the_icon_line_offers_the_initials_as_its_default_and_stores_a_glyph_or_none()
    {
        var host = new FakeHost();
        var icon = At<SettingsIcon>(host, Work, Work + "/icon");

        Assert.Equal(Glyphs.Mail, icon.Current);
        Assert.Equal("WO", icon.DefaultText);

        icon.Set(Glyphs.Home);
        Assert.Equal(Glyphs.Home, host.State.FindView("work")!.Icon);

        At<SettingsIcon>(host, Work, Work + "/icon").Set(null);
        Assert.Null(host.State.FindView("work")!.Icon);
        Assert.Null(At<SettingsIcon>(host, Work, Work + "/icon").Current);
    }

    [Fact]
    public void S3810_the_colour_line_stores_rrggbb_and_its_default_clears_it()
    {
        var host = new FakeHost();
        var colour = At<SettingsColour>(host, Work, Work + "/colour");

        Assert.Equal("#0099BC", colour.Current);
        colour.Set("#e74856");
        Assert.Equal("#E74856", host.State.FindView("work")!.Color);

        // [S-3812] The menu follows: the view's line wears the new colour.
        Assert.Equal("#E74856", At<SettingsSubmenu>(host, Work).GlyphColour);

        At<SettingsColour>(host, Work, Work + "/colour").Set(string.Empty);
        Assert.Null(host.State.FindView("work")!.Color);
    }

    [Fact]
    public void S3542_delete_asks_first_and_only_the_answer_deletes()
    {
        var host = new FakeHost();
        var delete = At<SettingsCommand>(host, Work, Work + "/delete");

        Assert.True(delete.Danger);
        Assert.False(delete.Disabled);
        delete.Run();

        var asked = Assert.Single(host.Asked);
        Assert.True(asked.Danger);
        Assert.Contains("Work", asked.Question, StringComparison.Ordinal);
        Assert.Empty(host.Log);

        asked.Done();
        Assert.Equal(["delete work"], host.Log);
    }

    [Fact]
    public void S12_the_last_view_cannot_be_deleted()
    {
        var host = new FakeHost();
        host.State.Views.RemoveAll(v => v.Id != "work");

        Assert.True(At<SettingsCommand>(host, Work, Work + "/delete").Disabled);
    }

    [Fact]
    public void S3541_keys_hold_the_ids_so_a_rename_or_a_language_switch_keeps_the_shape()
    {
        var host = new FakeHost();
        var before = ViewsMenu.Root(host).Select(l => l.Key).ToArray();

        host.State.FindView("work")!.Name = "Office";
        try
        {
            Strings.SetLocale(Locale.De);
            Assert.Equal(before, ViewsMenu.Root(host).Select(l => l.Key).ToArray());
        }
        finally
        {
            Strings.SetLocale(Locale.En);
        }
    }
}
