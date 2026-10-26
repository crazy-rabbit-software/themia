using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Settings;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-3541] [S-3542] [S-3549] The edit bar's View menu as data: the view this display shows, its own
/// lines (its widgets, Name, Icon, Colour; Duplicate and Delete), then Manage views, every view in the
/// user's order with Rename and Delete at its end, dragged to reorder, and New view on top. Which view
/// shows is Display menu's. Every write goes through the host.
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

        public void Add() => Log.Add("add");

        public void Duplicate(string viewId) => Log.Add("duplicate " + viewId);

        public void Delete(string viewId) => Log.Add("delete " + viewId);

        public void Reorder(string viewId, string targetId) => Log.Add($"reorder {viewId} {targetId}");

        public void Confirm(SettingsConfirm confirm) => Asked.Add(confirm);
    }

    private static T At<T>(FakeHost host, params string[] path)
        where T : SettingsLine
        => Assert.IsType<T>(SettingsMenu.Find(ViewsMenu.Root(host), path));

    private const string Manage = ViewsMenu.Keys.Manage;

    private static readonly string Work = ViewsMenu.Keys.View("work");

    private static List<SettingsItem> Items(FakeHost host) => [.. At<SettingsSubmenu>(host, Manage).Lines.OfType<SettingsItem>()];

    [Fact]
    public void S3542_the_shown_views_own_lines_then_manage_views()
    {
        var keys = ViewsMenu.Root(new FakeHost()).Select(l => l is SettingsSeparator ? "-" : l.Key).ToArray();

        Assert.Equal(
            [ViewsMenu.Keys.Count, ViewsMenu.Keys.Name, ViewsMenu.Keys.Icon, ViewsMenu.Keys.Colour, "-", ViewsMenu.Keys.Duplicate, ViewsMenu.Keys.Delete, "-", Manage],
            keys);
        Assert.Equal(Str.Shell_Views_WidgetCount.Plural(0), At<SettingsNote>(new FakeHost(), ViewsMenu.Keys.Count).Text);
    }

    [Fact]
    public void S3590_the_view_menu_does_not_switch_views_the_display_menu_does()
    {
        // [S-3543] Which view a display shows is set in Display alone: no line here shows a view.
        var lines = ViewsMenu.Root(new FakeHost());
        Assert.DoesNotContain(lines, l => l is SettingsRadio or SettingsSubmenu { Run: not null });
        Assert.All(Items(new FakeHost()), i => Assert.DoesNotContain(i.Tools, t => t.Run is null && t.Ask is null));
    }

    [Fact]
    public void S3542_the_lines_are_the_view_this_display_shows_else_the_active_one()
    {
        var host = new FakeHost();
        Assert.Equal("Work", At<SettingsAsk>(host, ViewsMenu.Keys.Name).Value);
        Assert.Equal(Glyphs.Mail, At<SettingsIcon>(host, ViewsMenu.Keys.Icon).Glyph);

        host.State.MonitorViews.Remove(Here);
        Assert.Equal("Home", At<SettingsAsk>(host, ViewsMenu.Keys.Name).Value);
        Assert.Equal("home", ViewsMenu.ShownOn(host.State, Here));
    }

    [Fact]
    public void S3542_name_asks_with_the_name_and_stores_it_trimmed()
    {
        var host = new FakeHost();
        var rename = At<SettingsAsk>(host, ViewsMenu.Keys.Name);

        Assert.Equal("Work", rename.Initial);
        rename.Done("  Office  ");

        Assert.Equal("Office", host.State.FindView("work")!.Name);
        Assert.Equal(1, host.Mutations);
    }

    [Fact]
    public void S3542_the_icon_line_offers_the_initials_as_its_default_and_stores_a_glyph_or_none()
    {
        var host = new FakeHost();
        var icon = At<SettingsIcon>(host, ViewsMenu.Keys.Icon);

        Assert.Equal(Glyphs.Mail, icon.Current);
        Assert.Equal("WO", icon.DefaultText);

        icon.Set(Glyphs.Home);
        Assert.Equal(Glyphs.Home, host.State.FindView("work")!.Icon);

        At<SettingsIcon>(host, ViewsMenu.Keys.Icon).Set(null);
        Assert.Null(host.State.FindView("work")!.Icon);
        Assert.Null(At<SettingsIcon>(host, ViewsMenu.Keys.Icon).Current);
    }

    [Fact]
    public void S3810_the_colour_line_stores_rrggbb_and_its_default_clears_it()
    {
        var host = new FakeHost();
        var colour = At<SettingsColour>(host, ViewsMenu.Keys.Colour);

        Assert.Equal("#0099BC", colour.Current);
        colour.Set("#e74856");
        Assert.Equal("#E74856", host.State.FindView("work")!.Color);

        // [S-3812] The menu follows: the view's icon and its row in Manage views wear the new colour.
        Assert.Equal("#E74856", At<SettingsIcon>(host, ViewsMenu.Keys.Icon).GlyphColour);
        Assert.Equal("#E74856", Items(host)[1].GlyphColour);

        At<SettingsColour>(host, ViewsMenu.Keys.Colour).Set(string.Empty);
        Assert.Null(host.State.FindView("work")!.Color);
    }

    [Fact]
    public void S3542_duplicate_asks_the_host_for_the_shown_view()
    {
        var host = new FakeHost();

        At<SettingsCommand>(host, ViewsMenu.Keys.Duplicate).Run();

        Assert.Equal(["duplicate work"], host.Log);
        Assert.Equal(0, host.Mutations);
    }

    [Fact]
    public void S3542_delete_asks_first_and_only_the_answer_deletes()
    {
        var host = new FakeHost();
        var delete = At<SettingsCommand>(host, ViewsMenu.Keys.Delete);

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

        Assert.True(At<SettingsCommand>(host, ViewsMenu.Keys.Delete).Disabled);
        Assert.True(Assert.Single(Items(host)).Tools[1].Disabled);
    }

    [Fact]
    public void S3549_manage_views_is_new_view_then_every_view_in_its_order_with_its_mark()
    {
        var host = new FakeHost();
        var level = At<SettingsSubmenu>(host, Manage).Lines;

        Assert.Equal([ViewsMenu.Keys.New, "-", ViewsMenu.Keys.View("home"), Work, ViewsMenu.Keys.View("evening")], level.Select(l => l is SettingsSeparator ? "-" : l.Key));
        var work = Items(host)[1];
        Assert.Equal(("Work", Glyphs.Mail, "#0099BC"), (work.Text, work.Glyph, work.GlyphColour));
        Assert.Equal(Glyphs.View, Items(host)[0].Glyph);

        At<SettingsCommand>(host, Manage, ViewsMenu.Keys.New).Run();
        Assert.Equal(["add"], host.Log);
    }

    [Fact]
    public void S3549_each_view_has_rename_on_f2_and_delete_on_del_at_its_end()
    {
        var host = new FakeHost();
        var tools = Items(host)[1].Tools;

        Assert.Equal([SettingsToolKey.Rename, SettingsToolKey.Delete], tools.Select(t => t.Shortcut));
        Assert.Equal([Glyphs.Edit, Glyphs.Delete], tools.Select(t => t.Glyph));

        var rename = Assert.IsType<SettingsAsk>(tools[0].Ask);
        Assert.Equal("Work", rename.Initial);
        rename.Done(" Office ");
        Assert.Equal("Office", host.State.FindView("work")!.Name);

        tools[1].Run!();
        var asked = Assert.Single(host.Asked);
        Assert.Empty(host.Log);
        asked.Done();
        Assert.Equal(["delete work"], host.Log);
    }

    [Fact]
    public void S3732_every_views_row_is_one_ordered_list_and_a_drop_moves_by_id()
    {
        // Regression: each line carried a move of its own, so the engine saw three lists of one and
        // a dragged view had nowhere to go.
        var host = new FakeHost();
        var items = Items(host);

        Assert.All(items, l => Assert.NotNull(l.Order));
        Assert.Equal([0, 1, 2], items.Select(l => l.Order!.Index));
        Assert.All(items, l => Assert.Equal(3, l.Order!.Count));
        Assert.Single(items.Select(l => l.Order!.Move).Distinct());

        items[0].Order!.Move(0, 2);
        items[0].Order!.Move(1, 1);
        Assert.Equal(["reorder home evening"], host.Log);
    }

    [Fact]
    public void S3802_a_move_past_either_end_moves_nothing()
    {
        var host = new FakeHost();
        var order = Items(host)[0].Order!;

        order.Move(0, -1);
        order.Move(2, 3);
        Assert.Empty(host.Log);
    }

    [Fact]
    public void S3541_keys_hold_the_ids_so_a_rename_or_a_language_switch_keeps_the_shape()
    {
        var host = new FakeHost();
        string[] Keys() => [.. ViewsMenu.Root(host).Select(l => l.Key), .. At<SettingsSubmenu>(host, Manage).Lines.Select(l => l.Key)];
        var before = Keys();

        host.State.FindView("work")!.Name = "Office";
        try
        {
            Strings.SetLocale(Locale.De);
            Assert.Equal(before, Keys());
        }
        finally
        {
            Strings.SetLocale(Locale.En);
        }
    }
}
