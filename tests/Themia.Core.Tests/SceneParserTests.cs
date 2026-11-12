using Themia.Core.Model;
using Themia.Core.Scenes;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-1075] The store scenes file: read whole or refused at the first problem, by its JSON path.</summary>
public sealed class SceneParserTests
{
    private const string Day = """
        {
          "screen": { "width": 1920, "height": 1080 },
          "scenes": [
            {
              "name": "day",
              "theme": "light",
              "design": "preset=Glass,radius=8",
              "wallpaper": "wallpapers/dusk.jpg",
              "widgets": [
                { "kind": "clock", "at": [40, 40, 480, 240], "state": "sample", "style": "flip",
                  "header": "hidden", "background": "transparent", "items": "#2A3248@60%", "itemText": "#ffffff",
                  "scale": 1.5, "align": "center", "label": "Zürich", "bare": true,
                  "config": { "showDate": false }, "selected": true },
                { "kind": "weatherNow", "at": [520, 40, 300, 200] }
              ],
              "edit": { "open": "appearance", "grid": true }
            },
            { "name": "day-paper", "like": "day", "design": "preset=Paper" }
          ]
        }
        """;

    private static SceneFile Parsed(string json)
    {
        var file = SceneParser.Parse(json, out var refusal);
        Assert.True(file is not null, refusal?.ToString());
        return file!;
    }

    private static SceneRefusal Refused(string json)
    {
        var file = SceneParser.Parse(json, out var refusal);
        Assert.Null(file);
        Assert.NotNull(refusal);
        return refusal!;
    }

    /// <summary>One scene of <paramref name="widget"/>, in edit mode when <paramref name="edit"/>.</summary>
    private static string One(string widget, bool edit = false)
        => $$"""{ "scenes": [ { "name": "a", "widgets": [ {{widget}} ]{{(edit ? ", \"edit\": {}" : string.Empty)}} } ] }""";

    [Fact]
    public void S1075_reads_every_property_of_a_scene_and_its_widgets()
    {
        var file = Parsed(Day);

        Assert.Equal(new SceneScreen(1920, 1080), file.Screen);
        var day = file.Find("day")!;
        Assert.Equal(AppTheme.Light, day.Theme);
        Assert.Equal("preset=Glass,radius=8", day.Design);
        Assert.Equal("wallpapers/dusk.jpg", day.Wallpaper);
        Assert.Equal(new SceneEdit(Bar: true, Open: "appearance", Add: false, Grid: true), day.Edit);

        var clock = day.Widgets[0];
        Assert.Equal(new WidgetId("clock"), clock.Kind);
        Assert.Equal(new SceneRect(40, 40, 480, 240), clock.At);
        Assert.Equal("sample", clock.State);
        Assert.Equal("flip", clock.Style);
        Assert.Equal(HeaderPlacement.Hidden, clock.Header);
        Assert.Equal(FillKind.Transparent, clock.Background!.Kind);
        Assert.Equal("#2A3248", clock.Items!.Colour);
        Assert.Equal(0.6, clock.Items.Opacity, 3);
        Assert.Equal("#FFFFFF", clock.ItemText);
        Assert.Equal(1.5, clock.Scale);
        Assert.Equal(ContentAlign.Center, clock.Align);
        Assert.Equal("Zürich", clock.Label);
        Assert.True(clock.Bare);
        Assert.True(clock.Selected);
        Assert.Contains("showDate", clock.Config, StringComparison.Ordinal);
        Assert.Equal("$.scenes[0].widgets[0]", clock.Path);

        var weather = day.Widgets[1];
        Assert.Null(weather.State);
        Assert.Null(weather.Header);
        Assert.False(weather.Bare);
        Assert.False(weather.Selected);
    }

    [Fact]
    public void S1075_a_scene_without_a_screen_theme_or_design_is_full_hd_dark_and_default()
    {
        var file = Parsed(One("""{ "kind": "clock", "at": [0, 0, 1920, 1080] }"""));

        Assert.Equal(SceneScreen.Default, file.Screen);
        var scene = file.Scenes[0];
        Assert.Equal(AppTheme.Dark, scene.Theme);
        Assert.Equal(string.Empty, scene.Design);
        Assert.Null(scene.Wallpaper);
        Assert.Null(scene.Edit);
    }

