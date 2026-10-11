using Themia.Core.Localization;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

public sealed class MixerRulesTests
{
    private static AudioSessionInfo Session(
        string session,
        string group,
        string name,
        double level = 0.5,
        bool muted = false,
        bool active = false,
        bool system = false)
        => new(session, group, name, system ? null : group, level, muted, active, system);

    private static MixerRow Row(string key, double level = 0.5, bool muted = false, bool active = true, bool system = false)
        => new(key, key, null, level, muted, active, system, 1);

    private static readonly MixerRowOptions All = new(ShowSystemSounds: true, OnlyActive: false);

    // ---- [S-442] grouping ---------------------------------------------------------------------

    [Fact]
    public void S442_a_browser_with_three_tabs_is_one_row()
    {
        var rows = MixerRules.Group(
            [
                Session("s1", @"c:\chrome.exe", "Google Chrome", 0.4),
                Session("s2", @"c:\chrome.exe", "Google Chrome", 0.9, muted: true, active: true),
                Session("s3", @"c:\spotify.exe", "Spotify", 0.5),
            ],
            grouped: true);

        Assert.Equal(2, rows.Count);
        var chrome = rows[0];
        Assert.Equal("Google Chrome", chrome.Name);
        Assert.Equal(2, chrome.Sessions);
        Assert.Equal(0.9, chrome.Level);
        Assert.False(chrome.Muted);
        Assert.True(chrome.Active);
    }

    [Fact]
    public void S442_ungrouped_keeps_one_row_per_session()
    {
        var rows = MixerRules.Group(
            [Session("s1", @"c:\chrome.exe", "Google Chrome"), Session("s2", @"c:\chrome.exe", "Google Chrome")],
            grouped: false);

        Assert.Equal(["s1", "s2"], rows.Select(r => r.Key));
        Assert.All(rows, row => Assert.Equal(1, row.Sessions));
    }

    [Fact]
    public void S442_a_group_is_muted_only_when_every_session_is()
    {
        var rows = MixerRules.Group(
            [Session("s1", "c", "C", muted: true), Session("s2", "c", "C", muted: true)],
            grouped: true);

        Assert.True(rows[0].Muted);
    }

    [Fact]
    public void S442_a_group_takes_the_first_known_name_and_path()
    {
        var rows = MixerRules.Group(
            [Session("s1", "c", string.Empty) with { Path = null }, Session("s2", "c", "Chrome")],
            grouped: true);

        Assert.Equal("Chrome", rows[0].Name);
        Assert.Equal("c", rows[0].Path);
    }

    // ---- [S-443] order ------------------------------------------------------------------------

    [Fact]
    public void S443_orders_by_name_with_system_sounds_last()
    {
        var rows = MixerRules.Group(
            [
                Session("s1", @"c:\spotify.exe", "Spotify"),
                Session("sys", MixerRules.SystemSoundsKey, string.Empty, active: true, system: true),
                Session("s2", @"c:\discord.exe", "Discord", active: true),
            ],
            grouped: true);

        Assert.Equal([@"c:\discord.exe", @"c:\spotify.exe", MixerRules.SystemSoundsKey], rows.Select(r => r.Key));
    }

    [Fact]
    public void S443_orders_case_insensitively_and_ignores_activity()
    {
        var rows = MixerRules.Group(
            [Session("s1", "z", "zoom", active: true), Session("s2", "a", "Arc", active: false)],
            grouped: true);

        Assert.Equal("Arc", rows[0].Name);
    }

    // ---- [S-441] names ------------------------------------------------------------------------

    [Fact]
    public void S441_prefers_the_file_description()
        => Assert.Equal("Google Chrome", MixerRules.SessionName("Google Chrome", @"C:\x\chrome.exe", "Chrome"));

    [Fact]
    public void S441_falls_back_to_the_executable_name()
    {
        Assert.Equal("chrome", MixerRules.SessionName(null, @"C:\x\chrome.exe", string.Empty));
        Assert.Equal("vlc", MixerRules.SessionName("  ", "C:/x/vlc.exe", string.Empty));
    }

    [Fact]
    public void S441_ignores_unexpanded_resource_display_names()
    {
        Assert.Equal(string.Empty, MixerRules.SessionName(null, null, @"@%SystemRoot%\System32\AudioSrv.Dll,-202"));
        Assert.Equal("Discord", MixerRules.SessionName(null, null, "Discord"));
        Assert.Equal(string.Empty, MixerRules.SessionName(null, null, null));
    }

    [Fact]
    public void S441_an_entry_point_description_reads_as_the_app()
        => Assert.Equal("WhatsApp", MixerRules.SessionName("WhatsApp.Root", @"C:\x\WhatsApp.exe", string.Empty));

    // ---- [S-445] / [S-3251] which rows are shown ----------------------------------------------

    [Fact]
    public void S3251_the_list_is_the_apps_alone_with_no_master_row()
    {
        var rows = MixerRules.BuildRows([Row("chrome"), Row("spotify")], All);
        Assert.Equal(["chrome", "spotify"], rows.Select(r => r.Key));
        Assert.DoesNotContain(rows, r => r.Key == MixerRules.MasterKey);
    }

