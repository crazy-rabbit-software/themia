using System.Reflection;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Motion;
using Themia.Core.Settings;
using Themia.Core.Shell;
using Themia.Core.Sync;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-3524]..[S-3531] The edit bar's settings menu as data: which lines each section offers, their
/// choices and words, and what each writes. Every line writes the field its settings page wrote, and
/// nothing else, through one change of the state.
/// </summary>
[Collection(GlobalLocaleCollection.Name)]
public sealed partial class SettingsMenuTests
{
    /// <summary>A host over a state of its own: each change is applied to it and counted.</summary>
    private sealed class FakeHost : ISettingsMenuHost
    {
        public AppState State { get; } = AppState.CreateDefault();

        public int Mutations { get; private set; }

        public List<bool> RelayoutFlags { get; } = [];

        public AutostartStatus? Startup { get; set; }

        public List<bool> StartupAsked { get; } = [];

        public bool DesktopIconsShown { get; set; } = true;

        public List<bool> DesktopIconsAsked { get; } = [];

        public bool Suspended { get; set; }

        public bool AnimationsEnabled { get; set; } = true;

        public List<IntroMode> Replayed { get; } = [];

        public List<SettingsConfirm> Asked { get; } = [];

        public int AllSettings { get; private set; }

        public int Quits { get; private set; }

        public void Mutate(Action<AppState> change, bool relayout = true)
        {
            change(State);
            Mutations++;
            RelayoutFlags.Add(relayout);
        }

        public void SetStartup(bool on) => StartupAsked.Add(on);

        public void SetDesktopIcons(bool shown) => DesktopIconsAsked.Add(shown);

        public void SetGameModeSuspend(bool on) => Mutate(s => s.Preferences.GameModeSuspend = on);

        public void ReplayIntro(IntroMode mode) => Replayed.Add(mode);

        public Locale WindowsLanguage { get; set; } = Locale.De;

        public List<AppLanguage> LanguagesSet { get; } = [];

        public void SetLanguage(AppLanguage language)
        {
            LanguagesSet.Add(language);
            Mutate(s => s.Preferences.Language = language, relayout: false);
        }

        public void Confirm(SettingsConfirm confirm) => Asked.Add(confirm);

        public void ShowAllSettings() => AllSettings++;

        public void Quit() => Quits++;
    }

    private static IReadOnlyList<SettingsLine> Root(FakeHost host) => SettingsMenu.Root(host);

    private static T At<T>(FakeHost host, params string[] path)
        where T : SettingsLine
        => Assert.IsType<T>(SettingsMenu.Find(Root(host), path));

    /// <summary>The design's properties by name and value, to tell which one a line wrote.</summary>
    private static Dictionary<string, string> Snapshot(object target)
        => target.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
            .ToDictionary(p => p.Name, p => System.Text.Json.JsonSerializer.Serialize(p.GetValue(target)));

    private static string[] Changed(Dictionary<string, string> before, object after)
    {
        var now = Snapshot(after);
        return [.. before.Where(kv => now[kv.Key] != kv.Value).Select(kv => kv.Key)];
    }

    // ---- [S-3524] the first level ----------------------------------------------------------------

    [Fact]
    public void S3524_the_root_is_general_then_appearance_and_online_updates_then_the_way_out()
    {
        var keys = Root(new FakeHost()).Select(l => l is SettingsSeparator ? "-" : l.Key).ToArray();

        Assert.Equal(
            ["startup", "desktopIcons", "gameMode", "-", "appearance", "language", "online", "-", "allSettings", "quit"],
            keys);
    }

    [Fact]
    public void S3532_quit_themia_is_the_last_line_and_quits()
    {
        var host = new FakeHost();
        var quit = Assert.IsType<SettingsCommand>(Root(host)[^1]);

        Assert.Equal("Quit Themia", quit.Text);
        quit.Run();

        Assert.Equal(1, host.Quits);
        Assert.Equal(0, host.Mutations);
    }

    [Fact]
    public void S3533_all_settings_opens_the_window_and_changes_nothing()
    {
        var host = new FakeHost();
        At<SettingsCommand>(host, SettingsMenu.Keys.AllSettings).Run();

        Assert.Equal(1, host.AllSettings);
        Assert.Equal(0, host.Mutations);
    }

