using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Widget.Countdown;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-6037] The Countdown's options, all on its properties menu ([S-3719]): Appearance ([S-3723]),
/// the one target's name, its date and its time (typed, [S-3738]), and its format. Every line writes
/// the live config and saves; nothing keeps a copy of a value. It has no settings page.
/// </summary>
internal static class CountdownMenu
{
    /// <summary>The formats in their order, named in the menu's language.</summary>
    private static (CountdownFormat Value, string Label)[] Formats() =>
    [
        .. new[] { CountdownFormat.Auto, CountdownFormat.Days, CountdownFormat.DaysHoursMinutes, CountdownFormat.DaysHoursMinutesSeconds }
            .Select(format => (format, CountdownFormatter.FormatLabel(format, Strings.Current))),
    ];

    public static void Lines(WidgetMenuLines menu, CountdownConfig config) => Lines(menu, config, TimeZoneInfo.Local, () => DateTimeOffset.Now);

    /// <param name="zone">The zone the target's day and time are read in: the machine's.</param>
    /// <param name="now">When a new target is set, for its progress ([S-1448]).</param>
    internal static void Lines(WidgetMenuLines menu, CountdownConfig config, TimeZoneInfo zone, Func<DateTimeOffset> now)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(zone);
        ArgumentNullException.ThrowIfNull(now);

        var locale = Strings.Current;
        menu.PlaceCard();

        menu.Lines.Add(new ItemMenuEntry(Glyphs.Rename, CountdownStr.WidgetsA_Countdown_Settings_Name_Label.Text(), () => menu.Ask(new WidgetTextAsk(
            CountdownStr.WidgetsA_Countdown_Settings_Name_Label.Text(),
            CountdownFormatter.Untitled(locale),
            Str.Common_Save.Text(),
            NameRefusal,
            typed =>
            {
                var name = typed.Trim();
                if (name != config.Label)
                {
                    config.Label = name;
                    menu.Save();
                }
            },
            Initial: config.Label,
            AllowEmpty: true)), Value: config.Label.Length > 0 ? config.Label : null));

        // Not set yet ([S-1860]): no day, and midnight, so a typed day counts to its start.
        DateOnly? day = config.Target is { } target ? DateOnly.FromDateTime(CountdownFormatter.LocalDate(target, zone).DateTime) : null;
        var time = config.Target is { } at ? CountdownFormatter.LocalTimeOfDay(at, zone) : TimeSpan.Zero;

        void Retarget(DateOnly newDay, TimeSpan newTime)
        {
            // [S-144] An impossible (DST-skipped) moment leaves the target exactly as it was.
            var midnight = new DateTimeOffset(newDay.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            if (CountdownFormatter.CombineLocal(midnight, newTime, zone) is not { } next || next == config.Target)
            {
                return;
            }

            // [S-1448] A new target: its progress runs from now.
            config.Retarget(next, now());
            menu.Save();
        }

        // [S-1862] A countdown waits for its day: Set up asks for it.
        menu.Lines.Add(WidgetMenuLines.SetsUp(new ItemMenuEntry(Glyphs.Calendar, CountdownStr.WidgetsA_Countdown_Menu_Date.Text(), () => menu.Ask(new WidgetTextAsk(
            CountdownStr.WidgetsA_Countdown_Menu_Date.Text(),
            "2026-12-24",
            Str.Common_Save.Text(),
            typed => TypedTime.Date(typed) is null ? CountdownStr.WidgetsA_Countdown_Menu_DateRefused.Text() : null,
            typed =>
            {
                if (TypedTime.Date(typed) is { } picked)
                {
                    Retarget(picked, time);
                }
            },
            Initial: day is { } shown ? TypedTime.DateText(shown) : null)),
            Value: day is { } d ? LocaleFormat.Date(locale, d, DateStyle.Medium) : null)));

        // The time needs a day to fall on: greyed until there is one.
        menu.Lines.Add(new ItemMenuEntry(Glyphs.Clock, CountdownStr.WidgetsA_Countdown_Menu_Time.Text(), () => menu.Ask(new WidgetTextAsk(
            CountdownStr.WidgetsA_Countdown_Menu_Time.Text(),
            "18:00",
            Str.Common_Save.Text(),
            typed => TypedTime.TimeOfDay(typed) is null ? CountdownStr.WidgetsA_Countdown_Menu_TimeRefused.Text() : null,
            typed =>
            {
                if (day is { } onDay && TypedTime.TimeOfDay(typed) is { } minutes)
                {
                    Retarget(onDay, TimeSpan.FromMinutes(minutes));
                }
            },
            Initial: AlarmTimes.Text((int)time.TotalMinutes))),
            Disabled: day is null,
            Value: day is null ? null : LocaleFormat.Time(locale, TimeOnly.FromTimeSpan(time))));

        menu.Lines.Add(menu.Choice(Glyphs.Countdown, CountdownStr.WidgetsA_Countdown_Settings_Format_Label.Text(), Formats(), config.Format, value => config.Format = value));
    }

    /// <summary>Why a typed name cannot be the countdown's, or null: longer than <see cref="CountdownConfig.MostLabel"/>.</summary>
    internal static string? NameRefusal(string typed)
        => (typed ?? string.Empty).Trim().Length > CountdownConfig.MostLabel
            ? CountdownStr.WidgetsA_Countdown_Menu_NameTooLong.Format(("max", CountdownConfig.MostLabel))
            : null;
}
