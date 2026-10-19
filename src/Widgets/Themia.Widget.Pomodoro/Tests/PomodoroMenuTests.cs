using Themia.App.Shell.Config;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.Pomodoro.Tests;

/// <summary>[S-8110] The Pomodoro's options on its properties menu: the two lengths typed, the sound and its level.</summary>
public sealed class PomodoroMenuTests
{
    private int _saves;
    private WidgetTextAsk? _asked;

    private WidgetMenuLines Build(PomodoroConfig config)
    {
        var lines = new WidgetMenuLines(() => _saves++, ask => _asked = ask);
        PomodoroMenu.Lines(lines, config);
        return lines;
    }

    [Fact]
    public void S8110_appearance_then_the_lengths_then_the_sound_and_its_volume()
    {
        var lines = Build(new PomodoroConfig());

        Assert.Equal(0, lines.CardAt);
        Assert.Empty(lines.StyleLines);
        Assert.Equal(
            [
                PomodoroStr.Pomodoro_FocusLength_Label.Text(), PomodoroStr.Pomodoro_BreakLength_Label.Text(),
                PomodoroStr.Pomodoro_AlarmSound_Label.Text(), PomodoroStr.Pomodoro_AlarmVolume_Label.Text(),
            ],
            lines.Lines.Where(l => !l.IsSeparator).Select(l => l.Text));
    }

    [Fact]
    public void S8110_no_volume_while_the_sound_is_off()
        => Assert.DoesNotContain(Build(new PomodoroConfig { Sound = AlarmSound.Off }).Lines, l => l.Text == PomodoroStr.Pomodoro_AlarmVolume_Label.Text());

    [Fact]
    public void S8110_a_length_out_of_range_on_disk_shows_the_one_in_use()
        => Assert.Equal(PomodoroMenu.Minutes(180), Build(new PomodoroConfig { WorkMinutes = 900 }).Lines[0].Value);

    [Fact]
    public void S8110_the_break_is_typed_from_1_to_180_and_saves_once()
    {
        var config = new PomodoroConfig();
        Build(config).Lines[1].Action!();
        var ask = Assert.IsType<WidgetTextAsk>(_asked);

        Assert.NotNull(ask.Refuse("0"));
        Assert.NotNull(ask.Refuse("181"));
        Assert.Null(ask.Refuse("15"));

        ask.Done("15");
        Assert.Equal(15, config.BreakMinutes);
        Assert.Equal(1, _saves);
    }

    [Fact]
    public void S8110_a_sound_picked_is_stored_once_and_the_one_it_holds_saves_nothing()
    {
        var config = new PomodoroConfig();
        var sound = Build(config).Lines.Single(l => l.Text == PomodoroStr.Pomodoro_AlarmSound_Label.Text());

        sound.Children!.Single(l => l.Checked == true).Action!();
        Assert.Equal(0, _saves);

        sound.Children!.Single(l => l.Text == PomodoroSounds.Label(AlarmSound.Off)).Action!();
        Assert.Equal(AlarmSound.Off, config.Sound);
        Assert.Equal(1, _saves);
    }
}
