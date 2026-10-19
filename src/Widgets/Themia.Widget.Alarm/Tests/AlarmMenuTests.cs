using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.Alarm.Tests;

/// <summary>[S-4631] The Alarm's options on its properties menu: the time typed, the switch, the name, the days, the sound, its level, the snooze, the note.</summary>
public sealed class AlarmMenuTests
{
    private int _saves;
    private WidgetTextAsk? _asked;

    private WidgetMenuLines Build(AlarmConfig config)
    {
        var lines = new WidgetMenuLines(() => _saves++, ask => _asked = ask);
        AlarmMenu.Lines(lines, config);
        return lines;
    }

    private static ItemMenuEntry Line(WidgetMenuLines lines, string text) => lines.Lines.Single(l => l.Text == text);

    [Fact]
    public void S4631_appearance_then_when_it_rings_what_it_says_how_it_sounds_and_the_note_each_apart()
    {
        var lines = Build(new AlarmConfig());

        Assert.Equal(0, lines.CardAt);
        Assert.Equal(
            [
                AlarmStr.Alarm_Settings_Time_Label.Text(), AlarmStr.Alarm_Settings_Repeat.Text(), AlarmStr.Alarm_Settings_On_Label.Text(),
                "|", AlarmStr.Alarm_Settings_Name_Label.Text(),
                "|", AlarmStr.Alarm_Settings_Sound_Label.Text(), AlarmStr.Alarm_Settings_Volume_Label.Text(), AlarmStr.Alarm_Settings_Snooze_Label.Text(),
                "|", AlarmStr.Alarm_Settings_WhileRunning.Text(),
            ],
            lines.Lines.Select(l => l.IsSeparator ? "|" : l.Text));
        Assert.True(lines.Lines[^1].Note);
    }

    [Fact]
    public void S4631_no_volume_while_the_sound_is_off()
        => Assert.DoesNotContain(Build(new AlarmConfig { Sound = AlarmSound.Off }).Lines, l => l.Text == AlarmStr.Alarm_Settings_Volume_Label.Text());

    [Fact]
    public void S4631_a_typed_time_is_stored_as_the_file_writes_it_and_saves_once()
    {
        var config = new AlarmConfig();
        Line(Build(config), AlarmStr.Alarm_Settings_Time_Label.Text()).Action!();
        var ask = Assert.IsType<WidgetTextAsk>(_asked);

        Assert.Equal("07:00", ask.Initial);
        Assert.NotNull(ask.Refuse("25:00"));
        Assert.Null(ask.Refuse("6:45 pm"));

        ask.Done("6:45 pm");
        Assert.Equal("18:45", config.Time);
        Assert.Equal(1, _saves);
    }

    [Fact]
    public void S4631_the_time_it_holds_typed_again_saves_nothing()
    {
        Line(Build(new AlarmConfig { Time = "07:00" }), AlarmStr.Alarm_Settings_Time_Label.Text()).Action!();
        _asked!.Done("7:00");

        Assert.Equal(0, _saves);
    }

    [Fact]
    public void S4631_a_name_longer_than_forty_is_refused_and_an_empty_one_clears_it()
    {
        var config = new AlarmConfig { Label = "Gym" };
        Line(Build(config), AlarmStr.Alarm_Settings_Name_Label.Text()).Action!();
        var ask = _asked!;

        Assert.True(ask.AllowEmpty);
        Assert.NotNull(ask.Refuse(new string('x', AlarmConfig.MostName + 1)));
        Assert.Null(ask.Refuse(new string('x', AlarmConfig.MostName)));

        ask.Done("  ");
        Assert.Equal(string.Empty, config.Label);
        Assert.Equal(1, _saves);
    }

    [Fact]
    public void S4631_a_day_switch_adds_the_day_and_the_line_says_the_days()
    {
        var config = new AlarmConfig { Days = ["mon"] };
        var repeat = Line(Build(config), AlarmStr.Alarm_Settings_Repeat.Text());
        Assert.Equal(AlarmWords.Days(Strings.Current, AlarmDays.Mask(["mon"])), repeat.Value);

        var tuesday = LocaleFormat.WeekdayName(Strings.Current, DayOfWeek.Tuesday, NameWidth.Wide);
        repeat.Children!.Single(l => l.Text == tuesday).Action!();

        Assert.Equal(new[] { "mon", "tue" }, config.Days);
        Assert.Equal(1, _saves);
    }

    [Fact]
    public void S4631_the_last_day_off_rings_once_again()
    {
        var config = new AlarmConfig { Days = ["mon"] };
        var monday = LocaleFormat.WeekdayName(Strings.Current, DayOfWeek.Monday, NameWidth.Wide);
        Line(Build(config), AlarmStr.Alarm_Settings_Repeat.Text()).Children!.Single(l => l.Text == monday).Action!();

        Assert.Null(config.Days);
        Assert.Null(Line(Build(config), AlarmStr.Alarm_Settings_Repeat.Text()).Value);
    }

    [Fact]
    public void S4631_a_sound_picked_is_stored_once_and_the_one_it_holds_saves_nothing()
    {
        var config = new AlarmConfig { Sound = AlarmSound.Bell };
        var sound = Line(Build(config), AlarmStr.Alarm_Settings_Sound_Label.Text());

        sound.Children!.Single(l => l.Checked == true).Action!();
        Assert.Equal(0, _saves);

        sound.Children!.Single(l => l.Text == PomodoroSounds.Label(AlarmSound.Gong)).Action!();
        Assert.Equal(AlarmSound.Gong, config.Sound);
        Assert.Equal(1, _saves);
    }

    [Fact]
    public void S4631_the_volume_in_tens_and_the_snooze_typed_from_1_to_30()
    {
        var config = new AlarmConfig();
        var lines = Build(config);

        var volume = Line(lines, AlarmStr.Alarm_Settings_Volume_Label.Text());
        Assert.Equal(11, volume.Children!.Count);

        Line(lines, AlarmStr.Alarm_Settings_Snooze_Label.Text()).Action!();
        Assert.NotNull(_asked!.Refuse("0"));
        Assert.NotNull(_asked.Refuse("31"));
        _asked.Done("5");
        Assert.Equal(5, config.SnoozeMinutes);
    }
}
