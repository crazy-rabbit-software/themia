using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.Countdown.Tests;

/// <summary>[S-6037] The Countdown's options on its properties menu: the name, the target's day and time typed, the format.</summary>
public sealed class CountdownMenuTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 9, 0, 0, TimeSpan.Zero);

    private int _saves;
    private WidgetTextAsk? _asked;

    private WidgetMenuLines Build(CountdownConfig config)
    {
        var lines = new WidgetMenuLines(() => _saves++, ask => _asked = ask);
        CountdownMenu.Lines(lines, config, TimeZoneInfo.Utc, () => Now);
        return lines;
    }

    private ItemMenuEntry Line(CountdownConfig config, string text) => Build(config).Lines.Single(l => l.Text == text);

    private static string Date => CountdownStr.WidgetsA_Countdown_Menu_Date.Text();

    private static string Time => CountdownStr.WidgetsA_Countdown_Menu_Time.Text();

    [Fact]
    public void S6037_appearance_then_name_date_time_and_format()
    {
        var lines = Build(new CountdownConfig());

        Assert.Equal(0, lines.CardAt);
        Assert.Equal(
            [CountdownStr.WidgetsA_Countdown_Settings_Name_Label.Text(), Date, Time, CountdownStr.WidgetsA_Countdown_Settings_Format_Label.Text()],
            lines.Lines.Select(l => l.Text));
    }

    [Fact]
    public void S6037_without_a_target_the_time_is_greyed_and_a_typed_day_counts_to_its_midnight()
    {
        var config = new CountdownConfig();
        Assert.True(Line(config, Time).Disabled);

        Line(config, Date).Action!();
        Assert.Null(_asked!.Initial);
        _asked.Done("24.12.2026");

        Assert.Equal(new DateTimeOffset(2026, 12, 24, 0, 0, 0, TimeSpan.Zero), config.Target);
        Assert.Equal(Now, config.Created);
        Assert.Equal(1, _saves);
    }

    [Fact]
    public void S6037_a_new_day_keeps_the_time_and_a_new_time_keeps_the_day()
    {
        var config = new CountdownConfig { Target = new DateTimeOffset(2026, 12, 24, 18, 30, 0, TimeSpan.Zero) };

        Line(config, Date).Action!();
        Assert.Equal("2026-12-24", _asked!.Initial);
        _asked.Done("2026-12-31");
        Assert.Equal(new DateTimeOffset(2026, 12, 31, 18, 30, 0, TimeSpan.Zero), config.Target);

        Line(config, Time).Action!();
        Assert.Equal("18:30", _asked!.Initial);
        _asked.Done("11:59 pm");
        Assert.Equal(new DateTimeOffset(2026, 12, 31, 23, 59, 0, TimeSpan.Zero), config.Target);
        Assert.Equal(2, _saves);
    }

    [Fact]
    public void S6037_the_target_it_holds_typed_again_saves_nothing()
    {
        var config = new CountdownConfig { Target = new DateTimeOffset(2026, 12, 24, 18, 30, 0, TimeSpan.Zero), Created = Now.AddDays(-1) };
        Line(config, Date).Action!();
        _asked!.Done("2026-12-24");

        Assert.Equal(0, _saves);
        Assert.Equal(Now.AddDays(-1), config.Created);
    }

    [Fact]
    public void S6037_a_date_or_time_that_is_none_is_refused_with_the_reason()
    {
        var config = new CountdownConfig { Target = Now.AddDays(3) };

        Line(config, Date).Action!();
        Assert.NotNull(_asked!.Refuse("12/11/2026"));
        Assert.Null(_asked.Refuse("2026-11-12"));

        Line(config, Time).Action!();
        Assert.NotNull(_asked!.Refuse("25:00"));
    }

    [Fact]
    public void S6037_a_name_over_sixty_is_refused_and_an_empty_one_clears_it()
    {
        var config = new CountdownConfig { Label = "Launch" };
        Line(config, CountdownStr.WidgetsA_Countdown_Settings_Name_Label.Text()).Action!();

        Assert.NotNull(_asked!.Refuse(new string('x', CountdownConfig.MostLabel + 1)));
        _asked.Done(string.Empty);
        Assert.Equal(string.Empty, config.Label);
    }

    [Fact]
    public void S6037_a_format_picked_is_stored_once()
    {
        var config = new CountdownConfig();
        Line(config, CountdownStr.WidgetsA_Countdown_Settings_Format_Label.Text()).Children!
            .Single(l => l.Text == CountdownFormatter.FormatLabel(CountdownFormat.Days, Themia.Core.Localization.Strings.Current)).Action!();

        Assert.Equal(CountdownFormat.Days, config.Format);
        Assert.Equal(1, _saves);
    }
}
