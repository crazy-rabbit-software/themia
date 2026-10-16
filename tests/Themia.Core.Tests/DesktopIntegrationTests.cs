using Themia.Core.Localization;
using Themia.Core.Shell;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// Desktop integration and suspend (WP3, [S-920..S-939]): game mode's decision, the desktop-icons
/// switch, autostart for both registrations, and the click clean-up rules.
/// </summary>
public sealed class DesktopIntegrationTests
{
    private const UserNotificationState Busy = UserNotificationState.Busy;
    private const UserNotificationState D3D = UserNotificationState.RunningD3DFullScreen;
    private const UserNotificationState Free = UserNotificationState.AcceptsNotifications;

    // ---- Game mode -------------------------------------------------------------------------

    [Theory]
    [InlineData(UserNotificationState.Busy)]
    [InlineData(UserNotificationState.RunningD3DFullScreen)]
    [InlineData(UserNotificationState.PresentationMode)]
    public void S920_a_full_screen_application_suspends(UserNotificationState state)
        => Assert.True(GameMode.Decide(enabled: true, state, ForegroundKind.Application, suspended: false));

    [Theory]
    [InlineData(UserNotificationState.AcceptsNotifications)]
    [InlineData(UserNotificationState.QuietTime)]
    [InlineData(UserNotificationState.App)]
    public void S920_anything_else_resumes(UserNotificationState state)
        => Assert.False(GameMode.Decide(enabled: true, state, ForegroundKind.Application, suspended: true));

    [Fact]
    public void S921_off_never_suspends_and_resumes_at_once()
    {
        Assert.False(GameMode.Decide(enabled: false, D3D, ForegroundKind.Application, suspended: false));
        Assert.False(GameMode.Decide(enabled: false, D3D, ForegroundKind.Application, suspended: true));
    }

