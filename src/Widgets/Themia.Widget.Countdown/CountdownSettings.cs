using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Windows.System;
using Themia.Widget.Countdown;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-6037] The Countdown settings rows: the one target's name, its date and time, and its format.
/// <para>
/// Rows write straight to the live config and save as they go, like every other section: the widget
/// is on the desktop behind this window, and a setting you cannot see take effect is a setting you
/// cannot judge. Nothing here keeps a copy of a config value — the controls are seeded from the
/// config once and every handler writes back to it, so there is no second copy to fall out of step.
/// </para>
/// </summary>
internal static class CountdownSettings
{
    /// <summary>The formats in picker order, named in the current language (the page is rebuilt when it changes).</summary>
    private static IReadOnlyList<(CountdownFormat Value, string Label)> CountdownFormats() =>
    [
        .. new[] { CountdownFormat.Auto, CountdownFormat.Days, CountdownFormat.DaysHoursMinutes, CountdownFormat.DaysHoursMinutesSeconds }
            .Select(format => (format, CountdownFormatter.FormatLabel(format, Strings.Current))),
    ];

    public static void Rows(List<FrameworkElement> rows, CountdownConfig config, Action save)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(save);

        rows.Add(FluentLayout.Header(CountdownStr.Widget_Countdown_Label.Text()));

        var label = new TextBox
        {
            Text = config.Label,
            PlaceholderText = CountdownFormatter.Untitled(Strings.Current),
            MaxLength = CountdownConfig.MostLabel,
            MinWidth = FluentLayout.ChoiceMinWidth,
        };

        // [C-54] On lost focus and on Enter, never per keystroke: a rebuild between two letters
        // takes the caret with it, and a save per letter writes the file a dozen times a word.
        void CommitLabel()
        {
            var text = label.Text.Trim();
            if (text == config.Label)
            {
                return;
            }

            config.Label = text;
            save();
        }

        label.LostFocus += (_, _) => CommitLabel();
        label.KeyDown += (_, args) =>
        {
            if (args.Key == VirtualKey.Enter)
            {
                CommitLabel();
                args.Handled = true;
            }
        };

        rows.Add(FluentLayout.Row(Glyphs.Rename, CountdownStr.WidgetsA_Countdown_Settings_Name_Label.Text(), null, label, "name", "label", "title"));

        var zone = TimeZoneInfo.Local;

        // Not set yet ([S-1860]): no date, and midnight, so a picked day counts to its start.
        var date = new CalendarDatePicker
        {
            Date = config.Target is { } target ? CountdownFormatter.LocalDate(target, zone) : null,
            PlaceholderText = CountdownStr.WidgetsA_Countdown_Settings_PickDate.Text(),
        };

        var time = new TimePicker
        {
            SelectedTime = config.Target is { } at ? CountdownFormatter.LocalTimeOfDay(at, zone) : TimeSpan.Zero,
            ClockIdentifier = LocaleFormat.Uses24Hour(Strings.Current) ? "24HourClock" : "12HourClock",
            MinuteIncrement = 1,
        };

        void CommitTarget()
        {
            // [S-144] [C-55] A cleared or impossible value leaves the target exactly as it was.
            if (CountdownFormatter.CombineLocal(date.Date, time.SelectedTime, zone) is not { } next || next == config.Target)
            {
                return;
            }

            // [S-1448] A new target: its progress runs from now.
            config.Retarget(next, DateTimeOffset.Now);
            save();
        }

        date.DateChanged += (_, _) => CommitTarget();
        time.SelectedTimeChanged += (_, _) => CommitTarget();

        rows.Add(FluentLayout.Row(Glyphs.Calendar, CountdownStr.WidgetsA_Countdown_Settings_Target_Label.Text(), CountdownStr.WidgetsA_Countdown_Settings_Target_Desc.Text(),
            new StackPanel { Orientation = Orientation.Horizontal, Spacing = Fluent.Space2, Children = { date, time } },
            "date", "time", "target", "when"));

        rows.Add(FluentLayout.Row(Glyphs.Countdown, CountdownStr.WidgetsA_Countdown_Settings_Format_Label.Text(), CountdownStr.WidgetsA_Countdown_Settings_Format_Desc.Text(),
            FluentLayout.Choice(CountdownFormats(), config.Format, value =>
            {
                config.Format = value;
                save();
            }),
            "format", "seconds", "days"));
    }
}
