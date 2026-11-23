using System.Text.Json;
using System.Text.Json.Nodes;
using Themia.Core.Geometry;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Shell;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.QuickToggle.Tests;

using QuickToggle = Themia.Core.Model.QuickToggle;

/// <summary>
/// [S-16700]..[S-16704] The quick setting: one kind whose config targets one of the thirteen settings, offered everywhere as
/// a widget per target (the presets of [S-16680]..[S-16686] over a real kind), and each placed old toggle folded into it.
/// </summary>
public sealed class QuickToggleTests
{
    private static readonly WidgetId Kind = new("quickToggle");

    /// <summary>Each target's key, as its preset, its config and the removed kind (key + "Toggle") have it.</summary>
    private static readonly string[] Keys =
    [
        "wifi", "bluetooth", "airplaneMode", "sound", "microphone", "spatialSound", "monoAudio",
        "nightLight", "colorFilters", "rotationLock", "energySaver", "doNotDisturb", "gameMode",
    ];

    private static AddPage Page => AddPage.Of([.. ModuleCatalog.Widgets.Select(w => w.Id)], ReadyMadeGroups.Offered());

    // ---- [S-16700] one kind, its target a setting -----------------------------------------------------------------

    [Fact]
    public void S16700_one_kind_wifi_tile_at_first_its_target_and_style_kept_by_clone()
    {
        var config = Assert.IsType<QuickToggleConfig>(WidgetConfig.CreateDefault(Kind));
        Assert.Equal((QuickToggle.Wifi, QuickToggleStyle.Tile), (config.Target, config.Style));

        config.Target = QuickToggle.DoNotDisturb;
        config.Style = QuickToggleStyle.Switch;
        var copy = Assert.IsType<QuickToggleConfig>(config.Clone());
        Assert.Equal((QuickToggle.DoNotDisturb, QuickToggleStyle.Switch), (copy.Target, copy.Style));

        // No kind of a toggle is left: the thirteen are this one.
        Assert.All(Keys, key => Assert.False(ModuleCatalog.TryGet(new WidgetId(key + "Toggle"), out _), key));
        Assert.Single(ModuleCatalog.Widgets, w => w.ConfigType == typeof(QuickToggleConfig));
    }

    [Fact]
    public void S16700_the_target_is_written_by_its_camel_case_name_and_reads_back()
    {
        foreach (var target in QuickToggleRules.All)
        {
            var config = new QuickToggleConfig { Target = target, Style = QuickToggleStyle.Round };
            var json = JsonSerializer.Serialize<WidgetConfig>(config, StateJson.WidgetConfig);
            Assert.Equal(QuickToggleRules.Key(target), (string)JsonNode.Parse(json)!["target"]!);

            var back = Assert.IsType<QuickToggleConfig>(JsonSerializer.Deserialize(json, StateJson.WidgetConfig));
            Assert.Equal((target, QuickToggleStyle.Round, Kind), (back.Target, back.Style, back.Kind));
        }

        Assert.Equal(Keys.Order(), QuickToggleRules.All.Select(QuickToggleRules.Key).Order());
    }

    [Fact]
    public void S16700_a_target_this_build_does_not_know_is_repaired_to_wifi_and_said()
    {
        var config = Assert.IsType<QuickToggleConfig>(JsonSerializer.Deserialize("""{"kind":"quickToggle","target":"hotspot"}""", StateJson.WidgetConfig));

        Assert.Single(config.Repair());
        Assert.Equal(QuickToggle.Wifi, config.Target);
        Assert.Empty(config.Repair());
    }

    [Fact]
    public void S16700_its_toggles_line_lists_every_target_by_its_name_and_a_pick_sets_it()
    {
        var saves = 0;
        var config = new QuickToggleConfig();
        var menu = new WidgetMenuLines(() => saves++);
        new QuickToggleModule().MenuLines(menu, config);

        var line = Assert.IsType<Themia.Core.Settings.SettingsChoice>(Assert.Single(menu.Lines));
        Assert.Equal(QuickToggleStr.Toggles_Target_Label.Text(), line.Text);
        Assert.Equal(13, line.Options.Count);
        Assert.Equal(QuickToggleRules.Name(QuickToggle.Wifi), line.Value);
        Assert.All(QuickToggleRules.All, t => Assert.Contains(line.Options, o => o.Text == QuickToggleRules.Name(t)));

        line.Options.Single(o => o.Text == QuickToggleRules.Name(QuickToggle.NightLight)).Pick();
        Assert.Equal((QuickToggle.NightLight, 1), (config.Target, saves));
    }