    [Theory]
    [InlineData(UserNotificationState.Busy)]
    [InlineData(UserNotificationState.PresentationMode)]
    public void S922_own_window_in_front_never_suspends(UserNotificationState state)
    {
        // An edit-mode surface spans its monitor and Windows calls that "busy".
        Assert.False(GameMode.Decide(enabled: true, state, ForegroundKind.OwnWindow, suspended: false));
        Assert.False(GameMode.Decide(enabled: true, state, ForegroundKind.OwnWindow, suspended: true));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void S923_unknown_state_fails_open(bool suspended)
        => Assert.False(GameMode.Decide(enabled: true, UserNotificationState.Unknown, ForegroundKind.Application, suspended));

    [Theory]
    [InlineData(ForegroundKind.ShellSurface, true)]
    [InlineData(ForegroundKind.ShellSurface, false)]
    [InlineData(ForegroundKind.None, true)]
    [InlineData(ForegroundKind.None, false)]
    public void S924_start_over_a_game_or_a_switch_in_flight_holds(ForegroundKind foreground, bool suspended)
    {
        Assert.Equal(suspended, GameMode.Decide(enabled: true, Free, foreground, suspended));
        Assert.Equal(suspended, GameMode.Decide(enabled: true, Busy, foreground, suspended));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void S924_the_lock_screen_holds(bool suspended)
        => Assert.Equal(suspended, GameMode.Decide(enabled: true, UserNotificationState.NotPresent, ForegroundKind.Application, suspended));

    [Fact]
    public void S925_presentation_settings_suspend_whatever_is_in_front()
        => Assert.True(GameMode.Decide(enabled: true, UserNotificationState.PresentationMode, ForegroundKind.Desktop, suspended: false));

    [Fact]
    public void S926_the_desktop_in_front_resumes_even_if_windows_says_busy()
    {
        // Show Desktop over a game is the user asking to see the widgets.
        Assert.False(GameMode.Decide(enabled: true, Busy, ForegroundKind.Desktop, suspended: true));
        Assert.False(GameMode.Decide(enabled: true, D3D, ForegroundKind.Desktop, suspended: true));
    }

    [Fact]
    public void S926_a_game_session_round_trip()
    {
        var suspended = false;

        // The game starts and takes the screen.
        suspended = GameMode.Decide(true, D3D, ForegroundKind.Application, suspended);
        Assert.True(suspended);

        // The Start menu opens over it: no churn.
        suspended = GameMode.Decide(true, Free, ForegroundKind.ShellSurface, suspended);
        Assert.True(suspended);

        // Back in the game.
        suspended = GameMode.Decide(true, D3D, ForegroundKind.Application, suspended);
        Assert.True(suspended);

        // The game quits; the browser behind it is in front.
        suspended = GameMode.Decide(true, Free, ForegroundKind.Application, suspended);
        Assert.False(suspended);
    }

    [Theory]
    [InlineData(0, UserNotificationState.Unknown)]
    [InlineData(1, UserNotificationState.NotPresent)]
    [InlineData(3, UserNotificationState.RunningD3DFullScreen)]
    [InlineData(7, UserNotificationState.App)]
    [InlineData(8, UserNotificationState.Unknown)]
    [InlineData(-1, UserNotificationState.Unknown)]
    public void S923_raw_states_outside_the_known_set_read_as_unknown(int raw, UserNotificationState expected)
        => Assert.Equal(expected, GameMode.FromRaw(raw));

    // ---- Desktop icons ---------------------------------------------------------------------

    [Fact]
    public void S932_adopt_only_a_known_disagreeing_live_state()
    {
        Assert.False(DesktopIconRules.Adopt(false, stored: true));
        Assert.True(DesktopIconRules.Adopt(true, stored: false));
        Assert.Null(DesktopIconRules.Adopt(true, stored: true));
        Assert.Null(DesktopIconRules.Adopt(null, stored: false));
    }

    [Fact]
    public void S932_the_switch_shows_the_live_state_or_the_cache_never_the_unapplied_request()
    {
        Assert.True(DesktopIconRules.Shown(true, stored: false));
        Assert.False(DesktopIconRules.Shown(false, stored: true));
        Assert.True(DesktopIconRules.Shown(null, stored: true));
        Assert.False(DesktopIconRules.Shown(null, stored: false));
    }

    // Regression: the state was read with IsWindowVisible, which is false whenever an ancestor of
    // the list is hidden; a "show" request then toggled shown icons off. Only the list's own bit counts.
    [Theory]
    [InlineData(0x56003A40u, true)]   // the list as read on a live desktop with icons shown
    [InlineData(0x46003A40u, false)]  // the same list, hidden by "Show desktop icons" off
    [InlineData(0x10000000u, true)]
    [InlineData(0u, false)]
    public void S931_the_state_is_the_icon_lists_own_visible_bit(uint style, bool shown)
        => Assert.Equal(shown, DesktopIconRules.IsShown(style));

    [Fact]
    public void S930_the_view_under_progman_wins_over_one_under_a_workerw()
    {
        var found = new[]
        {
            new DesktopViewCandidate(UnderProgman: false, OwnedByShell: true, HasList: true),
            new DesktopViewCandidate(UnderProgman: true, OwnedByShell: true, HasList: true),
        };

        Assert.Equal(1, DesktopIconRules.ChooseView(found));
    }

    [Fact]
    public void S930_a_workerw_view_is_used_when_progman_has_none()
    {
        var found = new[]
        {
            new DesktopViewCandidate(UnderProgman: false, OwnedByShell: true, HasList: true),
            new DesktopViewCandidate(UnderProgman: false, OwnedByShell: true, HasList: true),
        };

        Assert.Equal(0, DesktopIconRules.ChooseView(found));
    }

    [Fact]
    public void S930_a_view_of_another_process_or_without_its_list_is_never_the_target()
    {
        Assert.Equal(-1, DesktopIconRules.ChooseView([]));
        Assert.Equal(-1, DesktopIconRules.ChooseView([new DesktopViewCandidate(UnderProgman: true, OwnedByShell: false, HasList: true)]));
        Assert.Equal(-1, DesktopIconRules.ChooseView([new DesktopViewCandidate(UnderProgman: true, OwnedByShell: true, HasList: false)]));
        Assert.Equal(1, DesktopIconRules.ChooseView(
        [
            new DesktopViewCandidate(UnderProgman: true, OwnedByShell: false, HasList: true),
            new DesktopViewCandidate(UnderProgman: false, OwnedByShell: true, HasList: true),
        ]));
    }

    [Theory]
    [InlineData(true, true, false, DesktopIconStep.Done)]
    [InlineData(false, false, false, DesktopIconStep.Done)]
    [InlineData(true, false, false, DesktopIconStep.Toggle)]
    [InlineData(false, true, false, DesktopIconStep.Toggle)]
    [InlineData(true, true, true, DesktopIconStep.Done)]
    [InlineData(true, false, true, DesktopIconStep.DidNotTake)]
    [InlineData(false, true, true, DesktopIconStep.DidNotTake)]
    public void S939_one_toggle_per_request_and_the_read_after_it_confirms(bool live, bool wanted, bool sent, DesktopIconStep expected)
        => Assert.Equal(expected, DesktopIconRules.Plan(live, wanted, sent));

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void S939_an_unreadable_shell_is_never_toggled(bool wanted, bool sent)
        => Assert.Equal(DesktopIconStep.Unreadable, DesktopIconRules.Plan(null, wanted, sent));

    [Fact]
    public void S939_requests_start_one_worker_and_the_latest_wins()
    {
        var requests = new DesktopIconRequests();
        Assert.False(requests.Busy);

        Assert.True(requests.Submit(false));   // starts the worker
        Assert.True(requests.Busy);
        Assert.False(requests.Take());         // the worker takes "hide"

        // Off-on-off while it works: no second worker, and only the last position is applied next.
        Assert.False(requests.Submit(true));
        Assert.False(requests.Submit(false));
        Assert.False(requests.Submit(true));
        Assert.True(requests.Take());
        Assert.True(requests.Busy);

        Assert.Null(requests.Take());          // nothing left: the worker stops
        Assert.False(requests.Busy);

        Assert.True(requests.Submit(false));   // the next request starts a worker again
    }

    // ---- Autostart -------------------------------------------------------------------------

    private const string Exe = @"C:\Program Files\Themia\Themia.exe";

    [Fact]
    public void S933_the_mechanism_follows_package_identity()
    {
        Assert.Equal(AutostartMechanism.StartupTask, AutostartRules.MechanismFor(packaged: true));
        Assert.Equal(AutostartMechanism.RunKey, AutostartRules.MechanismFor(packaged: false));
    }

    [Fact]
    public void S934_the_run_command_quotes_the_path_and_carries_the_flag()
    {
        var command = AutostartRules.RunCommand(Exe);
        Assert.Equal("\"C:\\Program Files\\Themia\\Themia.exe\" --autostart", command);
        Assert.True(AutostartRules.LaunchesThis(command, Exe));
    }

    [Theory]
    [InlineData("\"C:\\Program Files\\Themia\\Themia.exe\"", true)]
    [InlineData("\"c:\\program files\\themia\\THEMIA.EXE\" --autostart", true)]
    [InlineData("\"D:\\Old\\Themia.exe\" --autostart", false)]
    [InlineData("C:\\Program Files\\Themia\\Themia.exe", false)]
    [InlineData("\"C:\\Program Files\\Themia\\Themia.exe", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void S936_a_run_value_counts_only_when_it_launches_this_executable(string? command, bool expected)
        => Assert.Equal(expected, AutostartRules.LaunchesThis(command, Exe));

    [Fact]
    public void S936_an_unquoted_path_without_spaces_still_counts()
        => Assert.True(AutostartRules.LaunchesThis(@"C:\Apps\Themia.exe --autostart", @"C:\Apps\Themia.exe"));

    [Theory]
    [InlineData(null, true)]
    [InlineData(new byte[0], true)]
    [InlineData(new byte[] { 0x02, 0, 0, 0 }, true)]
    [InlineData(new byte[] { 0x06, 0, 0, 0 }, true)]
    [InlineData(new byte[] { 0x03, 0x1A, 0x2B }, false)]
    [InlineData(new byte[] { 0x07, 0 }, false)]
    public void S936_windows_startup_switch_is_the_low_bit_of_the_first_byte(byte[]? record, bool approved)
        => Assert.Equal(approved, AutostartRules.StartupApproved(record));

    [Fact]
    public void S936_enabled_means_it_will_actually_run()
    {
        Assert.Equal(AutostartState.On, AutostartRules.FromRunKey(launchesThis: true, approved: true));
        Assert.Equal(AutostartState.TurnedOffInWindows, AutostartRules.FromRunKey(launchesThis: true, approved: false));
        Assert.Equal(AutostartState.Off, AutostartRules.FromRunKey(launchesThis: false, approved: true));
        Assert.False(new AutostartStatus(AutostartState.TurnedOffInWindows).WillStart);
    }

    [Theory]
    [InlineData(0, AutostartState.Off, false)]
    [InlineData(1, AutostartState.TurnedOffInWindows, false)]
    [InlineData(2, AutostartState.On, true)]
    [InlineData(3, AutostartState.OffByPolicy, false)]
    [InlineData(4, AutostartState.OnByPolicy, true)]
    [InlineData(9, AutostartState.Unknown, false)]
    public void S937_startup_task_states_map_to_what_will_happen(int raw, AutostartState state, bool willStart)
    {
        Assert.Equal(state, AutostartRules.FromStartupTask(raw));
        Assert.Equal(willStart, new AutostartStatus(state).WillStart);
    }

    [Fact]
    public void S937_policy_states_are_locked()
    {
        Assert.True(new AutostartStatus(AutostartState.OffByPolicy).IsLocked);
        Assert.True(new AutostartStatus(AutostartState.OnByPolicy).IsLocked);
        Assert.False(new AutostartStatus(AutostartState.TurnedOffInWindows).IsLocked);
    }

    [Fact]
    public void S935_a_change_that_took_has_no_problem()
    {
        Assert.Equal(AutostartProblem.None, AutostartRules.Outcome(true, AutostartState.On).Problem);
        Assert.Equal(AutostartProblem.None, AutostartRules.Outcome(false, AutostartState.Off).Problem);
        Assert.Equal(AutostartProblem.None, AutostartRules.Outcome(false, AutostartState.TurnedOffInWindows).Problem);
    }

    [Theory]
    [InlineData(true, AutostartState.TurnedOffInWindows, AutostartProblem.TurnedOffInWindows)]
    [InlineData(true, AutostartState.OffByPolicy, AutostartProblem.Policy)]
    [InlineData(false, AutostartState.OnByPolicy, AutostartProblem.Policy)]
    [InlineData(true, AutostartState.Off, AutostartProblem.Failed)]
    [InlineData(true, AutostartState.Unknown, AutostartProblem.Failed)]
    public void S935_a_change_that_did_not_take_says_why(bool wanted, AutostartState reached, AutostartProblem problem)
    {
        var outcome = AutostartRules.Outcome(wanted, reached, "detail");
        Assert.Equal(problem, outcome.Problem);
        Assert.Equal("detail", outcome.Detail);
        Assert.False(string.IsNullOrWhiteSpace(AutostartRules.Advice(Locale.En, outcome.Problem)));
    }

    [Fact]
    public void S935_no_problem_no_advice() => Assert.Null(AutostartRules.Advice(Locale.En, AutostartProblem.None));

    [Fact]
    public void S934_launch_source_from_the_flag_or_the_activation()
    {
        Assert.Equal(LaunchSource.Autostart, AutostartRules.SourceOf([Exe, "--autostart"], startupTaskActivation: false));
        Assert.Equal(LaunchSource.Autostart, AutostartRules.SourceOf([Exe, "--AUTOSTART"], startupTaskActivation: false));
        Assert.Equal(LaunchSource.Autostart, AutostartRules.SourceOf([Exe], startupTaskActivation: true));
        Assert.Equal(LaunchSource.Manual, AutostartRules.SourceOf([Exe], startupTaskActivation: false));

        // The executable's own path is never read as the flag.
        Assert.Equal(LaunchSource.Manual, AutostartRules.SourceOf(["--autostart"], startupTaskActivation: false));
    }

    // ---- Click clean-up --------------------------------------------------------------------

    [Theory]
    [InlineData("Windows.UI.Core.CoreWindow", @"C:\Windows\SystemApps\Microsoft.Windows.StartMenuExperienceHost_cw5n1h2txyewy\StartMenuExperienceHost.exe", true)]
    [InlineData("Windows.UI.Core.CoreWindow", @"C:\Windows\SystemApps\MicrosoftWindows.Client.CBS_cw5n1h2txyewy\SearchHost.exe", true)]
    [InlineData("Windows.UI.Core.CoreWindow", @"C:\Windows\SystemApps\Microsoft.Windows.Search_cw5n1h2txyewy\SearchApp.exe", true)]
    [InlineData("Windows.UI.Core.CoreWindow", @"C:\Program Files\WindowsApps\Microsoft.WindowsCalculator\CalculatorApp.exe", false)]
    [InlineData("Chrome_WidgetWin_1", @"C:\Windows\SystemApps\Microsoft.Windows.StartMenuExperienceHost_cw5n1h2txyewy\StartMenuExperienceHost.exe", false)]
    [InlineData("Windows.UI.Core.CoreWindow", null, false)]
    public void S938_only_start_and_search_get_escape(string windowClass, string? process, bool expected)
        => Assert.Equal(expected, ShellClickRules.IsStartOrSearch(windowClass, process));

    [Theory]
    [InlineData("#32768", null, true)]
    [InlineData("#32768", @"C:\Program Files\Some\app.exe", true)]
    [InlineData("Microsoft.UI.Content.PopupWindowSiteBridge", @"C:\Windows\explorer.exe", true)]
    [InlineData("Xaml_WindowedPopupClass", @"C:\WINDOWS\Explorer.EXE", true)]
    [InlineData("Microsoft.UI.Content.PopupWindowSiteBridge", @"C:\Program Files\Themia\Themia.exe", false)]
    [InlineData("tooltips_class32", @"C:\Windows\explorer.exe", false)]
    public void S938_a_shell_menu_is_classic_or_owned_by_explorer(string windowClass, string? owner, bool expected)
        => Assert.Equal(expected, ShellClickRules.IsShellMenu(windowClass, owner));

    [Theory]
    [InlineData("#32768", true)]
    [InlineData("Microsoft.UI.Content.PopupWindowSiteBridge", true)]
    [InlineData("Button", false)]
    [InlineData(null, false)]
    public void S938_menu_candidates_are_cheap_to_rule_out(string? windowClass, bool expected)
        => Assert.Equal(expected, ShellClickRules.IsMenuCandidate(windowClass));
}