    [Fact]
    public void S3531_the_keys_are_the_same_in_every_language_so_a_switch_keeps_what_is_open()
    {
        static List<string> Keys(IReadOnlyList<SettingsLine> lines) => [.. lines.SelectMany(l => l switch
        {
            SettingsSubmenu s => (IEnumerable<string>)[s.Key, .. Keys(s.Lines)],
            SettingsChoice c => [c.Key, .. c.Options.Select(o => o.Key)],
            _ => [l.Key],
        })];

        var english = Keys(Root(new FakeHost()));
        try
        {
            Strings.SetLocale(Locale.De);
            var german = Keys(Root(new FakeHost()));
            Assert.Equal(english, german);
            Assert.Equal("Themia beenden", Assert.IsType<SettingsCommand>(Root(new FakeHost())[^1]).Text);
        }
        finally
        {
            Strings.SetLocale(Locale.En);
        }

        Assert.Equal(english.Count, english.Distinct(StringComparer.Ordinal).Count());
    }

    // ---- [S-3525] General --------------------------------------------------------------------------

    [Fact]
    public void S3525_start_with_windows_shows_the_stored_value_until_windows_is_read()
    {
        var host = new FakeHost();
        host.State.Design.RunAtStartup = true;
        Assert.True(At<SettingsToggle>(host, "startup").On);

        host.Startup = new AutostartStatus(AutostartState.Unknown);
        Assert.True(At<SettingsToggle>(host, "startup").On);

        host.Startup = new AutostartStatus(AutostartState.TurnedOffInWindows);
        Assert.False(At<SettingsToggle>(host, "startup").On);
    }

    [Fact]
    public void S3525_start_with_windows_asks_windows_and_writes_nothing_itself()
    {
        var host = new FakeHost();
        At<SettingsToggle>(host, "startup").Set(true);

        Assert.Equal([true], host.StartupAsked);
        Assert.Equal(0, host.Mutations);
    }

    [Fact]
    public void S3525_what_holds_against_start_with_windows_stands_under_it()
    {
        Assert.Equal((null, null), SettingsMenu.StartupAdvice(null));
        Assert.Equal((null, null), SettingsMenu.StartupAdvice(new AutostartStatus(AutostartState.On)));
        Assert.Equal((null, null), SettingsMenu.StartupAdvice(new AutostartStatus(AutostartState.Off, Detail: "raw")));

        var off = SettingsMenu.StartupAdvice(new AutostartStatus(AutostartState.TurnedOffInWindows));
        Assert.Equal(AutostartRules.Advice(AutostartProblem.TurnedOffInWindows), off.Advice);
        Assert.Null(off.Detail);

        var policy = SettingsMenu.StartupAdvice(new AutostartStatus(AutostartState.OnByPolicy));
        Assert.Equal(AutostartRules.Advice(AutostartProblem.Policy), policy.Advice);

        var failed = SettingsMenu.StartupAdvice(new AutostartStatus(AutostartState.Off, AutostartProblem.Failed, "0x80070005"));
        Assert.NotNull(failed.Advice);
        Assert.Equal("0x80070005", failed.Detail);

        var host = new FakeHost { Startup = new AutostartStatus(AutostartState.Off, AutostartProblem.Failed, "0x80070005") };
        var keys = SettingsMenu.General(host).Select(l => l.Key).ToArray();
        Assert.Equal(["startup", "startup/advice", "startup/detail", "desktopIcons", "gameMode"], keys);
    }

    [Fact]
    public void S3525_desktop_icons_show_what_windows_has_and_ask_windows()
    {
        var host = new FakeHost { DesktopIconsShown = false };
        var line = At<SettingsToggle>(host, "desktopIcons");
        Assert.False(line.On);

        line.Set(true);
        Assert.Equal([true], host.DesktopIconsAsked);
        Assert.Equal(0, host.Mutations);
    }

    [Fact]
    public void S3525_game_mode_says_paused_now_while_it_holds_the_widgets()
    {
        var host = new FakeHost();
        Assert.Null(At<SettingsToggle>(host, "gameMode").Value);

        host.Suspended = true;
        Assert.Equal(Str.Shell_Settings_General_PausedNow.Text(), At<SettingsToggle>(host, "gameMode").Value);

        At<SettingsToggle>(host, "gameMode").Set(true);
        Assert.True(host.State.Preferences.GameModeSuspend);
    }

