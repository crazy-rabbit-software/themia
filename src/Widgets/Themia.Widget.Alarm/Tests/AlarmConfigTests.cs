using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Tests;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Widget.Alarm.Tests;

/// <summary>[S-4620] One alarm widget's config: repaired loudly, round-tripped, its default and a deep clone. Moved with the widget from the module host's AlarmTests, unchanged in what they assert.</summary>
public sealed class AlarmConfigTests
{
    [Fact]
    public void S4620_a_hand_edited_file_is_repaired_loudly()
    {
        var config = (AlarmConfig)ConfigJson.Read("alarm", """
            { "kind": "alarm", "time": "25:00", "days": ["fri", "nope"], "on": true, "label": "  Wake  ",
              "sound": "Bell", "volume": 300, "snoozeMinutes": 0 }
            """);

        // Load validation has repaired it already; a second repair has nothing left to do.
        Assert.Empty(config.Repair());
        Assert.Equal("07:00", config.Time);
        Assert.Equal(["fri"], config.Days!);
        Assert.Equal("Wake", config.Label);
        Assert.True(config.On);
        Assert.Equal(100, config.Volume);
        Assert.Equal(AlarmConfig.MinSnooze, config.SnoozeMinutes);
    }

    [Fact]
    public void S4620_an_alarm_round_trips_through_the_file()
    {
        var config = (AlarmConfig)ConfigJson.Read("alarm", """
            { "kind": "alarm", "time": "06:45", "days": ["mon", "fri"], "on": true, "label": "Train", "volume": 40, "snoozeMinutes": 5 }
            """);

        Assert.Equal(("06:45", true, "Train", 40, 5), (config.Time, config.On, config.Label, config.Volume, config.SnoozeMinutes));
        Assert.Equal(["mon", "fri"], config.Days!);
        Assert.Equal(new AlarmPlan(6 * 60 + 45, AlarmDays.Bit(DayOfWeek.Monday) | AlarmDays.Bit(DayOfWeek.Friday), true), config.Plan);
        Assert.Equal(TimeSpan.FromMinutes(5), config.Snooze);
    }

    [Fact]
    public void S4620_a_new_widget_is_one_alarm_at_seven_switched_off_and_needs_no_setting_up()
    {
        var config = (AlarmConfig)WidgetConfig.CreateDefault(new WidgetId("alarm"));
        Assert.Equal("07:00", config.Time);
        Assert.Null(config.Days);
        Assert.False(config.On);
        Assert.Equal(AlarmSound.Bell, config.Sound);
        Assert.Equal(AlarmConfig.DefaultVolume, config.Volume);
        Assert.Equal(10, config.SnoozeMinutes);
        Assert.False(WidgetSetup.Needed(config, FeatureSet.None));
    }

    [Fact]
    public void S4620_a_clone_is_deep()
    {
        var config = new AlarmConfig { Days = ["mon"] };
        var clone = (AlarmConfig)config.Clone();
        clone.On = true;
        clone.Days![0] = "tue";
        Assert.False(config.On);
        Assert.Equal(["mon"], config.Days);
    }
}
