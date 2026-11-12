using Themia.Core.Model;
using Themia.Core.Scenes;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Tests;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-1076] [S-1078] A scene's widgets against every installed widget, and the repository's own
/// <c>store/scenes.json</c>, so the checked-in scenes cannot rot.
/// </summary>
public sealed class SceneWidgetsTests
{
    private static readonly SceneScreen Screen = new(1920, 1080);

    private static SceneWidget Clock(string? config = null, string? style = null) => new("$.w", "clock", new WidgetId("clock"), new SceneRect(192, 108, 480, 216), Style: style, Config: config);

    [Fact]
    public void S1076_a_widget_is_its_kind_where_the_scene_puts_it_with_what_the_scene_names()
    {
        var widget = Clock("""{ "showDate": false }""", "flip") with
        {
            Bare = true,
            Header = HeaderPlacement.Bottom,
            Items = FillChoice.Of("#2A3248", 0.6),
            ItemText = "#FFFFFF",
            Scale = 1.5,
            Align = ContentAlign.Right,
            Label = "Zürich",
        };

        var model = SceneWidgets.Build(widget, Screen, out var refusal);

        Assert.Null(refusal);
        Assert.NotNull(model);
        Assert.Equal(new WidgetId("clock"), ModuleCatalog.IdOf(model!.Config));
        Assert.Equal(0.1, model.X, 9);
        Assert.Equal(0.1, model.Y, 9);
        Assert.Equal(0.25, model.W, 9);
        Assert.Equal(0.2, model.H, 9);

        // Bare is the shorthand, and what the widget names itself is over it.
        Assert.Equal(FillKind.Transparent, model.Background.Kind);
        Assert.Equal(HeaderPlacement.Bottom, model.Header);
        Assert.Equal("#2A3248", model.ItemBackground.Colour);
        Assert.Equal("#FFFFFF", model.ItemTextColor);
        Assert.Equal(1.5, model.Scale);
        Assert.Equal(ContentAlign.Right, model.Align);
        Assert.Equal("Zürich", model.Label);
        Assert.Equal(WidgetStyles.Find(new WidgetId("clock"), "flip")?.Value, WidgetStyles.Current(model.Config));
        Assert.Contains("\"showDate\": false", System.Text.Json.JsonSerializer.Serialize(model.Config, StateJson.WidgetConfig), StringComparison.Ordinal);
    }

    [Fact]
    public void S1076_bare_alone_is_a_transparent_card_without_a_header()
    {
        var model = SceneWidgets.Build(Clock() with { Bare = true }, Screen, out _)!;

        Assert.Equal(FillKind.Transparent, model.Background.Kind);
        Assert.Equal(HeaderPlacement.Hidden, model.Header);
    }

    [Fact]
    public void S1076_a_kind_this_build_does_not_have_is_refused()
    {
        var widget = Clock() with { Kind = new WidgetId("sundial") };

        Assert.Null(SceneWidgets.Build(widget, Screen, out var refusal));
        Assert.Equal("$.w.kind", refusal!.Path);
    }

    [Fact]
    public void S1076_a_style_the_kind_does_not_have_is_refused_naming_its_styles()
    {
        Assert.Null(SceneWidgets.Build(Clock(style: "sundial"), Screen, out var refusal));
        Assert.Equal("$.w.style", refusal!.Path);
        Assert.Contains("flip", refusal.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public void S1076_a_misspelt_config_property_is_refused_not_dropped()
    {
        Assert.Null(SceneWidgets.Build(Clock("""{ "showDat": false }"""), Screen, out var refusal));
        Assert.Equal("$.w.config.showDat", refusal!.Path);
    }

    [Fact]
    public void S1076_a_config_value_of_the_wrong_type_is_refused()
    {
        Assert.Null(SceneWidgets.Build(Clock("""{ "showDate": "no" }"""), Screen, out var refusal));
        Assert.Equal("$.w.config", refusal!.Path);
    }

    [Fact]
    public void S1078_S2210_every_checked_in_scene_parses_and_every_widget_and_wallpaper_is_one_this_build_has()
    {
        var path = RepoFiles.At("store", "scenes.json");
        var file = SceneParser.Parse(File.ReadAllText(path), out var refusal);
        Assert.True(file is not null, $"store/scenes.json: {refusal}");

        var problems = file!.Scenes.SelectMany(scene => SceneWidgets.Check(scene, file.Screen).Select(r => $"{scene.Name}: {r}")).ToList();
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));

        foreach (var scene in file.Scenes.Where(s => s.WallpaperFile is not null))
        {
            Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(path)!, scene.WallpaperFile!)), $"{scene.Name}: no wallpaper {scene.WallpaperFile}");
        }

        // [S-2210] A sample wallpaper names a picture this build paints.
        foreach (var scene in file.Scenes.Where(s => s.SampleWallpaper is not null))
        {
            Assert.True(
                Themia.Sdk.Samples.SamplePictures.WallpaperSlugs.Contains(scene.SampleWallpaper!, StringComparer.Ordinal),
                $"{scene.Name}: no sample picture \"{scene.SampleWallpaper}\" (has: {string.Join(", ", Themia.Sdk.Samples.SamplePictures.WallpaperSlugs)})");
        }
    }
}