    // ---- [S-3526] Appearance ----------------------------------------------------------------------

    [Fact]
    public void S3526_appearance_holds_the_pages_groups_in_order()
    {
        var appearance = At<SettingsSubmenu>(new FakeHost(), "appearance");

        Assert.Equal(
            ["appearance/style", "-", "appearance/theme", "appearance/items", "appearance/cards", "appearance/text", "appearance/layout", "-", "appearance/motion"],
            appearance.Lines.Select(l => l is SettingsSeparator ? "-" : l.Key));
    }

    /// <summary>[S-3526] Every line of Appearance writes the one field of the design its page row wrote.</summary>
    [Theory]
    [InlineData("appearance/theme/theme", "Theme")]
    [InlineData("appearance/theme/accent", "PrimaryColor")]
    [InlineData("appearance/theme/background", "Background")]
    [InlineData("appearance/theme/headerBackground", "HeaderBackgroundColor")]
    [InlineData("appearance/theme/headerOpacity", "HeaderOpacity")]
    [InlineData("appearance/items/background", "ItemBackground")]
    [InlineData("appearance/items/text", "ItemTextColor")]
    [InlineData("appearance/cards/radius", "CornerRadiusPx")]
    [InlineData("appearance/cards/border", "BorderStyle")]
    [InlineData("appearance/cards/borderWidth", "BorderWidthPx")]
    [InlineData("appearance/cards/borderColour", "BorderColor")]
    [InlineData("appearance/cards/borderOpacity", "BorderOpacity")]
    [InlineData("appearance/text/font", "FontFamily")]
    [InlineData("appearance/text/size", "FontScale")]
    [InlineData("appearance/text/scale", "UiScale")]
    [InlineData("appearance/layout/gap", "WidgetGapPx")]
    [InlineData("appearance/layout/snap", "GridSnap")]
    [InlineData("appearance/motion/smooth", "SmoothValues")]
    public void S3526_each_line_writes_its_pages_field_and_nothing_else(string path, string field)
    {
        var host = new FakeHost();
        var before = Snapshot(host.State.Design);
        var line = SettingsMenu.Find(Root(host), path.Split('/').Select((_, i) => string.Join('/', path.Split('/').Take(i + 1))).ToArray());

        switch (line)
        {
            case SettingsChoice choice:
                choice.Options.First(o => !o.Holds).Pick();
                break;
            case SettingsToggle toggle:
                toggle.Set(!toggle.On);
                break;
            case SettingsSlider slider:
                slider.Set(slider.Value == slider.Max ? slider.Min : slider.Max);
                break;
            case SettingsColour colour:
                colour.Set("#123456");
                break;
            case SettingsFill fill:
                fill.Set(FillChoice.Of("#123456", 0.5));
                break;
            case SettingsFont font:
                font.Set("Georgia");
                break;
            default:
                Assert.Fail($"{path} is a {line?.GetType().Name ?? "missing line"}");
                break;
        }

        Assert.Equal([field], Changed(before, host.State.Design));
        Assert.Equal(1, host.Mutations);
    }

    [Fact]
    public void S3526_a_choice_of_the_option_that_holds_changes_nothing()
    {
        var host = new FakeHost();
        At<SettingsChoice>(host, "appearance", "appearance/theme", "appearance/theme/theme").Options.Single(o => o.Holds).Pick();
        At<SettingsChoice>(host, "appearance", "appearance/cards", "appearance/cards/border").Options.Single(o => o.Holds).Pick();

        Assert.Equal(0, host.Mutations);
    }

    [Fact]
    public void S3526_the_lines_say_what_holds()
    {
        var host = new FakeHost();
        host.State.Design.Theme = AppTheme.Dark;
        host.State.Design.BorderStyle = WidgetBorderStyle.Dashed;

        Assert.Equal(Str.Settings_Theme_Dark.Text(), At<SettingsChoice>(host, "appearance", "appearance/theme", "appearance/theme/theme").Value);
        Assert.Equal(Str.Settings_Style_Dashed.Text(), At<SettingsSubmenu>(host, "appearance", "appearance/cards").Value);

        var accent = At<SettingsColour>(host, "appearance", "appearance/theme", "appearance/theme/accent");
        Assert.True(accent.DefaultIsWindowsAccent);
        Assert.Equal(Str.Shell_Appearance_WindowsAccent.Text(), accent.DefaultText);
    }