    [Fact]
    public void S445_drops_system_sounds_unless_switched_on()
    {
        var rows = new[] { Row("chrome"), Row(MixerRules.SystemSoundsKey, system: true) };
        Assert.Equal(["chrome"], MixerRules.BuildRows(rows, All with { ShowSystemSounds = false }).Select(r => r.Key));
        Assert.Equal(2, MixerRules.BuildRows(rows, All).Count);
    }

    [Fact]
    public void S445_only_active_hides_idle_apps_but_never_system_sounds()
    {
        var rows = new[]
        {
            Row("a", active: true),
            Row("b", active: false),
            Row(MixerRules.SystemSoundsKey, active: false, system: true),
        };

        var shown = MixerRules.BuildRows(rows, All with { OnlyActive = true });
        Assert.Equal(["a", MixerRules.SystemSoundsKey], shown.Select(r => r.Key));
    }

    [Fact]
    public void S445_keeps_the_grouped_order()
    {
        var shown = MixerRules.BuildRows([Row("z"), Row("a")], All);
        Assert.Equal(["z", "a"], shown.Select(r => r.Key));
    }

    // ---- [S-447] percent ------------------------------------------------------------------------

    [Fact]
    public void S447_a_muted_row_reads_zero_but_keeps_its_level()
    {
        var row = Row("a", level: 0.62, muted: true);
        Assert.Equal(0, MixerRules.DisplayPercent(row));
        Assert.Equal(0.62, row.Level);
    }

    [Fact]
    public void S447_rounds_to_whole_percent()
    {
        Assert.Equal(62, MixerRules.DisplayPercent(Row("a", level: 0.615)));
        Assert.Equal(100, MixerRules.DisplayPercent(Row("a", level: 1)));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(-0.5)]
    [InlineData(3)]
    public void S447_a_junk_level_never_reaches_the_slider(double junk)
    {
        var percent = MixerRules.DisplayPercent(Row("a", level: junk));
        Assert.InRange(percent, 0, 100);
    }

    // ---- [S-446] overrides and writes ---------------------------------------------------------

    [Fact]
    public void S446_keeps_an_override_windows_has_not_confirmed()
    {
        var rows = new[] { Row("a", level: 0.2) };
        var overrides = new Dictionary<string, double> { ["a"] = 0.8 };

        Assert.Equal(0.8, MixerRules.ReconcileOverrides(overrides, rows)["a"]);
        Assert.Equal(0.8, MixerRules.ApplyOverrides(rows, overrides)[0].Level);
    }

    [Fact]
    public void S446_drops_an_override_once_confirmed()
        => Assert.Empty(MixerRules.ReconcileOverrides(new Dictionary<string, double> { ["a"] = 0.8 }, [Row("a", level: 0.8)]));

    [Fact]
    public void S446_drops_an_override_whose_app_exited()
        => Assert.Empty(MixerRules.ReconcileOverrides(new Dictionary<string, double> { ["gone"] = 0.8 }, [Row("a")]));

    [Fact]
    public void S447_dragging_a_muted_row_up_shows_it_unmuted()
    {
        var rows = new[] { Row("a", level: 0, muted: true) };

        var raised = MixerRules.ApplyOverrides(rows, new Dictionary<string, double> { ["a"] = 0.4 })[0];
        Assert.False(raised.Muted);
        Assert.Equal(40, MixerRules.DisplayPercent(raised));

        // Back to zero leaves the mute alone.
        Assert.True(MixerRules.ApplyOverrides(rows, new Dictionary<string, double> { ["a"] = 0 })[0].Muted);
    }

    [Fact]
    public void S446_newest_value_per_key_wins_and_other_keys_survive()
    {
        var writes = new LatestWrites<double>();
        writes.Post("a", 0.1);
        writes.Post("b", 0.5);
        writes.Post("a", 0.2);
        writes.Post("a", 0.3);

        Assert.Equal([("a", 0.3), ("b", 0.5)], writes.Drain());
        Assert.True(writes.IsEmpty);
        Assert.Empty(writes.Drain());
    }

    [Fact]
    public void S446_a_zero_or_false_is_a_real_value_to_write()
    {
        var writes = new LatestWrites<bool>();
        writes.Post("a", true);
        writes.Post("a", false);
        Assert.Equal([("a", false)], writes.Drain());
    }

    [Theory]
    [InlineData(Locale.En, false, false, true, "Playing")]
    [InlineData(Locale.En, true, false, true, "Muted")]
    [InlineData(Locale.De, false, false, true, "Spielt")]
    [InlineData(Locale.De, false, false, false, "Still")]
    [InlineData(Locale.De, false, true, true, "Windows-Klänge")]
    public void S1624_the_caption_speaks_the_language(Locale locale, bool muted, bool system, bool active, string expected)
    {
        var row = new MixerRow("app", "App", null, 0.5, muted, active, system, 1);

        Assert.Equal(expected, MixerFill.Detail(row, locale));
    }
}