    [Fact]
    public void S16701_every_target_has_its_name_glyph_and_unavailable_reason_in_one_row()
    {
        Assert.Equal(Enum.GetValues<QuickToggle>().Order(), QuickToggleRules.All.Order());
        Assert.Equal(13, QuickToggleRules.Targets.Select(t => t.Name).Distinct().Count());
        Assert.All(QuickToggleRules.Targets, t => Assert.False(string.IsNullOrEmpty(t.Glyph)));
        Assert.All(QuickToggleRules.Targets, t => Assert.NotNull(QuickToggleRules.Reason(Locale.En, t.Unavailable)));

        // [S-3902] Sound and microphone are struck through while muted; the rest wear one glyph.
        Assert.Equal([QuickToggle.Sound, QuickToggle.Microphone], QuickToggleRules.Targets.Where(t => t.Glyph != t.OffGlyph).Select(t => t.Toggle));
    }

    // ---- [S-16703] offered as a widget per target --------------------------------------------------------------------

    [Fact]
    public void S16703_the_quick_settings_subject_lists_a_tile_per_target_in_its_runs_and_none_for_the_kind()
    {
        var tiles = Page.Sections(AddFolderId.QuickSettings).Where(s => s.Kind == AddSectionKind.Widgets).SelectMany(s => s.Tiles).OfType<AddWidgetTile>().ToList();

        Assert.Equal(Keys.Select(k => $"kind:quickToggle/{k}"), tiles.Select(t => t.Key));
        Assert.Equal(13, Page.Rail.Single(r => r.Id == AddFolderId.QuickSettings).Count);
        Assert.Equal(QuickToggleRules.Glyph(QuickToggle.Wifi), Page.Rail.Single(r => r.Id == AddFolderId.QuickSettings).Glyph);
        Assert.DoesNotContain(Page.Homes, h => h.Kind == Kind && h.Preset is null);
    }