    [Fact]
    public void S3526_the_sliders_cover_the_pages_ranges()
    {
        var host = new FakeHost();
        SettingsSlider Slider(string group, string name) => At<SettingsSlider>(host, "appearance", "appearance/" + group, "appearance/" + group + "/" + name);

        Assert.Equal((0, 1, 0.05), (Slider("theme", "headerOpacity").Min, Slider("theme", "headerOpacity").Max, Slider("theme", "headerOpacity").Step));
        Assert.Equal((0, 32, 1), (Slider("cards", "radius").Min, Slider("cards", "radius").Max, Slider("cards", "radius").Step));
        Assert.Equal((0, 8, 1), (Slider("cards", "borderWidth").Min, Slider("cards", "borderWidth").Max, Slider("cards", "borderWidth").Step));
        Assert.Equal((Widgets.FluentType.MinTextScale, Widgets.FluentType.MaxTextScale), (Slider("text", "size").Min, Slider("text", "size").Max));
        Assert.Equal((0.5, 3), (Slider("text", "scale").Min, Slider("text", "scale").Max));
        Assert.Equal((0, 48, 2), (Slider("layout", "gap").Min, Slider("layout", "gap").Max, Slider("layout", "gap").Step));

        Assert.Equal("12px", Slider("cards", "radius").Format(12));
        Assert.Equal(LocaleFormat.Percent(Strings.Current, 50), Slider("theme", "headerOpacity").Format(0.5));
    }

    // ---- [S-3527] Style ----------------------------------------------------------------------------

    [Fact]
    public void S3527_a_preset_over_a_preset_applies_at_once()
    {
        var host = new FakeHost();
        var style = At<SettingsChoice>(host, "appearance", "appearance/style");
        Assert.DoesNotContain(style.Options, o => o.Key.EndsWith("/custom", StringComparison.Ordinal));

        var other = DesignPresets.All.First(p => p.Id != DesignPresets.Match(host.State.Design));
        style.Options.Single(o => o.Key == "appearance/style/" + other.Id).Pick();

        Assert.Equal(other.Id, DesignPresets.Match(host.State.Design));
        Assert.Empty(host.Asked);
        Assert.Equal(1, host.Mutations);
    }

    [Fact]
    public void S3527_a_preset_over_a_look_of_ones_own_asks_first_and_only_the_answer_applies_it()
    {
        var host = new FakeHost();
        host.State.Design.CornerRadiusPx = 17;
        host.State.Design.PrimaryColor = "#123456";
        Assert.Null(DesignPresets.Match(host.State.Design));

        var style = At<SettingsChoice>(host, "appearance", "appearance/style");
        var custom = style.Options.Single(o => o.Holds);
        Assert.Equal("appearance/style/custom", custom.Key);
        Assert.True(custom.Disabled);

        var target = DesignPresets.All[0];
        style.Options.Single(o => o.Key == "appearance/style/" + target.Id).Pick();

        var asked = Assert.Single(host.Asked);
        Assert.Equal(Str.Shell_Appearance_ReplaceLook_Title.Text(), asked.Question);
        Assert.Contains(target.Label, asked.Detail, StringComparison.Ordinal);
        Assert.Equal(0, host.Mutations);

        asked.Done();
        Assert.Equal(target.Id, DesignPresets.Match(host.State.Design));
        Assert.Equal(1, host.Mutations);
    }

    // ---- [S-3529] Motion ---------------------------------------------------------------------------

