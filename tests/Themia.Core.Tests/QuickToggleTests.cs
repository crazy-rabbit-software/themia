using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// The quick-setting toggle widgets: kinds, names, the decoders of what Windows answers, the click
/// gate, the look and the face ladder. [S-3900..S-3915] Nothing here touches the machine.
/// </summary>
public class QuickToggleTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);

    private static IEnumerable<WidgetId> ToggleKinds
        => ModuleCatalog.Widgets.Select(w => w.Id).ToArray().Where(k => QuickToggleRules.ForKind(k) is not null);

    // ---- kinds ---------------------------------------------------------------------------------

    [Fact]
    public void S3900_every_toggle_has_its_own_kind_and_back()
    {
        Assert.Equal(Enum.GetValues<QuickToggle>().Order(), QuickToggleRules.All.Order());
        Assert.Equal(QuickToggleRules.All.Count, ToggleKinds.Count());

        foreach (var toggle in QuickToggleRules.All)
        {
            var kind = QuickToggleRules.KindOf(toggle);
            Assert.Equal(toggle, QuickToggleRules.ForKind(kind));
            Assert.Equal(toggle, ((QuickToggleConfig)WidgetConfig.CreateDefault(kind)).Toggle);
        }
    }

    [Fact]
    public void S3900_a_power_or_meter_kind_is_not_a_toggle()
    {
        Assert.Null(QuickToggleRules.ForKind(ShutdownConfig.Descriptor.Id));
        Assert.Null(QuickToggleRules.ForKind(BluetoothConfig.Descriptor.Id));
        Assert.Null(QuickToggleRules.ForKind(VolumeConfig.Descriptor.Id));
    }

    [Fact]
    public void S3900_every_toggle_config_round_trips_through_the_state_file()
    {
        foreach (var kind in ToggleKinds)
        {
            var config = WidgetConfig.CreateDefault(kind);
            var json = System.Text.Json.JsonSerializer.Serialize(config, StateJson.WidgetConfig);
            var back = System.Text.Json.JsonSerializer.Deserialize(json, StateJson.WidgetConfig);
            Assert.NotNull(back);
            Assert.Equal(kind, back!.Kind);
            Assert.Equal(kind, back.Clone().Kind);
        }
    }

    [Fact]
    public void S3901_a_new_toggle_has_no_header_and_needs_no_setup()
    {
        foreach (var kind in ToggleKinds)
        {
            Assert.Equal(HeaderPlacement.Hidden, WidgetInstance.DefaultHeader(kind));
            Assert.False(WidgetSetup.Needed(WidgetConfig.CreateDefault(kind), Themia.Core.Accounts.AccountFeatures.None));
        }
    }

    [Fact]
    public void S3901_names_and_picker_labels_are_set_in_every_language_and_the_picker_label_is_never_another_widgets_name()
    {
        foreach (var locale in Enum.GetValues<Locale>())
        {
            var labels = new HashSet<string>(StringComparer.Ordinal);
            foreach (var toggle in QuickToggleRules.All)
            {
                var name = QuickToggleRules.Name(locale, toggle);
                var label = Strings.Get(locale, QuickToggleRules.PickerLabelId(toggle));
                Assert.False(string.IsNullOrWhiteSpace(name));
                Assert.False(string.IsNullOrWhiteSpace(Strings.Get(locale, QuickToggleRules.DescriptionId(toggle))));
                Assert.NotEqual(name, label);
                Assert.True(labels.Add(label), $"{locale}: {label} twice");
            }
        }
    }

    // ---- reading and look ----------------------------------------------------------------------

    [Fact]
    public void S3903_only_a_known_unblocked_reading_can_switch()
    {
        Assert.True(ToggleReading.Of(true).CanSwitch);
        Assert.True(ToggleReading.Of(false).CanSwitch);
        Assert.False(ToggleReading.Pending.CanSwitch);
        Assert.False(ToggleReading.Blocked(ToggleBlock.NoHardware).CanSwitch);
        Assert.False(new ToggleReading(true, ToggleBlock.NotNow).CanSwitch);
    }

    [Fact]
    public void S3904_the_look_follows_the_reading_and_a_pending_switch_shows_at_once()
    {
        Assert.Equal(ToggleLook.On, QuickToggleRules.Look(ToggleReading.Of(true)));
        Assert.Equal(ToggleLook.Off, QuickToggleRules.Look(ToggleReading.Of(false)));
        Assert.Equal(ToggleLook.Unknown, QuickToggleRules.Look(ToggleReading.Pending));
        Assert.Equal(ToggleLook.Unavailable, QuickToggleRules.Look(ToggleReading.Blocked(ToggleBlock.NoSwitch)));
        Assert.Equal(ToggleLook.Unavailable, QuickToggleRules.Look(new ToggleReading(true, ToggleBlock.NotNow)));

        Assert.Equal(ToggleLook.On, QuickToggleRules.Look(ToggleReading.Of(false), pending: true));
        Assert.Equal(ToggleLook.Off, QuickToggleRules.Look(ToggleReading.Of(true), pending: false));

        // A switch on its way never hides that the thing went away meanwhile.
        Assert.Equal(ToggleLook.Unavailable, QuickToggleRules.Look(ToggleReading.Blocked(ToggleBlock.NoHardware), pending: true));
    }

    [Fact]
    public void S3904_the_tooltip_names_the_toggle_and_its_state_or_reason()
    {
        Assert.Equal("Wi-Fi: On", QuickToggleRules.Tooltip(Locale.En, QuickToggle.Wifi, ToggleReading.Of(true), ToggleLook.On, refused: false));
        Assert.Equal("Sound: Off", QuickToggleRules.Tooltip(Locale.En, QuickToggle.Sound, ToggleReading.Of(false), ToggleLook.Off, refused: false));
        Assert.Equal(
            "Bluetooth: This PC does not have it",
            QuickToggleRules.Tooltip(Locale.En, QuickToggle.Bluetooth, ToggleReading.Blocked(ToggleBlock.NoHardware), ToggleLook.Unavailable, refused: false));
        Assert.Equal(
            "Night light: Windows did not switch it",
            QuickToggleRules.Tooltip(Locale.En, QuickToggle.NightLight, ToggleReading.Of(false), ToggleLook.Off, refused: true));
    }

    [Fact]
    public void S3903_every_block_but_none_has_a_reason_in_every_language()
    {
        foreach (var locale in Enum.GetValues<Locale>())
        {
            foreach (var block in Enum.GetValues<ToggleBlock>())
            {
                var reason = QuickToggleRules.Reason(locale, block);
                Assert.Equal(block == ToggleBlock.None, reason is null);
            }

            Assert.True(QuickToggleRules.LongestStateChars(locale) > 0);
        }
    }

    // ---- the click gate ------------------------------------------------------------------------

    [Fact]
    public void S3905_a_click_asks_for_the_opposite_and_shows_it_until_windows_answers()
    {
        var gate = new ToggleGate();
        var off = ToggleReading.Of(false);

        Assert.True(gate.Press(off, T0));
        Assert.Equal(ToggleLook.On, gate.Look(off));

        gate.Settled(succeeded: true);
        Assert.Null(gate.Pending);
        Assert.False(gate.Refused);
        Assert.Equal(ToggleLook.On, gate.Look(ToggleReading.Of(true)));
    }

    [Fact]
    public void S3905_clicks_while_a_switch_is_on_its_way_do_nothing()
    {
        var gate = new ToggleGate();
        var on = ToggleReading.Of(true);

        Assert.False(gate.Press(on, T0));
        Assert.Null(gate.Press(on, T0 + TimeSpan.FromSeconds(1)));
        Assert.Null(gate.Press(on, T0 + TimeSpan.FromSeconds(5)));
        Assert.False(gate.Pending);
    }

    [Fact]
    public void S3905_a_double_click_is_one_switch()
    {
        var gate = new ToggleGate();

        Assert.True(gate.Press(ToggleReading.Of(false), T0));
        gate.Settled(succeeded: true);

        // The answer came fast; the second half of the double-click must not flip it back.
        Assert.Null(gate.Press(ToggleReading.Of(true), T0 + TimeSpan.FromMilliseconds(250)));
        Assert.False(gate.Press(ToggleReading.Of(true), T0 + ToggleGate.DoubleClick));
    }

    [Fact]
    public void S3905_a_refused_switch_says_so_until_the_next_click()
    {
        var gate = new ToggleGate();

        Assert.True(gate.Press(ToggleReading.Of(false), T0));
        gate.Settled(succeeded: false);
        Assert.True(gate.Refused);
        Assert.Equal(ToggleLook.Off, gate.Look(ToggleReading.Of(false)));

        Assert.True(gate.Press(ToggleReading.Of(false), T0 + TimeSpan.FromSeconds(2)));
        Assert.False(gate.Refused);
    }

    [Fact]
    public void S3905_an_unknown_or_blocked_toggle_takes_no_click()
    {
        var gate = new ToggleGate();
        Assert.Null(gate.Press(ToggleReading.Pending, T0));
        Assert.Null(gate.Press(ToggleReading.Blocked(ToggleBlock.TurnedOff), T0 + TimeSpan.FromSeconds(1)));
        Assert.Null(gate.Pending);
    }

    [Fact]
    public void S3905_a_clock_set_back_does_not_swallow_clicks_forever()
    {
        var gate = new ToggleGate();
        Assert.True(gate.Press(ToggleReading.Of(false), T0));
        gate.Settled(succeeded: true);
        Assert.False(gate.Press(ToggleReading.Of(true), T0 - TimeSpan.FromMinutes(5)));
    }

    // ---- the face ------------------------------------------------------------------------------

    private static ToggleFacePlan Face(double w, double h, int name = 13)
    {
        var area = new WidgetArea(w, h);
        return ToggleFace.Plan(area, w - 24, h - 24, name, 11);
    }

    [Fact]
    public void S3906_xs_is_the_icon_alone()
    {
        var plan = Face(120, 120);
        Assert.Null(plan.Name);
        Assert.Null(plan.State);
        Assert.InRange(plan.IconSize, ToggleFace.MinIcon, ToggleFace.MaxIcon);
    }

    [Fact]
    public void S3906_from_regular_width_the_name_shows_and_the_state_where_the_height_allows()
    {
        foreach (var tier in WidgetTiers.All.Where(t => t.Name != "XS"))
        {
            var plan = Face(tier.Width, tier.Height);
            Assert.True(plan.Name is not null, tier.Name);
            Assert.True(plan.State is not null, $"{tier.Name} has no state line");
        }
    }

    [Fact]
    public void S3906_a_wide_strip_puts_the_icon_beside_the_words()
    {
        Assert.True(Face(480, 160).Row);
        Assert.False(Face(320, 240).Row);
        Assert.False(Face(200, 400).Row);
    }

    [Fact]
    public void S3906_the_face_fits_its_box_at_every_tier()
    {
        foreach (var tier in WidgetTiers.All)
        {
            var w = tier.Width - 24;
            var h = tier.Height - 24;
            var plan = Face(tier.Width, tier.Height);
            var words = (plan.Name?.LineHeight ?? 0) + (plan.State?.LineHeight ?? 0);
            if (plan.Row)
            {
                Assert.True(plan.IconSize + ToggleFace.Gap <= w, tier.Name);
                Assert.True(Math.Max(plan.IconSize, words) <= h, tier.Name);
            }
            else
            {
                var gaps = (plan.Name is null ? 0 : ToggleFace.Gap) + (plan.State is null ? 0 : ToggleFace.Gap);
                Assert.True(plan.IconSize + gaps + words <= h, tier.Name);
                Assert.True(plan.IconSize <= w, tier.Name);
            }

            Assert.Equal(0, plan.IconSize % 2);
        }
    }

    [Fact]
    public void S3906_a_degenerate_box_still_plans_an_icon()
    {
        var plan = ToggleFace.Plan(new WidgetArea(double.NaN, 0), double.NaN, -5, 5, 5);
        Assert.Null(plan.Name);
        Assert.Equal(ToggleFace.MinIcon, plan.IconSize);
    }

    // ---- decoders ------------------------------------------------------------------------------

    [Fact]
    public void S3910_airplane_mode_is_the_opposite_of_the_radios_enabled()
    {
        Assert.False(QuickToggleRules.AirplaneFrom(0, 1));
        Assert.True(QuickToggleRules.AirplaneFrom(0, 0));
        Assert.Null(QuickToggleRules.AirplaneFrom(unchecked((int)0x80004005), 1));
    }

    [Fact]
    public void S3911_energy_saver_is_on_in_standard_and_high_savings()
    {
        Assert.False(QuickToggleRules.EnergySaverFrom(0));
        Assert.True(QuickToggleRules.EnergySaverFrom(1));
        Assert.True(QuickToggleRules.EnergySaverFrom(2));
        Assert.Null(QuickToggleRules.EnergySaverFrom(7));
    }

    [Fact]
    public void S3912_rotation_lock_reads_as_windows_own_handler_reads_it()
    {
        Assert.Equal(ToggleReading.Of(false), QuickToggleRules.Rotation(0x0));
        Assert.Equal(ToggleReading.Of(true), QuickToggleRules.Rotation(0x1));
        Assert.Equal(ToggleBlock.NoHardware, QuickToggleRules.Rotation(0x10).Block);
        Assert.Equal(ToggleBlock.NoHardware, QuickToggleRules.Rotation(0x20 | 0x1).Block);
        Assert.Equal(ToggleBlock.NoHardware, QuickToggleRules.Rotation(null).Block);
        Assert.Equal(ToggleBlock.NotNow, QuickToggleRules.Rotation(0x8).Block);
        Assert.Equal(ToggleBlock.NotNow, QuickToggleRules.Rotation(0x40).Block);
        Assert.Equal(ToggleBlock.NotNow, QuickToggleRules.Rotation(0x80).Block);
        Assert.Equal(ToggleBlock.NotNow, QuickToggleRules.Rotation(0x4).Block);

        // AR_SUPPRESSED is the shell's own and does not lock anything.
        Assert.Equal(ToggleReading.Of(false), QuickToggleRules.Rotation(0x2));
    }

    [Fact]
    public void S3913_do_not_disturb_is_on_while_any_profile_but_the_off_one_is_in_force()
    {
        Assert.False(QuickToggleRules.DoNotDisturbOn(QuickToggleRules.QuietOffProfile, QuickToggleRules.QuietOffProfile));
        Assert.True(QuickToggleRules.DoNotDisturbOn(QuickToggleRules.QuietPriorityProfile, QuickToggleRules.QuietOffProfile));
        Assert.True(QuickToggleRules.DoNotDisturbOn("Microsoft.QuietHoursProfile.AlarmsOnly", null));
        Assert.False(QuickToggleRules.DoNotDisturbOn("microsoft.quiethoursprofile.unrestricted", null));
        Assert.False(QuickToggleRules.DoNotDisturbOn(null, null));
    }

    [Fact]
    public void S3914_spatial_sound_is_off_for_no_format_or_guid_null()
    {
        Assert.True(QuickToggleRules.SpatialIsOff(null));
        Assert.True(QuickToggleRules.SpatialIsOff(""));
        Assert.True(QuickToggleRules.SpatialIsOff(QuickToggleRules.SpatialOff));
        Assert.True(QuickToggleRules.SpatialIsOff("not a guid"));
        Assert.False(QuickToggleRules.SpatialIsOff("{B53D940C-B846-4831-9F76-D102B9B725A0}"));
    }

    [Fact]
    public void S3915_a_dword_setting_reads_absent_as_its_default_and_anything_else_as_unknown()
    {
        Assert.True(QuickToggleRules.DwordSetting(null, absent: true));
        Assert.False(QuickToggleRules.DwordSetting(null, absent: false));
        Assert.True(QuickToggleRules.DwordSetting(1, absent: false));
        Assert.False(QuickToggleRules.DwordSetting(0, absent: true));
        Assert.Null(QuickToggleRules.DwordSetting("1", absent: false));
        Assert.Null(QuickToggleRules.DwordSetting(new byte[] { 1 }, absent: false));
    }
}
