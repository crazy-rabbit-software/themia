using Themia.Core.Accounts;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// The power widgets' decisions: which kind runs which action, which actions arm, the arm state
/// machine, what each action calls, how failures read, and the face ladder. [S-1240..S-1251],
/// [S-1910..S-1916]
/// <para>
/// Nothing here touches the machine: <see cref="PowerButton"/> runs a <see cref="FakeBackend"/>
/// that only counts, and the Win32 seam only executes <see cref="PowerActionRules.Plan"/>.
/// </para>
/// </summary>
public class PowerActionTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

    private static readonly WidgetId[] PowerKinds =
        [new WidgetId("shutdown"), RestartConfig.Descriptor.Id, SleepConfig.Descriptor.Id, HibernateConfig.Descriptor.Id, LockConfig.Descriptor.Id, SignOutConfig.Descriptor.Id, ScreenOffConfig.Descriptor.Id];

    /// <summary>A backend that records what it was asked to run and answers what the test says.</summary>
    private sealed class FakeBackend(bool canHibernate = true) : IPowerBackend
    {
        public List<PowerAction> Ran { get; } = [];

        public PowerOutcome Outcome { get; set; } = PowerOutcome.Ok;

        /// <summary>When set, <see cref="Run"/> waits for it: a run in flight (sleep, until resume).</summary>
        public ManualResetEventSlim? Gate { get; set; }

        public bool CanHibernate { get; set; } = canHibernate;

        public PowerOutcome Run(PowerAction action)
        {
            lock (Ran)
            {
                Ran.Add(action);
            }

            Gate?.Wait(TimeSpan.FromSeconds(10));
            return Outcome;
        }
    }

    // ---- kinds -----------------------------------------------------------------------------

    [Fact]
    public void S1910_every_power_kind_maps_to_its_own_action_and_back()
    {
        var actions = PowerKinds.Select(k => PowerActionRules.ForKind(k)).ToList();

        Assert.All(actions, a => Assert.NotNull(a));
        Assert.Equal(PowerKinds.Length, actions.Distinct().Count());
        Assert.Equal(Enum.GetValues<PowerAction>().Order(), actions.Select(a => a!.Value).Order());
        Assert.Equal(PowerActionRules.All, actions.Select(a => a!.Value));

        foreach (var kind in PowerKinds)
        {
            Assert.Equal(kind, PowerActionRules.KindOf(PowerActionRules.ForKind(kind)!.Value));
        }
    }

    [Fact]
    public void S1910_no_other_kind_is_a_power_widget()
    {
        foreach (var kind in ModuleCatalog.Widgets.Select(w => w.Id).ToArray().Except(PowerKinds))
        {
            Assert.Null(PowerActionRules.ForKind(kind));
        }
    }

    [Fact]
    public void S1910_each_kind_has_a_config_of_its_own_that_names_its_action()
    {
        foreach (var kind in PowerKinds)
        {
            var config = Assert.IsAssignableFrom<PowerButtonConfig>(WidgetConfig.CreateDefault(kind));
            Assert.Equal(kind, config.Kind);
            Assert.Equal(PowerActionRules.ForKind(kind), config.Action);
            Assert.Equal(config.GetType(), config.Clone().GetType());
        }
    }

    [Fact]
    public async Task S1910_a_power_widget_round_trips_as_its_own_kind()
    {
        var dir = Path.Combine(Path.GetTempPath(), "themia-tests", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "state.json");

        try
        {
            var state = AppState.CreateDefault();
            var widget = state.ActiveView().Widgets[0];
            widget.Config = new SignOutConfig();

            var store = new AppStateStore(path, TimeSpan.Zero);
            store.SaveSoon(state);
            await store.FlushAsync();

            var loaded = new AppStateStore(path, TimeSpan.Zero).Load();
            var config = Assert.IsType<SignOutConfig>(loaded.State.ActiveView().Widgets[0].Config);
            Assert.Equal(PowerAction.SignOut, config.Action);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void S1911_power_widgets_start_without_a_header()
    {
        foreach (var kind in PowerKinds)
        {
            Assert.Equal(HeaderPlacement.Hidden, WidgetInstance.DefaultHeader(kind));
        }
    }

    [Fact]
    public void S1910_power_widgets_need_no_setting_up()
    {
        foreach (var kind in PowerKinds)
        {
            Assert.False(WidgetSetup.Needed(WidgetConfig.CreateDefault(kind), FeatureSet.None), kind.ToString());
        }
    }

    // ---- arming ----------------------------------------------------------------------------

    [Theory]
    [InlineData(PowerAction.Shutdown, true)]
    [InlineData(PowerAction.Restart, true)]
    [InlineData(PowerAction.SignOut, true)]
    [InlineData(PowerAction.Hibernate, true)]
    [InlineData(PowerAction.Sleep, false)]
    [InlineData(PowerAction.Lock, false)]
    [InlineData(PowerAction.ScreenOff, false)]
    public void S1913_destructive_actions_arm_and_the_rest_run_at_once(PowerAction action, bool arms)
        => Assert.Equal(arms, PowerActionRules.NeedsArming(action));

    [Theory]
    [InlineData(PowerAction.Sleep)]
    [InlineData(PowerAction.Lock)]
    [InlineData(PowerAction.ScreenOff)]
    public async Task S1913_lock_sleep_and_display_off_run_on_one_click(PowerAction action)
    {
        var backend = new FakeBackend();
        var button = new PowerButton(action, backend);

        Assert.Equal(PowerPress.Ran, await button.PressAsync(T0));
        Assert.Equal([action], backend.Ran);
        Assert.False(button.IsArmed(T0));
    }

    [Theory]
    [InlineData(PowerAction.Shutdown)]
    [InlineData(PowerAction.Restart)]
    [InlineData(PowerAction.SignOut)]
    [InlineData(PowerAction.Hibernate)]
    public async Task S1913_a_destructive_action_arms_on_the_first_click_and_runs_on_the_second(PowerAction action)
    {
        var backend = new FakeBackend();
        var button = new PowerButton(action, backend);

        Assert.Equal(PowerPress.Armed, await button.PressAsync(T0));
        Assert.Empty(backend.Ran);
        Assert.True(button.IsArmed(T0.AddSeconds(1)));

        Assert.Equal(PowerPress.Ran, await button.PressAsync(T0.AddSeconds(1)));
        Assert.Equal([action], backend.Ran);
        Assert.False(button.IsArmed(T0.AddSeconds(1)));
    }

    [Fact]
    public async Task S1913_a_double_click_is_one_click()
    {
        var backend = new FakeBackend();
        var button = new PowerButton(PowerAction.Shutdown, backend);

        await button.PressAsync(T0);
        Assert.Equal(PowerPress.Ignored, await button.PressAsync(T0.AddMilliseconds(150)));
        Assert.Empty(backend.Ran);
        Assert.True(button.IsArmed(T0.AddMilliseconds(150)));

        Assert.Equal(PowerPress.Ran, await button.PressAsync(T0 + PowerButton.MinimumDelay));
        Assert.Single(backend.Ran);
    }

    [Fact]
    public async Task S1913_the_arm_runs_out_and_the_next_click_arms_again()
    {
        var backend = new FakeBackend();
        var button = new PowerButton(PowerAction.Restart, backend);

        await button.PressAsync(T0);
        var late = T0 + PowerButton.ArmWindow;

        Assert.False(button.IsArmed(late));
        Assert.Equal(0, button.ArmRemaining(late));
        Assert.True(button.Expire(late));
        Assert.False(button.Expire(late));

        Assert.Equal(PowerPress.Armed, await button.PressAsync(late));
        Assert.Empty(backend.Ran);
    }

    [Fact]
    public async Task S1913_the_ring_drains_with_the_window()
    {
        var button = new PowerButton(PowerAction.SignOut, new FakeBackend());

        Assert.Equal(0, button.ArmRemaining(T0));
        await button.PressAsync(T0);

        Assert.Equal(1, button.ArmRemaining(T0));
        Assert.Equal(0.5, button.ArmRemaining(T0 + (PowerButton.ArmWindow / 2)), 3);
    }

    [Fact]
    public async Task S1913_a_clock_that_went_backwards_is_not_a_second_click()
    {
        var backend = new FakeBackend();
        var button = new PowerButton(PowerAction.Shutdown, backend);

        await button.PressAsync(T0);

        Assert.False(button.IsArmed(T0.AddSeconds(-30)));
        Assert.Equal(PowerPress.Armed, await button.PressAsync(T0.AddSeconds(-30)));
        Assert.Empty(backend.Ran);
    }

    [Fact]
    public async Task S1913_leaving_the_widget_disarms_it()
    {
        var backend = new FakeBackend();
        var button = new PowerButton(PowerAction.SignOut, backend);

        await button.PressAsync(T0);
        button.Disarm();

        Assert.Equal(PowerPress.Armed, await button.PressAsync(T0.AddSeconds(1)));
        Assert.Empty(backend.Ran);
    }

    // ---- availability, busy, failure -------------------------------------------------------

    [Fact]
    public async Task S1914_hibernate_on_a_machine_that_cannot_is_unavailable_and_never_runs()
    {
        var backend = new FakeBackend(canHibernate: false);
        var button = new PowerButton(PowerAction.Hibernate, backend);

        Assert.False(button.Available);
        Assert.Equal(PowerPress.Unavailable, await button.PressAsync(T0));
        Assert.Equal(PowerPress.Unavailable, await button.PressAsync(T0.AddSeconds(1)));
        Assert.Empty(backend.Ran);
        Assert.False(button.IsArmed(T0.AddSeconds(1)));
    }

    [Fact]
    public async Task S1914_turning_hibernation_on_shows_on_refresh()
    {
        var backend = new FakeBackend(canHibernate: false);
        var button = new PowerButton(PowerAction.Hibernate, backend);

        backend.CanHibernate = true;
        button.Refresh();

        Assert.True(button.Available);
        Assert.Equal(PowerPress.Armed, await button.PressAsync(T0));
    }

    [Fact]
    public void S1914_only_hibernate_depends_on_the_machine()
    {
        foreach (var action in Enum.GetValues<PowerAction>())
        {
            Assert.Equal(action != PowerAction.Hibernate, PowerActionRules.Available(action, canHibernate: false));
            Assert.True(new PowerButton(action, new FakeBackend(canHibernate: false)).Available == (action != PowerAction.Hibernate));
        }
    }

    [Fact]
    public async Task S1916_a_press_while_the_action_runs_runs_nothing_more()
    {
        using var gate = new ManualResetEventSlim(false);
        var backend = new FakeBackend { Gate = gate };
        var button = new PowerButton(PowerAction.Sleep, backend);

        var first = button.PressAsync(T0);
        Assert.True(button.Running);
        Assert.Equal(PowerPress.Busy, await button.PressAsync(T0.AddSeconds(1)));

        gate.Set();
        Assert.Equal(PowerPress.Ran, await first);
        Assert.False(button.Running);
        Assert.Single(backend.Ran);
    }

    [Fact]
    public async Task S1915_a_failed_run_says_why_until_the_next_press()
    {
        var backend = new FakeBackend { Outcome = PowerActionRules.SuspendOutcome(false, hibernate: false) };
        var button = new PowerButton(PowerAction.Sleep, backend);

        await button.PressAsync(T0);
        Assert.Equal("Could not sleep: Windows refused to sleep", button.Failure);

        backend.Outcome = PowerOutcome.Ok;
        await button.PressAsync(T0.AddSeconds(1));
        Assert.Null(button.Failure);
    }

    [Fact]
    public void S1913_the_armed_label_says_what_the_next_click_does()
    {
        Assert.Equal("Click again to shut down", PowerActionRules.ArmedLabel(Locale.En, PowerAction.Shutdown));
        Assert.Equal("Click again to sign out", PowerActionRules.ArmedLabel(Locale.En, PowerAction.SignOut));
        Assert.Equal("Erneut klicken: Herunterfahren", PowerActionRules.ArmedLabel(Locale.De, PowerAction.Shutdown));
    }

    // ---- face ladder -----------------------------------------------------------------------

    [Fact]
    public void S1912_xs_is_the_icon_alone()
    {
        var plan = PowerFace.Plan(new WidgetArea(120, 120), 104, 104, "Shut down".Length, "Click again to shut down".Length);

        Assert.Null(plan.Label);
        Assert.Equal(0, plan.LabelSlot);
        Assert.InRange(plan.IconBox, PowerFace.MinBox, 104);
    }

    [Fact]
    public void S1912_from_s_up_the_name_sits_under_the_icon_and_both_grow()
    {
        var s = PowerFace.Plan(new WidgetArea(200, 150), 176, 126, 9, 24);
        var m = PowerFace.Plan(new WidgetArea(320, 240), 296, 216, 9, 24);
        var xl = PowerFace.Plan(new WidgetArea(800, 560), 776, 536, 9, 24);

        Assert.NotNull(s.Label);
        Assert.NotNull(m.Label);
        Assert.NotNull(xl.Label);
        Assert.True(s.IconBox < m.IconBox && m.IconBox <= xl.IconBox);
        Assert.True(s.Label!.Value.FontSize <= m.Label!.Value.FontSize);

        // Everything fits: the icon square, the gap and the label slot.
        foreach (var (plan, height) in new[] { (s, 126.0), (m, 216.0), (xl, 536.0) })
        {
            Assert.True(plan.IconBox + PowerFace.LabelGap + plan.LabelSlot <= height);
            Assert.True(plan.IconSize < plan.IconBox);
        }
    }

    [Fact]
    public void S1912_the_armed_sentence_keeps_the_face_still()
    {
        // The slot is planned for the longer of the two, so arming does not move the icon.
        var plan = PowerFace.Plan(new WidgetArea(200, 150), 176, 126, "Herunterfahren".Length, "Erneut klicken: Herunterfahren".Length);

        Assert.NotNull(plan.Label);
        Assert.True(plan.LabelSlot >= plan.Label!.Value.LineHeight);
        Assert.True(plan.LabelSlot >= plan.Armed.LineHeight * plan.ArmedLines || plan.ArmedLines == 1);
    }

    [Fact]
    public void S1912_an_unmeasured_box_is_the_icon_alone()
    {
        var plan = PowerFace.Plan(default, double.NaN, 0, 9, 0);

        Assert.Null(plan.Label);
        Assert.Equal(PowerFace.MinBox, plan.IconBox);
    }

    // ---- call plan -------------------------------------------------------------------------

    [Fact]
    public void S1243_Shutdown_and_restart_power_off_or_reboot_with_the_privilege_and_force_if_hung()
    {
        var shutdown = PowerActionRules.Plan(PowerAction.Shutdown);
        Assert.Equal(PowerCallKind.ExitWindows, shutdown.Kind);
        Assert.Equal(0x8u | 0x10u, shutdown.ExitFlags);
        Assert.True(shutdown.NeedsShutdownPrivilege);

        var restart = PowerActionRules.Plan(PowerAction.Restart);
        Assert.Equal(0x2u | 0x10u, restart.ExitFlags);
        Assert.True(restart.NeedsShutdownPrivilege);
    }

    [Fact]
    public void S1243_Sign_out_lock_and_display_off_need_no_privilege()
    {
        var signOut = PowerActionRules.Plan(PowerAction.SignOut);
        Assert.Equal(PowerCallKind.ExitWindows, signOut.Kind);
        Assert.Equal(0x10u, signOut.ExitFlags);
        Assert.False(signOut.NeedsShutdownPrivilege);

        Assert.Equal(new PowerCall(PowerCallKind.Lock, 0, false, false), PowerActionRules.Plan(PowerAction.Lock));
        Assert.Equal(new PowerCall(PowerCallKind.MonitorOff, 0, false, false), PowerActionRules.Plan(PowerAction.ScreenOff));
    }

    [Fact]
    public void S1243_Sleep_and_hibernate_suspend_with_the_privilege()
    {
        Assert.Equal(new PowerCall(PowerCallKind.Suspend, 0, false, true), PowerActionRules.Plan(PowerAction.Sleep));
        Assert.Equal(new PowerCall(PowerCallKind.Suspend, 0, true, true), PowerActionRules.Plan(PowerAction.Hibernate));
    }

    [Fact]
    public void S1243_Every_action_has_a_plan_label_and_verb_in_every_language()
    {
        foreach (var action in Enum.GetValues<PowerAction>())
        {
            _ = PowerActionRules.Plan(action);
            Assert.False(string.IsNullOrWhiteSpace(PowerActionRules.Label(action)));
            foreach (var locale in Enum.GetValues<Locale>())
            {
                Assert.False(string.IsNullOrWhiteSpace(PowerActionRules.Verb(locale, action)));
                Assert.False(string.IsNullOrWhiteSpace(PowerActionRules.Label(locale, action)));
                Assert.False(string.IsNullOrWhiteSpace(PowerActionRules.ArmedLabel(locale, action)));
            }
        }
    }

    [Fact]
    public void S1243_An_undefined_action_has_no_plan()
        => Assert.Throws<ArgumentOutOfRangeException>(() => PowerActionRules.Plan((PowerAction)99));

    // ---- privilege and failure mapping -----------------------------------------------------

    [Fact]
    public void S1243_Adjust_success_with_not_all_assigned_is_not_allowed()
    {
        // AdjustTokenPrivileges "succeeds" and assigns nothing: the only signal is the last error.
        Assert.Equal(PowerFailure.NotAllowed, PowerActionRules.PrivilegeOutcome(true, PowerActionRules.NotAllAssigned).Failure);
        Assert.True(PowerActionRules.PrivilegeOutcome(true, 0).Succeeded);
        Assert.Equal(PowerFailure.Win32, PowerActionRules.PrivilegeOutcome(false, PowerActionRules.AccessDenied).Failure);
        Assert.Equal(PowerFailure.NotAllowed, PowerActionRules.PrivilegeOutcome(false, PowerActionRules.PrivilegeNotHeld).Failure);
    }

    [Fact]
    public void S1246_Failures_name_the_action_and_the_reason()
    {
        Assert.Null(PowerActionRules.FailureMessage(PowerAction.Sleep, PowerOutcome.Ok));
        Assert.Equal("Could not hibernate: hibernation is turned off on this PC",
            PowerActionRules.FailureMessage(PowerAction.Hibernate, PowerActionRules.SuspendOutcome(false, hibernate: true)));
        Assert.Equal("Could not sleep: Windows refused to sleep",
            PowerActionRules.FailureMessage(PowerAction.Sleep, PowerActionRules.SuspendOutcome(false, hibernate: false)));
        Assert.Equal("Could not lock: access denied",
            PowerActionRules.FailureMessage(PowerAction.Lock, PowerOutcome.Error(5)));
        Assert.Equal("Could not restart: Windows error 87",
            PowerActionRules.FailureMessage(PowerAction.Restart, PowerOutcome.Error(87)));
        Assert.StartsWith("Could not shut down: this account is not allowed",
            PowerActionRules.FailureMessage(PowerAction.Shutdown, PowerActionRules.PrivilegeOutcome(true, PowerActionRules.NotAllAssigned)), StringComparison.Ordinal);
    }

    [Fact]
    public void S1246_Failures_are_in_the_language_and_codes_are_never_grouped()
    {
        Assert.Equal("Ruhezustand nicht möglich: der Ruhezustand ist auf diesem PC deaktiviert",
            PowerActionRules.FailureMessage(Locale.De, PowerAction.Hibernate, PowerActionRules.SuspendOutcome(false, hibernate: true)));
        Assert.Equal("Could not restart: Windows error 12345",
            PowerActionRules.FailureMessage(Locale.En, PowerAction.Restart, PowerOutcome.Error(12345)));
    }
}