    [Fact]
    public void S1075_a_like_scene_starts_from_the_other_and_replaces_what_it_sets()
    {
        var file = Parsed(Day);
        var day = file.Find("day")!;
        var paper = file.Find("day-paper")!;

        Assert.Equal("preset=Paper", paper.Design);
        Assert.Equal(day.Theme, paper.Theme);
        Assert.Equal(day.Wallpaper, paper.Wallpaper);
        Assert.Same(day.Widgets, paper.Widgets);
        Assert.Equal(day.Edit, paper.Edit);
    }

    [Fact]
    public void S1075_a_like_scene_may_follow_one_written_after_it_and_a_chain()
    {
        var file = Parsed("""
            { "scenes": [
              { "name": "c", "like": "b", "theme": "light" },
              { "name": "b", "like": "a", "design": "radius=2" },
              { "name": "a", "widgets": [], "wallpaper": "w.png" }
            ] }
            """);

        var c = file.Find("c")!;
        Assert.Equal(AppTheme.Light, c.Theme);
        Assert.Equal("radius=2", c.Design);
        Assert.Equal("w.png", c.Wallpaper);
        Assert.Equal(["c", "b", "a"], file.Scenes.Select(s => s.Name));
    }

    [Fact]
    public void S1075_a_like_scene_drops_the_wallpaper_or_edit_it_would_take_with_null()
    {
        var file = Parsed("""
            { "scenes": [
              { "name": "a", "widgets": [], "wallpaper": "w.png", "edit": { "add": true } },
              { "name": "b", "like": "a", "wallpaper": null, "edit": null }
            ] }
            """);

        Assert.Null(file.Find("b")!.Wallpaper);
        Assert.Null(file.Find("b")!.Edit);
    }