    [Fact]
    public void S16703_each_preset_is_named_and_drawn_as_its_target_never_as_another_widget()
    {
        foreach (var locale in Enum.GetValues<Locale>())
        {
            var labels = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pick in ModuleCatalog.Of(Kind).Picks)
            {
                var target = ((QuickToggleConfig)pick.CreateConfig()).Target;
                var label = pick.Label.Text(locale);
                Assert.Equal(QuickToggleRules.Key(target), pick.Preset);
                Assert.Equal(QuickToggleRules.Glyph(target), pick.Glyph);
                Assert.False(string.IsNullOrWhiteSpace(pick.Describe(locale)));
                Assert.NotEqual(QuickToggleRules.Name(locale, target), label);
                Assert.True(labels.Add(label), $"{locale}: {label} twice");
            }
        }
    }

    [Fact]
    public void S16703_placing_a_preset_makes_the_kind_with_its_target_at_the_tiles_natural_size()
    {
        foreach (var key in Keys)
        {
            Assert.True(WidgetPick.TryParse("quickToggle/" + key, out var pick));
            var widget = WidgetInstance.Create(pick, new FracRect(0, 0, 0.1, 0.1));
            Assert.Equal(Kind, widget.Kind);
            Assert.Equal(key, QuickToggleRules.Key(((QuickToggleConfig)widget.Config).Target));
            Assert.Equal(pick, WidgetPick.Of(widget.Config));
            Assert.Equal(ModuleCatalog.Of(Kind).Natural, pick.Natural);

            var payload = PlacementPayload.ForNew(pick, pick.Natural.Width, pick.Natural.Height);
            var placed = Assert.Single(payload.Plan(0.5, 0.5, new ScreenMetrics(1920, 1080)).Added);
            Assert.Equal(pick, WidgetPick.Of(placed.Config));
            Assert.Equal(pick.Name, Assert.Single(payload.Outlines(0.5, 0.5, new ScreenMetrics(1920, 1080))).Config is { } ghost ? WidgetPick.Of(ghost).Name : null);
        }
    }

    [Fact]
    public void S16703_a_placed_toggle_is_named_as_its_target_on_the_hint_and_the_selections_entry()
    {
        static (AppState State, ViewState View, WidgetSelection Selection) Selected(params string[] keys)
        {
            var view = ViewState.Create("v");
            foreach (var key in keys)
            {
                WidgetPick.TryParse("quickToggle/" + key, out var pick);
                view.Widgets.Add(WidgetInstance.Create(pick, new FracRect(0.1 * view.Widgets.Count, 0, 0.05, 0.05)));
            }

            var selection = new WidgetSelection();
            selection.Replace(view.Id, [.. view.Widgets.Select(w => w.Id)]);
            return (new AppState { Views = [view] }, view, selection);
        }

        string Words(params string[] keys)
        {
            var (state, view, selection) = Selected(keys);
            return SelectionPage.Words(QuickSet.For(state, selection, view.Id), id => ModuleCatalog.Of(id).Name)!;
        }

        var wifi = QuickToggleStr.Toggles_Wifi_Label.Text();
        Assert.Equal(wifi, Words("wifi"));
        Assert.Equal(Str.EditBar_Selection_Same.Format(("count", LocaleFormat.Integer(2)), ("kind", wifi)), Words("wifi", "wifi"));
        Assert.Equal(Str.Shell_Views_WidgetCount.Plural(2), Words("wifi", "bluetooth"));

        var (_, one, _) = Selected("gameMode");
        var payload = PlacementPayload.FromView(one, [one.Widgets[0].Id], PlacementMode.Copy)!;
        Assert.Equal(QuickToggleStr.Toggles_GameMode_Label.Text(), payload.SinglePick!.Value.Name);
    }

    [Fact]
    public void S16703_the_gallery_shows_each_target_at_its_size_and_its_least_as_pieces_of_their_own()
    {
        var quick = WidgetGallery.Of(Page).Single(c => c.Id == AddFolderId.QuickSettings);
        var toggles = quick.Pieces.Where(p => p.Kind == Kind).ToList();

        // [S-16726] [S-16731] In its first style at both sizes; in each other style once, the same targets in the same order.
        var first = toggles.Where(p => p.Style is null).ToList();
        Assert.Equal(Keys, first.Where(p => !p.Least).Select(p => p.Preset));
        Assert.Equal(Keys, first.Where(p => p.Least).Select(p => p.Preset));
        foreach (var style in toggles.Where(p => p.Style is not null).GroupBy(p => p.Style))
        {
            Assert.Equal(Keys, style.Select(p => p.Preset));
        }

        Assert.All(toggles, p => Assert.Equal(new WidgetPick(Kind, p.Preset), p.Pick));
    }

    [Fact]
    public void S16703_a_search_finds_a_target_as_its_own_widget_on_its_words()
    {
        AddWidgetTile[] Found(string query) => [.. Page.Find(query).SelectMany(s => s.Tiles).OfType<AddWidgetTile>().Where(t => t.Kind == Kind)];

        Assert.Equal(["wifi"], Found("wlan").Select(t => t.Preset));
        Assert.Equal(["doNotDisturb"], Found("focus assist").Select(t => t.Preset));
        Assert.Contains(Found("bluetooth"), t => t.Preset == "bluetooth");
        Assert.DoesNotContain(Found("bluetooth"), t => t.Preset == "wifi");
        Assert.Equal(13, Found("quick settings").Length);
    }

    [Fact]
    public void S16703_the_quick_settings_group_and_the_first_run_desktop_place_their_targets()
    {
        var group = ReadyMadeGroups.Build(ReadyMadeGroups.QuickSettings, new ScreenMetrics(1920, 1080))!;
        Assert.Equal(["wifi", "bluetooth", "doNotDisturb", "nightLight"],
            group.Where(w => w.Kind == Kind).Select(w => QuickToggleRules.Key(((QuickToggleConfig)w.Config).Target)));
        Assert.Contains(ReadyMadeGroups.QuickSettings, ReadyMadeGroups.Offered());

        var home = FirstRunDesktop.View("Home", FirstRunDesktop.Home);
        Assert.Equal(["doNotDisturb", "colorFilters", "nightLight", "airplaneMode", "bluetooth", "wifi"],
            home.Widgets.Where(w => w.Kind == Kind).Select(w => QuickToggleRules.Key(((QuickToggleConfig)w.Config).Target)));
    }

    // ---- [S-16704] each placed old toggle folds into it ------------------------------------------------------------

    [Fact]
    public void S16704_every_placed_old_toggle_becomes_the_quick_setting_of_its_target_in_its_box_with_its_style()
    {
        var placed = new JsonArray();
        for (var i = 0; i < Keys.Length; i++)
        {
            placed.Add(new JsonObject
            {
                ["id"] = $"t{i}",
                ["x"] = 0.05 * i,
                ["y"] = 0.1,
                ["w"] = 0.05,
                ["h"] = 0.05,
                ["config"] = new JsonObject { ["kind"] = Keys[i] + "Toggle", ["style"] = i % 2 == 0 ? "round" : "switch" },
            });
        }

        var document = new JsonObject { ["views"] = new JsonArray(new JsonObject { ["name"] = "Home", ["widgets"] = placed }) };
        var notes = KindMerges.Replace(document);

        Assert.Equal(13, notes.Count);
        var widgets = document["views"]![0]!["widgets"]!.AsArray();
        Assert.Equal(13, widgets.Count);
        for (var i = 0; i < Keys.Length; i++)
        {
            var widget = widgets[i]!;
            Assert.Equal(($"t{i}", 0.05 * i, "quickToggle", Keys[i], i % 2 == 0 ? "round" : "switch"),
                ((string)widget["id"]!, (double)widget["x"]!, (string)widget["config"]!["kind"]!, (string)widget["config"]!["target"]!, (string)widget["config"]!["style"]!));
            var config = Assert.IsType<QuickToggleConfig>(JsonSerializer.Deserialize(widget["config"]!.ToJsonString(), StateJson.WidgetConfig));
            Assert.Equal(Keys[i], QuickToggleRules.Key(config.Target));
        }
    }
}
