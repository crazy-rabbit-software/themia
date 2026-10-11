using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// The footer a large tile adds under the drawing and its legend: under a day, the week that day
/// belongs to as seven small bars (a way through the widget's own days). No stats line and no
/// limit bar: the limit is the headline's "of 4h" already, and averages are not this tile's job.
/// </summary>
internal sealed partial class ScreenTimeView
{
    /// <summary>The drawing and its legend keep at least this much before a footer is considered.</summary>
    private const double BodyFloor = 200;

    /// <summary>The week strip's bars, without their weekday labels.</summary>
    private const double StripBars = 48;

    private static readonly Brush StripBrush = FluentLayout.Themed("AccentFillColorDefaultBrush", Colors.DodgerBlue);
    private static readonly Brush TrackBrush = FluentLayout.Themed("ControlStrongFillColorDisabledBrush", Colors.Gray);

    private readonly StackPanel _footer = new() { Spacing = Fluent.Space2 };

    /// <summary>
    /// [S-1213] [S-1214] Build the footer for this size and return the height it takes from the
    /// body, its gap included; 0 when the tile is not large both ways or the body would be squeezed.
    /// </summary>
    private double LayFooter(double width, double height, ScreenTimeStyle style)
    {
        _footer.Children.Clear();
        _footer.Visibility = Visibility.Collapsed;

        if (_source is null || !_area.AtLeast(SizeClass.Expanded, SizeClass.Expanded) || height - BodyFloor <= 0)
        {
            return 0;
        }

        var room = new Disclosure(width - (WidgetLayout.ContentInset * 2), height - BodyFloor, Fluent.Space2);
        var day = !_model.Window;
        var today = _source.Today;
        var shown = day && LocalDay.TryParse(_pinned, out var pinned) ? pinned : today;

        // The week strip, under a day only (a window already draws its days).
        if (day && style != ScreenTimeStyle.None && room.Take(StripBars + FluentType.Caption.LineHeight + 2))
        {
            List<(LocalDay Day, long Seconds)> week = [.. ScreenTimeRules.DaysEnding(shown, 7).Select(d => (Day: d, Seconds: DayTotal(d, today)))];
            _footer.Children.Add(WeekStrip(week, shown, today));
        }

        if (_footer.Children.Count == 0)
        {
            return 0;
        }

        _footer.Visibility = Visibility.Visible;
        _footer.Margin = new Thickness(WidgetLayout.ContentInset, 0, WidgetLayout.ContentInset, Fluent.Space2);
        return height - BodyFloor - room.Remaining + Fluent.Space2;
    }

    /// <summary>A day's total with hidden apps left out, as every other figure here is.</summary>
    private long DayTotal(LocalDay day, LocalDay today)
    {
        if (_source is null)
        {
            return 0;
        }

        var record = _source.Day(day == today ? null : day.Key);
        return ScreenTimeRules.Sum(ScreenTimeRules.Visible(record.Apps, _model.Hidden));
    }

    /// <summary>
    /// [S-1214] The week ending on the shown day: one bar per day scaled to the busiest, the shown day
    /// in the accent and the rest dimmed, weekday under each. Clicking a day shows it.
    /// </summary>
    private Grid WeekStrip(IReadOnlyList<(LocalDay Day, long Seconds)> week, LocalDay shown, LocalDay today)
    {
        var busiest = Math.Max(1, week.Max(d => d.Seconds));
        var strip = new Grid { ColumnSpacing = Fluent.Space2, Height = StripBars + FluentType.Caption.LineHeight + 2 };
        var wide = (_area.Width - (WidgetLayout.ContentInset * 2)) / Math.Max(1, week.Count) >= 36;

        for (var i = 0; i < week.Count; i++)
        {
            var (date, seconds) = week[i];
            strip.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var column = new Grid { Background = new SolidColorBrush(Colors.Transparent) };
            column.RowDefinitions.Add(new RowDefinition { Height = new GridLength(StripBars) });
            column.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var track = new Border { Background = TrackBrush, Opacity = 0.35, CornerRadius = new CornerRadius(2) };
            column.Children.Add(track);

            if (seconds > 0)
            {
                column.Children.Add(new Border
                {
                    Height = Math.Max(2, (double)seconds / busiest * StripBars),
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Background = StripBrush,
                    Opacity = date == shown ? 1 : 0.45,
                    CornerRadius = new CornerRadius(2),
                });
            }

            var label = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
            label.Text = wide ? DayShort(date.DayOfWeek) : DayLetter(date.DayOfWeek);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.TextTrimming = TextTrimming.None;
            label.Margin = new Thickness(0, 2, 0, 0);

            if (date == shown)
            {
                label.Foreground = TodayLabelBrush;
            }

            Grid.SetRow(label, 1);
            column.Children.Add(label);

            ToolTipService.SetToolTip(column, $"{Formatting.DayHeading(date.Date, today.Date)} — {ScreenTimeRules.FormatDuration(seconds)}");

            // [S-454] The strip is another way through the days: today unpins, any other day pins.
            // Days past the navigation's reach stay a tooltip.
            var key = date == today ? null : date.Key;
            if (date.DaysUntil(today) <= ScreenTimeRules.MaxHistoryOffset)
            {
                column.Tapped += (_, e) =>
                {
                    e.Handled = true;
                    _pinned = key;
                    _focus = null;
                    Render();
                };
            }

            Grid.SetColumn(column, i);
            strip.Children.Add(column);
        }

        return strip;
    }
}