    [Theory]
    [InlineData("startup", IntroMode.Startup)]
    [InlineData("unlock", IntroMode.Unlock)]
    [InlineData("transition", IntroMode.Transition)]
    public void S3529_a_moments_style_and_length_are_stored_and_played(string name, IntroMode mode)
    {
        var host = new FakeHost();
        string[] path = ["appearance", "appearance/motion", "appearance/motion/" + name];
        var moment = At<SettingsSubmenu>(host, path);

        Assert.Equal(10, moment.Lines.OfType<SettingsRadio>().Count());
        Assert.Equal(SettingsMenu.IntroStyles().Single(s => s.Value == IntroMotion.StyleFor(host.State.Design, mode)).Text, moment.Value);

        moment.Lines.OfType<SettingsRadio>().Single(r => r.Key.EndsWith("/" + IntroAnimation.Hyperspace, StringComparison.Ordinal)).Pick();
        Assert.Equal(IntroAnimation.Hyperspace, IntroMotion.StyleFor(host.State.Design, mode));

        var length = At<SettingsSubmenu>(host, path).Lines.OfType<SettingsSlider>().Single();
        Assert.Equal((IntroMotion.MinMs, IntroMotion.MaxMs, 100d), (length.Min, length.Max, length.Step));
        length.Set(1200);
        Assert.Equal(1200, IntroMotion.MsFor(host.State.Design, mode));

        At<SettingsSubmenu>(host, path).Lines.OfType<SettingsCommand>().Single().Run();
        Assert.Equal([mode, mode, mode], host.Replayed);
        Assert.Equal(2, host.Mutations);
    }

    [Fact]
    public void S3529_a_moments_length_is_called_length_its_submenu_names_the_moment()
    {
        // Regression: "Animation length" read "Animation len…" beside its track at the menu's width.
        var length = At<SettingsSubmenu>(new FakeHost(), "appearance", "appearance/motion", "appearance/motion/startup").Lines.OfType<SettingsSlider>().Single();

        Assert.Equal("Length", length.Text);
    }

    [Fact]
    public void S3525_start_with_windows_does_not_wear_quit_themias_power_button()
    {
        var host = new FakeHost();
        var startup = At<SettingsToggle>(host, SettingsMenu.Keys.Startup);
        var quit = At<SettingsCommand>(host, SettingsMenu.Keys.Quit);

        Assert.Equal(Glyphs.StartWithWindows, startup.Glyph);
        Assert.NotEqual(quit.Glyph, startup.Glyph);
    }

    [Fact]
    public void S3529_with_windows_animations_off_motion_says_so()
    {
        var host = new FakeHost();
        Assert.DoesNotContain(At<SettingsSubmenu>(host, "appearance", "appearance/motion").Lines, l => l is SettingsNote);

        host.AnimationsEnabled = false;
        var note = Assert.Single(At<SettingsSubmenu>(host, "appearance", "appearance/motion").Lines.OfType<SettingsNote>());
        Assert.Equal(Str.Shell_Motion_AnimationsOff.Text(), note.Text);
    }

    // ---- [S-3530] Online updates -------------------------------------------------------------------

    [Fact]
    public void S3530_online_updates_has_one_line_per_cadence_with_its_intervals()
    {
        var host = new FakeHost();
        var online = At<SettingsSubmenu>(host, "online");
        var rows = online.Lines.OfType<SettingsChoice>().ToList();

        Assert.Equal(UpdateCadence.Rows.Select(r => "online/" + r.Id.Value), rows.Select(r => r.Key));
        foreach (var (row, line) in UpdateCadence.Rows.Zip(rows))
        {
            Assert.Equal(row.Label.Text(), line.Text);
            Assert.Equal(row.Choices.Select(SettingsMenu.Every), line.Options.Select(o => o.Text));
            Assert.Equal(SettingsMenu.Every(UpdateCadence.Minutes(host.State.Preferences.Refresh, row)), line.Value);
        }

        Assert.Equal(Str.WidgetPrefs_Updates_Note.Text(), Assert.Single(online.Lines.OfType<SettingsNote>()).Text);
    }

    [Fact]
    public void S3530_an_interval_picked_is_stored_without_a_relayout()
    {
        var host = new FakeHost();
        var weather = At<SettingsChoice>(host, "online", "online/weather");
        weather.Options.Single(o => o.Key == "online/weather/120").Pick();

        Assert.Equal(120, UpdateCadence.Minutes(host.State.Preferences.Refresh, HostCadences.Weather));
        Assert.Equal([false], host.RelayoutFlags);

        At<SettingsChoice>(host, "online", "online/weather").Options.Single(o => o.Holds).Pick();
        Assert.Equal(1, host.Mutations);
    }

    [Theory]
    [InlineData(1, "Every minute")]
    [InlineData(5, "Every 5 minutes")]
    [InlineData(60, "Every hour")]
    [InlineData(120, "Every 2 hours")]
    public void S3530_an_interval_reads_as_windows_writes_one(int minutes, string text)
        => Assert.Equal(text, SettingsMenu.Every(minutes));
}
