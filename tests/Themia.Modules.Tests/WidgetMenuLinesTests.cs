using Themia.App.Shell;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Settings;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>[S-3718] [S-3719] [S-3724]..[S-3730] The lines a module puts on its properties menu, as the SDK builds them.</summary>
public sealed class WidgetMenuLinesTests
{
    private int _saves;

    private WidgetTextAsk? _asked;

    private WidgetConfirm? _confirm;

    private WidgetMenuLines Lines() => new(() => _saves++, ask => _asked = ask, confirm => _confirm = confirm);

    [Fact]
    public void S3718_a_choice_is_a_submenu_of_one_group_with_the_current_option_marked()
    {
        var choice = Lines().Choice("g", "Open with", [(ItemClickMode.Double, "Double"), (ItemClickMode.Single, "Single")], ItemClickMode.Single, _ => { });

        Assert.Null(choice.Action);
        var options = choice.Children!;
        Assert.Equal(["Double", "Single"], options.Select(o => o.Text));
        Assert.Equal([false, true], options.Select(o => o.Checked == true));
        Assert.All(options, o => Assert.Equal("Open with", o.Group));
    }

    [Fact]
    public void S3718_picking_another_option_sets_it_and_saves_and_the_current_one_does_nothing()
    {
        var set = new List<ItemClickMode>();
        var options = Lines().Choice("g", "Open with", [(ItemClickMode.Double, "Double"), (ItemClickMode.Single, "Single")], ItemClickMode.Double, set.Add).Children!;

        options[0].Action!();
        Assert.Empty(set);
        Assert.Equal(0, _saves);

        options[1].Action!();
        Assert.Equal([ItemClickMode.Single], set);
        Assert.Equal(1, _saves);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void S3719_a_switch_is_ticked_while_on_and_a_click_turns_it_the_other_way_and_saves(bool on)
    {
        bool? set = null;
        var toggle = Lines().Toggle(null, "Icons only", on, value => set = value);

        Assert.Equal(on, toggle.Checked);
        Assert.Null(toggle.Group);
        toggle.Action!();
        Assert.Equal(!on, set);
        Assert.Equal(1, _saves);
    }

    [Fact]
    public void S3719_a_module_without_lines_adds_none()
    {
        var lines = Lines();
        ((IWidgetModule)new NoLines()).MenuLines(lines, new ShortcutsConfig());

        Assert.Empty(lines.Lines);
        Assert.Empty(lines.StyleLines);
    }

    [Fact]
    public void S3736_every_line_the_sdk_builds_that_asks_says_so()
    {
        var menu = Lines();
        var feeds = new List<string> { "a" };
        var list = menu.List(null, "Feeds", feeds, new MenuList<string>(f => f, "None")
        {
            Rename = new MenuListRename<string>((_, _) => true),
            Add = new MenuListAdd<string>("Add a feed", "https://", _ => null, f => f),
        });

        // A number asked for as text, an item's Rename… and the list's Add… open a field's level;
        // a confirmed line its question's.
        Assert.True(menu.Number<int>(null, "Every", 30, 5, 600, 5, n => n.ToString(System.Globalization.CultureInfo.InvariantCulture), _ => { }).Asks);
        Assert.True(list.Children![0].Children![0].Asks);
        Assert.True(list.Children!.Last().Asks);
        Assert.True(menu.Confirm(null, "Clear", "Clear it?", "Clear", () => { }).Asks);

        // What does not ask has no level of its own to open.
        Assert.False(menu.Toggle(null, "On", true, _ => { }).Asks);
        Assert.False(menu.Command(null, "Do", () => { }).Asks);
        Assert.False(menu.Number<int>(null, "Rows", 4, 1, 10, 1, n => n.ToString(System.Globalization.CultureInfo.InvariantCulture), _ => { }).Asks);
    }

    [Fact]
    public void S3749_the_accounts_line_is_a_link_to_accounts_and_greyed_without_an_edit_bar()
    {
        var opened = 0;
        var menu = new WidgetMenuLines(() => _saves++, ask => _asked = ask) { ShowAccounts = () => opened++ };
        var line = menu.AccountsLine(null, "Manage accounts");

        Assert.Equal(Str.Shell_Settings_Nav_Accounts.Text(), line.LinkTo);
        Assert.Null(line.Children);
        line.Action!();
        Assert.Equal(1, opened);
        Assert.Equal(0, _saves);

        Assert.True(Lines().AccountsLine(null, "Manage accounts").Disabled);
    }

    // ---- colours ------------------------------------------------------------------------------

    [Fact]
    public void S3724_a_colour_line_holds_its_colour_and_a_swatch_sets_it_and_saves()
    {
        var set = new List<string>();
        var line = Lines().Colour("Accent", "#3a6ea5", set.Add);

        Assert.False(line.IsSeparator);
        var colour = Assert.IsType<MenuColour>(line.Colour);
        Assert.False(colour.IsFill);
        Assert.Equal("#3A6EA5", colour.Chosen);
        Assert.False(colour.DefaultHolds);
        Assert.Equal(Str.Common_Default.Text(), colour.Options.DefaultLabel);
        Assert.Same(ColorPresets.Surfaces, colour.Options.Presets);

        colour.Choose("#3A6EA5");
        Assert.Empty(set);
        Assert.Equal(0, _saves);

        colour.Choose("#ff0000");
        Assert.Equal(["#FF0000"], set);
        Assert.Equal(1, _saves);

        colour.ChooseDefault();
        Assert.Equal(["#FF0000", string.Empty], set);
        Assert.Equal(2, _saves);
    }

    [Fact]
    public void S3724_a_colour_that_is_required_has_no_default_and_shows_its_first_swatch()
    {
        var set = new List<string>();
        var options = new ColorChoiceOptions { DefaultLabel = null, Presets = ColorPresets.Windows, AllowCustom = false };
        var colour = Lines().Colour("Ink", string.Empty, set.Add, options).Colour!;

        Assert.Equal(HexColor.Normalise(ColorPresets.Windows[0].Hex), colour.Chosen);
        Assert.False(colour.DefaultHolds);
        colour.ChooseDefault();
        Assert.Empty(set);
        Assert.False(colour.Options.AllowCustom);
    }

    [Fact]
    public void S3724_a_fill_keeps_its_opacity_on_a_swatch_and_steps_it_on_its_colour()
    {
        var set = new List<FillChoice>();
        var colour = Lines().Fill("Accent fill", FillChoice.Of("#102030", 0.5), set.Add).Colour!;

        Assert.True(colour.IsFill);
        Assert.True(colour.HasOpacity);
        Assert.Equal(0.5, colour.Opacity);

        colour.Choose("#405060");
        Assert.True(FillChoice.Of("#405060", 0.5).SameAs(set[^1]));

        colour.ChooseOpacity(0.8);
        Assert.True(FillChoice.Of("#102030", 0.8).SameAs(set[^1]));

        colour.ChooseOpacity(0.5);
        Assert.Equal(2, set.Count);
        Assert.Equal(2, _saves);
    }

    [Fact]
    public void S3724_a_fill_offers_default_and_transparent_and_no_opacity_without_a_colour()
    {
        var set = new List<FillChoice>();
        var colour = Lines().Fill("Accent fill", FillChoice.Transparent(), set.Add).Colour!;

        Assert.True(colour.TransparentHolds);
        Assert.False(colour.HasOpacity);
        colour.ChooseTransparent();
        colour.ChooseOpacity(0.3);
        Assert.Empty(set);

        colour.ChooseDefault();
        Assert.Equal(FillKind.Default, Assert.Single(set).Kind);
        Assert.Equal(1, _saves);
    }

    [Fact]
    public void S3724_a_mixed_selection_holds_nothing_and_any_pick_sets_it()
    {
        var set = new List<FillChoice>();
        var colour = MenuColour.OfFill(null, set.Add, new FillChoiceOptions { DefaultLabel = "Default" });

        Assert.True(colour.Mixed);
        Assert.False(colour.DefaultHolds);
        colour.ChooseDefault();
        Assert.Equal(FillKind.Default, Assert.Single(set).Kind);
    }

    // ---- numbers ------------------------------------------------------------------------------

    [Fact]
    public void S3726_a_number_of_few_steps_is_a_choice_of_dots_with_its_value_at_the_end()
    {
        var set = new List<int>();
        var line = Lines().Number(null, "Rows", 4, 1, 10, 1, n => n + " rows", set.Add);

        Assert.Equal("4 rows", line.Value);
        var options = line.Children!;
        Assert.Equal(10, options.Count);
        Assert.Equal("4 rows", Assert.Single(options, o => o.Checked == true).Text);

        options[3].Action!();
        Assert.Empty(set);
        options[6].Action!();
        Assert.Equal([7], set);
        Assert.Equal(1, _saves);
    }

    [Fact]
    public void S3726_a_number_of_many_steps_is_asked_for_refused_outside_its_range_and_rounded_to_a_step()
    {
        var set = new List<int>();
        var line = Lines().Number(null, "Minutes", 30, 5, 600, 5, n => n + " min", set.Add);

        Assert.Null(line.Children);
        Assert.Equal("30 min", line.Value);
        line.Action!();
        var ask = Assert.IsType<WidgetTextAsk>(_asked);
        Assert.Equal("30", ask.Initial);

        // The box says the range in the number's own unit before anything is typed.
        Assert.Contains("5 min", ask.Placeholder);
        Assert.Contains("600 min", ask.Placeholder);
        Assert.Contains("5 min", ask.Refuse("2"));
        Assert.Contains("600 min", ask.Refuse("601"));
        Assert.NotNull(ask.Refuse("many"));
        Assert.Null(ask.Refuse("42"));

        ask.Done("30");
        Assert.Empty(set);
        ask.Done("42");
        Assert.Equal([40], set);
        Assert.Equal(1, _saves);
    }

    [Theory]
    [InlineData("1,5", true, 1.5)]
    [InlineData(" 2.25 ", true, 2.25)]
    [InlineData("2.3", true, 2.25)]
    [InlineData("0.1", false, 0)]
    [InlineData("NaN", false, 0)]
    public void S3726_a_typed_number_reads_a_comma_as_a_point_and_rounds_to_a_step(string typed, bool ok, double expected)
    {
        Assert.Equal(ok, WidgetMenuLines.ParseNumber(typed, 0.5, 10.0, 0.25, out var value));
        if (ok)
        {
            Assert.Equal(expected, value);
        }
    }

    [Fact]
    public void S3726_a_number_without_a_positive_step_is_refused_where_it_is_built()
        => Assert.Throws<ArgumentOutOfRangeException>(() => Lines().Number(null, "Rows", 1, 1, 10, 0, n => n.ToString(System.Globalization.CultureInfo.InvariantCulture), _ => { }));

    // ---- sliders ------------------------------------------------------------------------------

    [Fact]
    public void S3731_a_slider_is_one_line_with_its_value_at_the_end_and_no_submenu()
    {
        var line = Lines().Slider(Glyphs.Volume, "Volume", 40, 0, 100, 5, v => v + " %", _ => { });

        Assert.NotNull(line.Slider);
        Assert.Null(line.Children);
        Assert.Null(line.Action);
        Assert.False(line.IsSeparator);
        Assert.Equal("40 %", line.Value);
        Assert.Equal(40, line.Slider!.Value);
        Assert.Equal((0, 100, 5), (line.Slider.Min, line.Slider.Max, line.Slider.Step));
    }

    [Fact]
    public void S3731_a_drag_previews_and_only_the_release_stores_once()
    {
        var set = new List<int>();
        var slider = Lines().Slider(null, "Volume", 40, 0, 100, 5, v => v + " %", set.Add).Slider!;

        slider.Preview(52);
        slider.Preview(71);
        Assert.Empty(set);
        Assert.Equal("70 %", slider.Format(71));

        Assert.True(slider.Commit(71));
        Assert.Equal([70], set);
        Assert.Equal(1, _saves);
    }

    [Fact]
    public void S3731_releasing_on_the_value_that_holds_stores_nothing()
    {
        var set = new List<int>();
        var slider = Lines().Slider(null, "Volume", 40, 0, 100, 5, v => v + " %", set.Add).Slider!;

        Assert.False(slider.Commit(41));
        Assert.Empty(set);
        Assert.Equal(0, _saves);
    }

    [Theory]
    [InlineData(-20, 0)]
    [InlineData(250, 100)]
    [InlineData(12.4, 10)]
    [InlineData(12.5, 15)]
    [InlineData(double.NaN, 0)]
    public void S3731_a_value_is_kept_in_range_on_the_grain(double value, double snapped)
        => Assert.Equal(snapped, new MenuSlider(0, 0, 100, 5, v => v.ToString(), _ => { }).Snap(value));

    [Fact]
    public void S3731_the_preview_ends_on_the_release_and_on_a_cancel()
    {
        var previews = new List<double?>();
        var slider = new MenuSlider(1.0, 0.5, 2.0, 0.05, v => v.ToString(), _ => { }, previews.Add);

        slider.Preview(1.23);
        slider.Commit(1.23);
        slider.Preview(0.6);
        slider.Cancel();

        Assert.Equal([1.25, null, 0.6, null], previews);
    }

    [Fact]
    public void S3731_a_slider_without_a_positive_step_is_refused_where_it_is_built()
        => Assert.Throws<ArgumentOutOfRangeException>(() => Lines().Slider(null, "Volume", 1, 0, 10, 0, v => v.ToString(), _ => { }));

    [Fact]
    public void S3731_a_fills_opacity_is_a_slider_that_previews_the_colour_and_stores_once()
    {
        var set = new List<FillChoice>();
        var previews = new List<FillChoice?>();
        var colour = MenuColour.OfFill(FillChoice.Of("#102030", 0.5), set.Add, new FillChoiceOptions { DefaultLabel = "Default" }, previews.Add);
        var slider = colour.OpacitySlider(v => v.ToString())!;

        Assert.Equal(0.5, slider.Value);
        Assert.Equal((MenuColour.OpacityMin, MenuColour.OpacityMax), (slider.Min, slider.Max));

        slider.Preview(0.32);
        Assert.True(FillChoice.Of("#102030", 0.3).SameAs(previews[^1]));
        Assert.Empty(set);

        slider.Commit(0.32);
        Assert.True(FillChoice.Of("#102030", 0.3).SameAs(Assert.Single(set)));
        Assert.Null(previews[^1]);
    }

    [Fact]
    public void S3731_a_colour_has_no_opacity_slider_and_a_fill_without_a_colour_stores_none()
    {
        Assert.Null(Lines().Colour("Ink", "#102030", _ => { }).Colour!.OpacitySlider(v => v.ToString()));

        var set = new List<FillChoice>();
        var slider = Lines().Fill("Fill", FillChoice.Default(), set.Add).Colour!.OpacitySlider(v => v.ToString())!;
        slider.Commit(0.4);
        Assert.Empty(set);
    }

    // ---- confirm and note ---------------------------------------------------------------------

    [Fact]
    public void S3727_a_confirmed_line_asks_first_and_only_the_answer_runs_and_saves()
    {
        var cleared = 0;
        var line = Lines().Confirm(null, "Clear history", "Clear the whole history?", "Clear", () => cleared++);

        Assert.True(line.Danger);
        line.Action!();
        var confirm = Assert.IsType<WidgetConfirm>(_confirm);
        Assert.Equal("Clear the whole history?", confirm.Question);
        Assert.Null(confirm.Detail);
        Assert.Equal("Clear", confirm.Accept);
        Assert.Equal(0, cleared);
        Assert.Equal(0, _saves);

        confirm.Done();
        Assert.Equal(1, cleared);
        Assert.Equal(1, _saves);
    }

    [Fact]
    public void S3727_a_confirmed_line_may_say_what_goes_under_its_question()
    {
        Lines().Confirm(null, "Delete this list", "Delete this list?", "Delete", () => { }, detail: "Its 4 habits go too.").Action!();

        var confirm = Assert.IsType<WidgetConfirm>(_confirm);
        Assert.Equal("Delete this list?", confirm.Question);
        Assert.Equal("Its 4 habits go too.", confirm.Detail);
    }

    [Fact]
    public void S3727_with_nobody_to_ask_a_confirmed_line_does_nothing()
    {
        var cleared = 0;
        var lines = new WidgetMenuLines(() => _saves++, _ => { });
        lines.Confirm(null, "Clear history", "Sure?", "Clear", () => cleared++).Action!();

        Assert.Equal(0, cleared);
        Assert.Equal(0, _saves);
    }

    [Fact]
    public void S3728_a_note_is_a_greyed_line_of_words_not_a_divider()
    {
        var note = WidgetMenuLines.Note("Data: Open-Meteo");

        Assert.True(note.Note);
        Assert.True(note.Disabled);
        Assert.Null(note.Action);
        Assert.False(note.IsSeparator);
    }

    // ---- lists --------------------------------------------------------------------------------

    private static MenuList<string> Feeds() => new(s => s.ToUpperInvariant(), "No feeds yet")
    {
        Rename = new MenuListRename<string>((_, _) => false),
        Add = new MenuListAdd<string>("Add a feed", "https://…", typed => typed.StartsWith("https://", StringComparison.Ordinal) ? null : "Not a web address", typed => typed),
    };

    [Fact]
    public void S3729_an_empty_list_says_so_and_still_offers_add()
    {
        var lines = Lines().List(null, "Feeds", new List<string>(), Feeds()).Children!;

        Assert.True(lines[0].Note);
        Assert.Equal("No feeds yet", lines[0].Text);
        Assert.True(lines[1].IsSeparator);
        Assert.Equal(Str.Menu_Add.Text(), lines[2].Text);
    }

    [Fact]
    public void S3729_each_item_is_a_submenu_of_rename_and_remove_with_no_move_lines()
    {
        var items = new List<string> { "a", "b", "c" };
        var lines = Lines().List(null, "Feeds", items, Feeds()).Children!;

        Assert.Equal(["A", "B", "C"], lines.Take(3).Select(l => l.Text));
        var first = lines[0].Children!.Where(l => !l.IsSeparator).ToList();
        Assert.Equal([Str.Menu_Rename.Text(), Str.Common_Remove.Text()], first.Select(l => l.Text));
    }

    [Fact]
    public void S3732_each_item_line_carries_its_place_in_the_list_for_the_drag()
    {
        var items = new List<string> { "a", "b", "c" };
        var lines = Lines().List(null, "Feeds", items, Feeds()).Children!;

        Assert.Equal([0, 1, 2], lines.Take(3).Select(l => l.Order!.Index));
        Assert.All(lines.Take(3), l => Assert.Equal(3, l.Order!.Count));
        Assert.All(lines.Skip(3), l => Assert.Null(l.Order));
    }

    [Fact]
    public void S3732_a_drop_moves_the_item_there_and_saves_once()
    {
        var items = new List<string> { "a", "b", "c", "d" };
        var lines = Lines().List(null, "Feeds", items, Feeds()).Children!;

        lines[0].Order!.Move(0, 2);
        Assert.Equal(["b", "c", "a", "d"], items);
        Assert.Equal(1, _saves);

        // Built before that move: the line still names the item that stood at its place ("d").
        lines[3].Order!.Move(3, 0);
        Assert.Equal(["d", "b", "c", "a"], items);
        Assert.Equal(2, _saves);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(0, -1)]
    [InlineData(2, 3)]
    [InlineData(5, 0)]
    public void S3732_a_drop_on_its_own_place_or_outside_the_list_changes_nothing(int from, int to)
    {
        var items = new List<string> { "a", "b", "c" };
        var lines = Lines().List(null, "Feeds", items, Feeds()).Children!;

        lines[0].Order!.Move(from, to);

        Assert.Equal(["a", "b", "c"], items);
        Assert.Equal(0, _saves);
    }

    [Fact]
    public void S3729_remove_changes_the_list_and_saves()
    {
        var items = new List<string> { "a", "b", "c" };
        var lines = Lines().List(null, "Feeds", items, Feeds()).Children!;

        lines[0].Order!.Move(0, 1);
        Assert.Equal(["b", "a", "c"], items);

        // Built before the move: the line still finds its item.
        lines[2].Children!.Single(l => l.Text == Str.Common_Remove.Text()).Action!();
        Assert.Equal(["b", "a"], items);
        Assert.Equal(2, _saves);
    }

    [Fact]
    public void S3729_rename_starts_from_the_name_and_saves_only_a_change()
    {
        var renamed = new List<(string Item, string Typed)>();
        var list = Feeds() with { Rename = new MenuListRename<string>((item, typed) => { renamed.Add((item, typed)); return typed != "keep"; }) { AllowEmpty = true } };
        Lines().List(null, "Feeds", new List<string> { "a" }, list).Children![0].Children![0].Action!();

        var ask = Assert.IsType<WidgetTextAsk>(_asked);
        Assert.Equal("A", ask.Initial);
        Assert.True(ask.AllowEmpty);
        ask.Done("keep");
        Assert.Equal(0, _saves);
        ask.Done("Alpha");
        Assert.Equal([("a", "keep"), ("a", "Alpha")], renamed);
        Assert.Equal(1, _saves);
    }

    [Fact]
    public void S3729_add_asks_checks_and_appends_and_is_greyed_at_the_limit()
    {
        var items = new List<string> { "https://a.example/" };
        var add = Lines().List(null, "Feeds", items, Feeds()).Children!.Single(l => l.Text == Str.Menu_Add.Text());

        add.Action!();
        var ask = Assert.IsType<WidgetTextAsk>(_asked);
        Assert.NotNull(ask.Refuse("feed"));
        Assert.Null(ask.Refuse("https://b.example/"));
        ask.Done("https://b.example/");
        Assert.Equal(["https://a.example/", "https://b.example/"], items);
        Assert.Equal(1, _saves);

        var full = Lines().List(null, "Feeds", items, Feeds() with { Max = 2 }).Children!.Single(l => l.Text == Str.Menu_Add.Text());
        Assert.True(full.Disabled);
    }

    [Fact]
    public void S3729_the_callers_add_lines_follow_the_items_and_an_unordered_list_is_not_dragged()
    {
        var pick = new ItemMenuEntry(null, "Pick a file…", () => { });
        var list = new MenuList<string>(s => s, "None") { AddLines = [pick], Reorder = false };
        var lines = Lines().List(null, "Files", new List<string> { "x" }, list).Children!;

        Assert.Equal("Pick a file…", lines[^1].Text);
        Assert.Null(lines[0].Order);
    }

    // ---- folders ------------------------------------------------------------------------------

    [Theory]
    [InlineData(@"C:\Users\Me\Pictures", "Pictures")]
    [InlineData(@"C:\Users\Me\Pictures\", "Pictures")]
    [InlineData(@"D:\", @"D:\")]
    [InlineData("", null)]
    [InlineData("   ", null)]
    public void S3730_a_folder_line_shows_the_folders_name(string path, string? expected)
        => Assert.Equal(expected, WidgetMenuLines.FolderName(path));

    [Fact]
    public void S3730_a_folder_line_carries_the_name_and_opens_the_picker_on_a_click()
    {
        var line = Lines().PickFolder(null, "Folder", @"C:\Photos", _ => { });

        Assert.Equal("Photos", line.Value);
        Assert.NotNull(line.Action);
        Assert.Null(line.Children);
    }

    private sealed class NoLines : IWidgetModule
    {
        public Themia.Sdk.WidgetDescriptor Descriptor => throw new NotSupportedException();

        public Microsoft.UI.Xaml.FrameworkElement CreateView(IWidgetHost host) => throw new NotSupportedException();
    }
}
