using System.Text.Json;
using Themia.Core.Model;
using Themia.Core.Shell;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// The three widgets on Windows' own panels, after Karpo: the notification list [S-4400..S-4409], the
/// focus session [S-4420..S-4429] and the brightness slider [S-4440..S-4449]. Nothing here touches the machine.
/// </summary>
public class WindowsPanelsTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);

    private static NotificationItem Note(uint id, string app, int minutesAgo, string name = "")
        => new(id, app, name, $"t{id}", $"b{id}", T0.AddMinutes(-minutesAgo));

    // ---- configs -------------------------------------------------------------------------------

    [Theory]
    [InlineData(WidgetKind.Notifications)]
    [InlineData(WidgetKind.FocusSession)]
    [InlineData(WidgetKind.Brightness)]
    public void S4400_each_config_round_trips_through_the_state_file(WidgetKind kind)
    {
        var config = WidgetConfig.CreateDefault(kind);
        var json = JsonSerializer.Serialize(config, StateJson.WidgetConfig);
        var back = JsonSerializer.Deserialize(json, StateJson.WidgetConfig);

        Assert.NotNull(back);
        Assert.Equal(kind, back!.Kind);
        Assert.Equal(kind, back.Clone().Kind);
    }

    [Fact]
    public void S4442_the_screens_choice_is_written_as_a_word_and_read_back()
    {
        var config = new BrightnessConfig { Screens = BrightnessScreens.ThisDisplay };
        var json = JsonSerializer.Serialize<WidgetConfig>(config, StateJson.WidgetConfig);

        Assert.Contains("\"ThisDisplay\"", json, StringComparison.Ordinal);
        var back = Assert.IsType<BrightnessConfig>(JsonSerializer.Deserialize(json, StateJson.WidgetConfig));
        Assert.Equal(BrightnessScreens.ThisDisplay, back.Screens);
        Assert.Equal(BrightnessScreens.ThisDisplay, ((BrightnessConfig)back.Clone()).Screens);
    }

    [Fact]
    public void S4421_a_hand_edited_length_is_pulled_into_range_and_said_so()
    {
        var config = new FocusSessionConfig { Minutes = 9000 };
        Assert.NotEmpty(config.Repair());
        Assert.Equal(FocusSessionRules.MaxMinutes, config.Minutes);

        var fine = new FocusSessionConfig { Minutes = 45 };
        Assert.Empty(fine.Repair());
        Assert.Equal(45, fine.Minutes);
    }

    [Fact]
    public void S4442_an_unknown_screens_number_is_repaired_to_every_display()
    {
        var config = new BrightnessConfig { Screens = (BrightnessScreens)7 };
        Assert.NotEmpty(config.Repair());
        Assert.Equal(BrightnessScreens.All, config.Screens);
    }

    [Fact]
    public void S4400_none_of_the_three_asks_to_be_set_up()
    {
        foreach (var kind in new[] { WidgetKind.Notifications, WidgetKind.FocusSession, WidgetKind.Brightness })
        {
            Assert.False(WidgetSetup.Needed(WidgetConfig.CreateDefault(kind), Themia.Core.Accounts.AccountFeatures.None), kind.ToString());
        }
    }

    // ---- notifications -------------------------------------------------------------------------

    [Fact]
    public void S4402_grouped_by_app_apps_by_their_newest_each_newest_first()
    {
        var groups = NotificationRules.Groups(
        [
            Note(1, "Mail", 30, "Mail"),
            Note(2, "Chat", 5, "Chat"),
            Note(3, "mail", 1, "Mail"),
            Note(4, "Chat", 50, "Chat"),
        ]);

        Assert.Equal(["mail", "Chat"], groups.Select(g => g.AppId));
        Assert.Equal([3u, 1u], groups[0].Items.Select(n => n.Id));
        Assert.Equal([2u, 4u], groups[1].Items.Select(n => n.Id));
        Assert.Equal("Mail", groups[0].AppName);
    }

    [Fact]
    public void S4402_two_of_the_same_moment_put_the_later_id_first()
    {
        var sorted = NotificationRules.Newest([Note(5, "A", 0), Note(9, "A", 0), Note(7, "A", 0)]);
        Assert.Equal([9u, 7u, 5u], sorted.Select(n => n.Id));
    }

    [Fact]
    public void S4402_a_long_group_shows_its_newest_three_and_how_many_more()
    {
        var group = NotificationRules.Groups([.. Enumerable.Range(1, 5).Select(i => Note((uint)i, "A", i))])[0];

        var (rows, more) = NotificationRules.Fold(group, open: false);
        Assert.Equal([1u, 2u, 3u], rows.Select(n => n.Id));
        Assert.Equal(2, more);

        var (all, none) = NotificationRules.Fold(group, open: true);
        Assert.Equal(5, all.Count);
        Assert.Equal(0, none);

        var shortGroup = NotificationRules.Groups([Note(1, "B", 1), Note(2, "B", 2), Note(3, "B", 3)])[0];
        Assert.Equal(0, NotificationRules.Fold(shortGroup, open: false).More);
    }

    [Fact]
    public void S4401_the_first_text_is_the_title_the_rest_the_body()
    {
        var (title, body) = NotificationRules.Text(["  ", "Anna", "See you at 5", null, "Bring the keys\r"]);
        Assert.Equal("Anna", title);
        Assert.Equal("See you at 5\nBring the keys", body);

        Assert.Equal((string.Empty, string.Empty), NotificationRules.Text([]));
    }

    [Fact]
    public void S4401_control_characters_go_and_long_text_is_cut()
    {
        var (title, body) = NotificationRules.Text(["Hi\u0007 there", new string('x', 2000)]);
        Assert.Equal("Hi there", title);
        Assert.Equal(NotificationRules.MostBody, body.Length);
        Assert.EndsWith("…", body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Windows.SystemToast.SecurityAndMaintenance", "Windows")]
    [InlineData("Microsoft.Windows.Explorer", "Windows")]
    [InlineData("", "Windows")]
    [InlineData("5319275A.WhatsAppDesktop_cv1g1gvanyjgm!App", "WhatsAppDesktop")]
    [InlineData("Contoso.Chat.App", "Chat")]
    [InlineData(@"{6D809377-6AF0-444B-8957-A3773F02200E}\Vendor\Tool\tool.exe", "tool")]
    [InlineData("Slack", "Slack")]
    public void S4405_a_sender_without_a_name_is_named_from_its_id(string id, string expected)
        => Assert.Equal(expected, NotificationRules.FallbackName(id));

    [Fact]
    public void S4405_a_group_takes_the_name_windows_gave_or_the_fallback()
    {
        var unnamed = NotificationRules.Groups([Note(1, "Windows.SystemToast.Calling", 1)]);
        Assert.Equal("Windows", unnamed[0].AppName);
    }

    [Fact]
    public void S4406_the_sender_opens_as_its_apps_folder_item_and_nothing_else_gets_through()
    {
        Assert.Equal(@"shell:AppsFolder\5319275A.WhatsAppDesktop_cv1g1gvanyjgm!App", NotificationRules.AppTarget("5319275A.WhatsAppDesktop_cv1g1gvanyjgm!App"));
        Assert.Equal(@"shell:AppsFolder\{6D809377-6AF0-444B-8957-A3773F02200E}\Tool\tool.exe", NotificationRules.AppTarget(@"{6D809377-6AF0-444B-8957-A3773F02200E}\Tool\tool.exe"));

        Assert.Null(NotificationRules.AppTarget(null));
        Assert.Null(NotificationRules.AppTarget("  "));
        Assert.Null(NotificationRules.AppTarget("app\" & calc"));
        Assert.Null(NotificationRules.AppTarget(@"..\..\Windows\System32\calc.exe"));
        Assert.Null(NotificationRules.AppTarget(@"\\server\share\x.exe"));
        Assert.Null(NotificationRules.AppTarget("a%PATH%b"));
        Assert.Null(NotificationRules.AppTarget(new string('a', 600)));
    }

    [Fact]
    public void S4407_a_dismissal_hides_at_once_and_is_forgotten_when_windows_lets_it_go()
    {
        var windows = new[] { Note(1, "A", 1), Note(2, "A", 2) };

        var (shown, still) = NotificationRules.Visible(windows, new HashSet<uint> { 2 });
        Assert.Equal([1u], shown.Select(n => n.Id));
        Assert.Equal([2u], still);

        // Windows has removed it: nothing is hidden any more, so a later id 2 would show.
        var (after, none) = NotificationRules.Visible([Note(1, "A", 1)], still);
        Assert.Equal([1u], after.Select(n => n.Id));
        Assert.Empty(none);
    }

    [Fact]
    public void S4404_the_ladder_face_then_flat_then_grouped_with_more_body()
    {
        var face = NotificationRules.Plan(new WidgetArea(150, 150));
        Assert.True(face.Face);

        var low = NotificationRules.Plan(new WidgetArea(320, 50));
        Assert.True(low.Face);

        var strip = NotificationRules.Plan(new WidgetArea(440, 110));
        Assert.False(strip.Face);
        Assert.Equal(0, strip.BodyLines);

        var small = NotificationRules.Plan(new WidgetArea(320, 200));
        Assert.False(small.Face);
        Assert.False(small.Grouped);
        Assert.Equal(1, small.BodyLines);

        var medium = NotificationRules.Plan(new WidgetArea(320, 320));
        Assert.True(medium.Grouped);
        Assert.Equal(2, medium.BodyLines);

        var large = NotificationRules.Plan(new WidgetArea(480, 560));
        Assert.True(large.Grouped);
        Assert.Equal(3, large.BodyLines);
    }

    // ---- focus session -------------------------------------------------------------------------

    [Fact]
    public void S4421_a_session_runs_for_its_length_and_reports_what_is_left()
    {
        var state = FocusSessionRules.Start(T0, 25);
        Assert.True(state.Running);
        Assert.Equal(TimeSpan.FromMinutes(25), state.Remaining(T0));
        Assert.Equal(TimeSpan.FromMinutes(5), state.Remaining(T0.AddMinutes(20)));
        Assert.Equal(0.8, state.Progress(T0.AddMinutes(20)), 3);
        Assert.False(state.Over(T0.AddMinutes(24)));
        Assert.True(state.Over(T0.AddMinutes(25)));
        Assert.Equal(TimeSpan.Zero, state.Remaining(T0.AddHours(1)));

        Assert.False(FocusSessionState.Idle.Running);
        Assert.Equal(0, FocusSessionState.Idle.Progress(T0));
    }

    [Fact]
    public void S4421_the_length_steps_by_five_minutes_on_the_grid_inside_the_range()
    {
        Assert.Equal(35, FocusSessionRules.Step(30, +1));
        Assert.Equal(25, FocusSessionRules.Step(30, -1));
        Assert.Equal(35, FocusSessionRules.Step(32, +1));
        Assert.Equal(30, FocusSessionRules.Step(32, -1));
        Assert.Equal(FocusSessionRules.MinMinutes, FocusSessionRules.Step(5, -1));
        Assert.Equal(FocusSessionRules.MaxMinutes, FocusSessionRules.Step(240, +1));
        Assert.Equal(FocusSessionRules.MaxMinutes, FocusSessionRules.Start(T0, 9999).Remaining(T0).TotalMinutes);
    }

    [Theory]
    [InlineData(1500, "25:00")]
    [InlineData(1499.2, "25:00")]
    [InlineData(0.4, "00:01")]
    [InlineData(0, "00:00")]
    [InlineData(3600, "1:00:00")]
    [InlineData(3599, "59:59")]
    [InlineData(5025, "1:23:45")]
    public void S4422_what_is_left_reads_as_a_clock_rounded_up(double seconds, string expected)
        => Assert.Equal(expected, FocusSessionRules.Clock(TimeSpan.FromSeconds(seconds)));

    [Fact]
    public void S4422_only_a_running_session_ticks()
    {
        Assert.Null(FocusSessionRules.Tick(FocusSessionState.Idle));
        Assert.Equal(TimeSpan.FromSeconds(1), FocusSessionRules.Tick(FocusSessionRules.Start(T0, 30)));
    }

    [Fact]
    public void S4423_off_before_the_session_it_is_turned_on_and_handed_back_at_the_end()
    {
        var hold = new FocusQuietHold();
        Assert.Equal(QuietCommand.TurnOn, hold.Update(true, ToggleReading.Of(false)));
        hold.Settled(succeeded: true);
        Assert.True(hold.TurnedOn);

        Assert.Equal(QuietCommand.None, hold.Update(true, ToggleReading.Of(true)));
        Assert.Equal(QuietCommand.TurnOff, hold.Update(false, ToggleReading.Of(true)));
        Assert.False(hold.Holding);
    }

    [Fact]
    public void S4423_on_before_the_session_it_is_the_persons_and_left_on()
    {
        var hold = new FocusQuietHold();
        Assert.Equal(QuietCommand.None, hold.Update(true, ToggleReading.Of(true)));
        Assert.False(hold.TurnedOn);
        Assert.Equal(QuietCommand.None, hold.Update(false, ToggleReading.Of(true)));
    }

    [Fact]
    public void S4423_switched_off_by_the_person_during_the_session_it_stays_theirs()
    {
        var hold = new FocusQuietHold();
        hold.Update(true, ToggleReading.Of(false));
        hold.Settled(succeeded: true);

        Assert.Equal(QuietCommand.None, hold.Update(true, ToggleReading.Of(false)));
        Assert.False(hold.TurnedOn);

        // Turned back on by the person: still theirs; the end leaves it alone.
        Assert.Equal(QuietCommand.None, hold.Update(true, ToggleReading.Of(true)));
        Assert.Equal(QuietCommand.None, hold.Update(false, ToggleReading.Of(true)));
    }

    [Fact]
    public void S4423_an_off_reading_before_windows_answers_the_switch_is_not_the_person()
    {
        var hold = new FocusQuietHold();
        Assert.Equal(QuietCommand.TurnOn, hold.Update(true, ToggleReading.Of(false)));

        // The reading from before the switch arrives after the ask: still the session's.
        Assert.Equal(QuietCommand.None, hold.Update(true, ToggleReading.Of(false)));
        Assert.True(hold.TurnedOn);
        hold.Settled(succeeded: true);
        Assert.Equal(QuietCommand.TurnOff, hold.Update(false, ToggleReading.Of(true)));
    }

    [Fact]
    public void S4423_unknown_waits_for_windows_and_a_refusal_runs_without_it()
    {
        var hold = new FocusQuietHold();
        Assert.Equal(QuietCommand.None, hold.Update(true, ToggleReading.Pending));
        Assert.Equal(QuietCommand.TurnOn, hold.Update(true, ToggleReading.Of(false)));
        hold.Settled(succeeded: false);
        Assert.False(hold.TurnedOn);
        Assert.Equal(QuietCommand.None, hold.Update(false, ToggleReading.Of(false)));

        var blocked = new FocusQuietHold();
        Assert.Equal(QuietCommand.None, blocked.Update(true, ToggleReading.Blocked(ToggleBlock.NoSwitch)));
        Assert.Equal(QuietCommand.None, blocked.Update(false, ToggleReading.Blocked(ToggleBlock.NoSwitch)));
    }

    [Fact]
    public void S4423_a_session_that_ends_before_windows_answered_asks_nothing()
    {
        var hold = new FocusQuietHold();
        Assert.Equal(QuietCommand.None, hold.Update(true, ToggleReading.Pending));
        Assert.Equal(QuietCommand.None, hold.Update(false, ToggleReading.Pending));
        Assert.False(hold.Holding);
    }

    [Fact]
    public void S4426_the_ladder_time_then_button_then_words_steppers_status_ring()
    {
        var tiny = FocusSessionRules.Plan(new WidgetArea(140, 100));
        Assert.False(tiny.Button);

        var small = FocusSessionRules.Plan(new WidgetArea(240, 110));
        Assert.True(small.Button);
        Assert.True(small.Words);
        Assert.False(small.Status);

        var square = FocusSessionRules.Plan(new WidgetArea(240, 200));
        Assert.True(square.Steppers);
        Assert.True(square.Status);
        Assert.False(square.Ring);

        var big = FocusSessionRules.Plan(new WidgetArea(420, 320));
        Assert.True(big.Ring);
    }

    [Fact]
    public void S4425_the_iid_of_a_parameterised_interface_is_the_winrt_one()
    {
        // IIterable<String>, as Windows' own headers give it.
        Assert.Equal(new Guid("e2fcc7c1-3bfc-5a0b-b2b0-72e769d1cb7e"), WinRtIid.Of("pinterface({faa585ea-6214-4217-afda-7f46de5869b3};string)"));

        // IVector<String>.
        Assert.Equal(new Guid("98b9acc1-4b56-532e-ac73-03d5291cca90"), WinRtIid.Of("pinterface({913337e9-11a1-4345-a3a2-4e7f956e222d};string)"));
    }

    [Fact]
    public void S4425_the_focus_event_handler_signature_names_the_class_and_its_interface()
    {
        var signature = WinRtIid.TypedEventHandlerOfClass("Windows.UI.Shell.FocusSessionManager", new Guid("e7ffbaa9-d8be-5dbf-bac6-49364842e37e"));
        Assert.Equal(
            "pinterface({9de1c534-6ae1-11e0-84e1-18a905bcc53f};rc(Windows.UI.Shell.FocusSessionManager;{e7ffbaa9-d8be-5dbf-bac6-49364842e37e});cinterface(IInspectable))",
            signature);
    }

    // ---- brightness ----------------------------------------------------------------------------

    private static readonly BrightnessDisplay Panel = new(@"\\.\DISPLAY1", BrightnessPath.BuiltIn, 60);
    private static readonly BrightnessDisplay Monitor = new(@"\\.\DISPLAY2", BrightnessPath.Ddc, 30);

    [Fact]
    public void S4442_every_display_together_by_default_its_own_shown()
    {
        BrightnessDisplay[] both = [Panel, Monitor];

        Assert.Equal(both, BrightnessRules.Dimmed(both, @"\\.\DISPLAY2", BrightnessScreens.All));
        Assert.Same(Monitor, BrightnessRules.Shown(both, @"\\.\display2", BrightnessScreens.All));

        // On a display with none: every display's slider shows the first that has one.
        Assert.Same(Panel, BrightnessRules.Shown(both, @"\\.\DISPLAY3", BrightnessScreens.All));
    }

    [Fact]
    public void S4442_only_its_own_display_or_nothing_where_that_one_has_none()
    {
        BrightnessDisplay[] both = [Panel, Monitor];

        Assert.Equal([Monitor], BrightnessRules.Dimmed(both, @"\\.\DISPLAY2", BrightnessScreens.ThisDisplay));
        Assert.Empty(BrightnessRules.Dimmed(both, @"\\.\DISPLAY3", BrightnessScreens.ThisDisplay));
        Assert.Null(BrightnessRules.Shown(both, @"\\.\DISPLAY3", BrightnessScreens.ThisDisplay));
        Assert.Null(BrightnessRules.Shown([], null, BrightnessScreens.All));
    }

    [Theory]
    [InlineData(0u, 50u, 100u, 50)]
    [InlineData(0u, 0u, 100u, 0)]
    [InlineData(20u, 60u, 100u, 50)]
    [InlineData(0u, 255u, 100u, 100)]
    [InlineData(0u, 7u, 10u, 70)]
    public void S4441_a_monitors_range_reads_in_percent(uint min, uint current, uint max, int expected)
        => Assert.Equal(expected, BrightnessRules.Percent(min, current, max));

    [Fact]
    public void S4441_a_range_that_is_no_range_reads_as_nothing_and_writes_its_minimum()
    {
        Assert.Null(BrightnessRules.Percent(50, 50, 50));
        Assert.Equal(50u, BrightnessRules.Raw(80, 50, 50));
        Assert.Equal(60u, BrightnessRules.Raw(50, 20, 100));
        Assert.Equal(10u, BrightnessRules.Raw(150, 0, 10));
        Assert.Equal(0u, BrightnessRules.Raw(-5, 0, 10));
    }

    [Fact]
    public void S4443_the_newest_level_asked_wins_and_a_write_of_an_older_one_keeps_it()
    {
        var levels = new NewestLevels();
        levels.Ask("A", 10);
        levels.Ask("A", 40);
        levels.Ask("B", 120);

        Assert.Equal(40, levels.Asked("A"));
        Assert.Equal(100, levels.Asked("b"));
        Assert.Equal(2, levels.Pending().Count);

        // A was being written at 40 when the drag asked 45: the 45 stays for the next round.
        levels.Ask("A", 45);
        levels.Written("A", 40);
        Assert.Equal(45, levels.Asked("A"));

        levels.Written("A", 45);
        Assert.Null(levels.Asked("A"));

        levels.Forget("B");
        Assert.Empty(levels.Pending());
    }

    [Theory]
    [InlineData(WidgetKey.Right, 50, 51)]
    [InlineData(WidgetKey.Down, 0, 0)]
    [InlineData(WidgetKey.PageUp, 95, 100)]
    [InlineData(WidgetKey.PageDown, 50, 40)]
    [InlineData(WidgetKey.Home, 50, 0)]
    [InlineData(WidgetKey.End, 50, 100)]
    public void S4444_the_keys_step_as_windows_slider(WidgetKey key, int level, int expected)
        => Assert.Equal(expected, BrightnessRules.Key(key, level));

    [Fact]
    public void S4444_other_keys_are_not_the_sliders()
        => Assert.Null(BrightnessRules.Key(WidgetKey.Enter, 50));
}