    [Fact]
    public void S1075_like_in_a_circle_or_to_no_scene_is_refused()
    {
        var circle = Refused("""
            { "scenes": [ { "name": "a", "like": "b" }, { "name": "b", "like": "a" } ] }
            """);
        Assert.Equal("$.scenes[0].like", circle.Path);
        Assert.Contains("a > b > a", circle.Problem, StringComparison.Ordinal);
        Assert.Contains("circle", circle.Problem, StringComparison.Ordinal);

        var self = Refused("""{ "scenes": [ { "name": "a", "like": "a" } ] }""");
        Assert.Contains("circle", self.Problem, StringComparison.Ordinal);

        var missing = Refused("""{ "scenes": [ { "name": "a", "like": "nope" } ] }""");
        Assert.Equal("$.scenes[0].like", missing.Path);
        Assert.Contains("nope", missing.Problem, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{ "scenes": [ { "name": "a", "widgets": [] } ], "screens": {} }""", "$.screens")]
    [InlineData("""{ "scenes": [ { "name": "a", "widgets": [], "wallpapr": "x.png" } ] }""", "$.scenes[0].wallpapr")]
    [InlineData("""{ "scenes": [ { "name": "a", "widgets": [ { "kind": "clock", "at": [0, 0, 10, 10], "stlye": "flip" } ] } ] }""", "$.scenes[0].widgets[0].stlye")]
    [InlineData("""{ "scenes": [ { "name": "a", "widgets": [], "edit": { "opne": "x" } } ] }""", "$.scenes[0].edit.opne")]
    [InlineData("""{ "screen": { "width": 800, "heigth": 600 }, "scenes": [ { "name": "a", "widgets": [] } ] }""", "$.screen.heigth")]
    public void S1075_an_unknown_property_is_refused_not_ignored(string json, string path)
    {
        var refusal = Refused(json);

        Assert.Equal(path, refusal.Path);
        Assert.Contains("unknown property", refusal.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public void S1075_a_property_written_twice_is_refused()
    {
        var refusal = Refused("""{ "scenes": [ { "name": "a", "name": "b", "widgets": [] } ] }""");

        Assert.Equal("$.scenes[0].name", refusal.Path);
        Assert.Contains("twice", refusal.Problem, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Day")]
    [InlineData("day one")]
    [InlineData("day_one")]
    [InlineData("")]
    public void S1075_a_name_is_lower_case_letters_digits_and_dashes(string name)
    {
        var refusal = Refused($$"""{ "scenes": [ { "name": "{{name}}", "widgets": [] } ] }""");

        Assert.Equal("$.scenes[0].name", refusal.Path);
    }

    [Fact]
    public void S1075_names_are_unique()
    {
        var refusal = Refused("""{ "scenes": [ { "name": "a", "widgets": [] }, { "name": "a", "widgets": [] } ] }""");

        Assert.Equal("$.scenes[1].name", refusal.Path);
    }

    [Theory]
    [InlineData("[0, 0, 10]", "$.scenes[0].widgets[0].at")]
    [InlineData("[0, 0, 10, \"10\"]", "$.scenes[0].widgets[0].at[3]")]
    [InlineData("[0, 0, 0, 10]", "$.scenes[0].widgets[0].at")]
    [InlineData("[-1, 0, 10, 10]", "$.scenes[0].widgets[0].at")]
    [InlineData("[1900, 0, 21, 10]", "$.scenes[0].widgets[0].at")]
    [InlineData("[0, 1000, 10, 81]", "$.scenes[0].widgets[0].at")]
    [InlineData("[0, 0, 1e999, 10]", "$.scenes[0].widgets[0].at[2]")]
    public void S1075_a_rectangle_is_four_finite_numbers_inside_the_display(string at, string path)
    {
        var refusal = Refused(One($$"""{ "kind": "clock", "at": {{at}} }"""));

        Assert.Equal(path, refusal.Path);
    }

    [Fact]
    public void S1075_a_rectangle_may_touch_the_display_edges()
    {
        var file = Parsed(One("""{ "kind": "clock", "at": [1520, 880, 400, 200] }"""));

        Assert.Equal(new SceneRect(1520, 880, 400, 200), file.Scenes[0].Widgets[0].At);
    }

    [Theory]
    [InlineData("""{ "screen": { "width": 100 }, "scenes": [ { "name": "a", "widgets": [] } ] }""", "$.screen.width")]
    [InlineData("""{ "screen": { "height": 1080.5 }, "scenes": [ { "name": "a", "widgets": [] } ] }""", "$.screen.height")]
    [InlineData("""{ "screen": { "width": 99999 }, "scenes": [ { "name": "a", "widgets": [] } ] }""", "$.screen.width")]
    [InlineData("""{ "screen": [1920, 1080], "scenes": [ { "name": "a", "widgets": [] } ] }""", "$.screen")]
    public void S1075_the_display_is_a_whole_sane_size(string json, string path)
    {
        Assert.Equal(path, Refused(json).Path);
    }

    [Theory]
    [InlineData("\"header\": \"middle\"", "$.scenes[0].widgets[0].header")]
    [InlineData("\"header\": \"2\"", "$.scenes[0].widgets[0].header")]
    [InlineData("\"align\": \"justify\"", "$.scenes[0].widgets[0].align")]
    [InlineData("\"background\": \"blue\"", "$.scenes[0].widgets[0].background")]
    [InlineData("\"items\": \"#12345\"", "$.scenes[0].widgets[0].items")]
    [InlineData("\"itemText\": \"white\"", "$.scenes[0].widgets[0].itemText")]
    [InlineData("\"scale\": 3", "$.scenes[0].widgets[0].scale")]
    [InlineData("\"scale\": \"1.5\"", "$.scenes[0].widgets[0].scale")]
    [InlineData("\"bare\": \"yes\"", "$.scenes[0].widgets[0].bare")]
    [InlineData("\"config\": \"showDate=false\"", "$.scenes[0].widgets[0].config")]
    [InlineData("\"state\": \"\"", "$.scenes[0].widgets[0].state")]
    [InlineData("\"label\": 4", "$.scenes[0].widgets[0].label")]
    public void S1075_a_bad_widget_value_is_refused_by_its_path(string property, string path)
    {
        var json = One($$"""{ "kind": "clock", "at": [0, 0, 10, 10], {{property}} }""");

        Assert.Equal(path, Refused(json).Path);
    }

    [Theory]
    [InlineData("\"theme\": \"system\"", "$.scenes[0].theme")]
    [InlineData("\"design\": \"preset=Neon\"", "$.scenes[0].design")]
    [InlineData("\"design\": \"radius=round\"", "$.scenes[0].design")]
    [InlineData("\"design\": \"accent=red\"", "$.scenes[0].design")]
    [InlineData("\"wallpaper\": \"C:\\\\walls\\\\a.jpg\"", "$.scenes[0].wallpaper")]
    [InlineData("\"edit\": { \"bar\": 1 }", "$.scenes[0].edit.bar")]
    [InlineData("\"edit\": { \"open\": \"\" }", "$.scenes[0].edit.open")]
    public void S1075_a_bad_scene_value_is_refused_by_its_path(string property, string path)
    {
        var refusal = Refused($$"""{ "scenes": [ { "name": "a", "widgets": [], {{property}} } ] }""");

        Assert.Equal(path, refusal.Path);
    }

    [Fact]
    public void S1075_a_kind_is_a_widget_id_in_form_and_a_widget_needs_one()
    {
        Assert.Equal("$.scenes[0].widgets[0].kind", Refused(One("""{ "kind": "Clock Face", "at": [0, 0, 10, 10] }""")).Path);
        Assert.Equal("$.scenes[0].widgets[0]", Refused(One("""{ "at": [0, 0, 10, 10] }""")).Path);
        Assert.Equal("$.scenes[0].widgets[0]", Refused(One("""{ "kind": "clock" }""")).Path);
    }

    [Fact]
    public void S1075_a_scene_with_neither_widgets_nor_like_is_refused()
    {
        Assert.Equal("$.scenes[0]", Refused("""{ "scenes": [ { "name": "a" } ] }""").Path);
        Assert.Equal("$.scenes", Refused("""{ "scenes": [] }""").Path);
        Assert.Equal("$", Refused("""{ }""").Path);
    }

    [Fact]
    public void S1075_a_selection_needs_edit_mode_and_an_open_submenu_a_selection()
    {
        var selected = Refused(One("""{ "kind": "clock", "at": [0, 0, 10, 10], "selected": true }"""));
        Assert.Equal("$.scenes[0]", selected.Path);
        Assert.Contains("$.scenes[0].widgets[0]", selected.Problem, StringComparison.Ordinal);

        var open = Refused("""{ "scenes": [ { "name": "a", "widgets": [ { "kind": "clock", "at": [0, 0, 10, 10] } ], "edit": { "open": "appearance" } } ] }""");
        Assert.Equal("$.scenes[0]", open.Path);

        Assert.True(Parsed(One("""{ "kind": "clock", "at": [0, 0, 10, 10], "selected": true }""", edit: true)).Scenes[0].Widgets[0].Selected);
    }

    [Fact]
    public void S1075_a_like_scene_that_drops_edit_from_a_selection_is_refused()
    {
        var refusal = Refused("""
            { "scenes": [
              { "name": "a", "widgets": [ { "kind": "clock", "at": [0, 0, 10, 10], "selected": true } ], "edit": {} },
              { "name": "b", "like": "a", "edit": null }
            ] }
            """);

        Assert.Equal("$.scenes[1]", refusal.Path);
    }

    [Fact]
    public void S1075_text_that_is_not_json_is_refused_at_the_root()
    {
        var refusal = Refused("{ \"scenes\": [ ");

        Assert.Equal("$", refusal.Path);
        Assert.Contains("not JSON", refusal.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public void S1075_edit_defaults_to_the_bar_alone()
    {
        var scene = Parsed(One("""{ "kind": "clock", "at": [0, 0, 10, 10] }""", edit: true)).Scenes[0];

        Assert.Equal(new SceneEdit(Bar: true, Open: null, Add: false, Grid: false), scene.Edit);
    }

    [Fact]
    public void S6480_design_text_applies_in_order_and_names_what_it_could_not_read()
    {
        var design = new DesignState();

        var refused = DesignText.Apply(design, "preset=Paper,radius=3,gap=12,accent=#00aaff,itemText=,border=wavy,shade=1,accent=blue");

        Assert.Equal(3, design.CornerRadiusPx);
        Assert.Equal(12, design.WidgetGapPx);
        Assert.Equal("#00AAFF", design.PrimaryColor);
        Assert.Equal(string.Empty, design.ItemTextColor);
        Assert.Equal(["border=wavy", "shade=1", "accent=blue"], refused);
    }

    [Fact]
    public void S6480_design_text_reads_fills_with_their_own_opacity()
    {
        Assert.Equal(FillKind.Transparent, DesignText.Fill("none")!.Kind);
        Assert.Equal(FillKind.Default, DesignText.Fill("DEFAULT")!.Kind);
        Assert.Equal(0.25, DesignText.Fill("#102030@25%")!.Opacity, 3);
        Assert.Equal(0.5, DesignText.Fill("#102030", 0.5)!.Opacity, 3);
        Assert.Null(DesignText.Fill("#1020"));
        Assert.Null(DesignText.Fill(null));
    }
}
