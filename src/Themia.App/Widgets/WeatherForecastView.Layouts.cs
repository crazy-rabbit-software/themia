using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>The forecast's rows and columns. [S-725]..[S-727]</summary>
internal sealed partial class WeatherForecastView
{
    /// <summary>
    /// The rows' padding: 4 above, so today's highlight does not butt against the header, and the
    /// rest of <see cref="WeatherForecastLayout.ListPadding"/> below.
    /// </summary>
    private static readonly Thickness ListInset = new(0, Fluent.Space1, 0, WeatherForecastLayout.ListPadding - Fluent.Space1);

    // ---- Rows [S-725] ------------------------------------------------------------------------

    /// <summary>
    /// [S-31] The days that have room, one star row each, so the list fills the tile edge to edge (up
    /// to the row's cap). Never a scroller: a day that does not fit whole is left out ([S-725]).
    /// </summary>
    private Grid BuildRows(IReadOnlyList<ForecastDay> days, DateOnly today, Range range, ForecastRowsPlan plan)
    {
        var count = Math.Min(plan.Days, days.Count);
        var grid = new Grid
        {
            Padding = ListInset,
            VerticalAlignment = VerticalAlignment.Top,
            Height = (plan.RowHeight * count) + WeatherForecastLayout.ListPadding,
        };

        for (var i = 0; i < count; i++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            var row = BuildRow(days[i], today, range, plan);
            SetRow(row, i);
            grid.Children.Add(row);
        }

        return grid;
    }

    private FrameworkElement BuildRow(ForecastDay day, DateOnly today, Range range, ForecastRowsPlan plan)
    {
        var grid = plan.TwoLine ? TwoLineRow(day, today, plan) : OneLineRow(day, today, range, plan);

        var row = Fluent.Row(grid);
        row.MinHeight = Math.Min(row.MinHeight, plan.RowHeight);
        _hovers.Add(Fluent.Hover(row));
        ToolTipService.SetToolTip(row, Describe(day));

        // [S-727] Today carries a quiet fill under its row, as well as its accent label; the hover
        // fill sits on top of it. Inset and rounded as Windows draws a selected list item, the
        // row's own padding less that inset so its text stays in line with the other days'.
        if (day.Day != today)
        {
            return row;
        }

        row.Padding = new Thickness(Fluent.Space4 - Fluent.Space1, 0, Fluent.Space4 - Fluent.Space1, 0);
        row.CornerRadius = new CornerRadius(Fluent.ControlRadius);
        var todayRow = new Border
        {
            CornerRadius = new CornerRadius(Fluent.ControlRadius),
            Margin = new Thickness(Fluent.Space1, 0, Fluent.Space1, 0),
            Child = row,
        };
        ItemFill.Paint(todayRow, ItemRole.Chip, ItemState.Selected);
        return todayRow;
    }

    private Grid OneLineRow(ForecastDay day, DateOnly today, Range range, ForecastRowsPlan plan)
    {
        var showRain = plan.Precipitation && _config.ShowPrecipitation;

        var parts = plan.Scale;
        var label = DayText(day, today, parts.Label);
        // The weekday column, so the glyphs line up down the list; a list too narrow for the glyph
        // has only the day and the high, and gives the day what it needs.
        label.MinWidth = plan.IconAndLow ? parts.LabelWidth : 0;

        var grid = new Grid { VerticalAlignment = VerticalAlignment.Center };
        var column = 0;

        void Add(FrameworkElement element, GridLength width)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = width });
            SetColumn(element, column++);
            element.VerticalAlignment = VerticalAlignment.Center;
            grid.Children.Add(element);
        }

        Add(label, GridLength.Auto);

        if (plan.IconAndLow)
        {
            Add(DayIcon(day, parts.Icon), GridLength.Auto);
        }

        if (plan.Description)
        {
            var sky = Fluent.Text(FluentType.Body, "TextFillColorSecondaryBrush");
            sky.Text = WeatherReport.Describe(day.Code);
            sky.TextTrimming = TextTrimming.CharacterEllipsis;
            sky.Margin = new Thickness(0, 0, Fluent.Space3, 0);
            Add(sky, new GridLength(1.4, GridUnitType.Star));
        }

        if (plan.Bar && day.Low is { } low && day.High is { } high)
        {
            Add(HorizontalBar(low, high, range, parts.Bar, day.Day == today ? _nowTemperature : null), new GridLength(1, GridUnitType.Star));
        }
        else
        {
            // Keeps the numbers at the right edge when there is no bar to take the slack.
            Add(new Border(), new GridLength(plan.Description ? 0.01 : 1, GridUnitType.Star));
        }

        if (plan.Chance)
        {
            var chance = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
            chance.Text = WeatherReport.Chance(day.PrecipitationChance);
            chance.TextAlignment = TextAlignment.Right;
            chance.MinWidth = 36;
            Add(chance, GridLength.Auto);
        }

        if (plan.Wind)
        {
            var wind = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
            wind.Text = day.WindMax is null ? string.Empty : WeatherReport.Wind(day.WindMax);
            wind.TextAlignment = TextAlignment.Right;
            wind.MinWidth = 56;
            Add(wind, GridLength.Auto);
        }

        if (showRain)
        {
            var rain = Rain(day.Precipitation, withUnit: false);
            rain.MinWidth = 44;
            rain.Margin = new Thickness(Fluent.Space2, 0, 0, 0);
            Add(rain, GridLength.Auto);
        }

        if (plan.IconAndLow)
        {
            Add(Low(day, parts), GridLength.Auto);
        }

        Add(High(day, parts), GridLength.Auto);
        return grid;
    }

    /// <summary>
    /// A tall row on a narrow tile: the day, glyph, low and high on the first line; the sky across
    /// the second line under them, and the precipitation under the high.
    /// </summary>
    private Grid TwoLineRow(ForecastDay day, DateOnly today, ForecastRowsPlan plan)
    {
        var grid = new Grid
        {
            VerticalAlignment = VerticalAlignment.Center,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
            },
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
            },
        };

        void Put(FrameworkElement element, int row, int column, int span = 1)
        {
            SetRow(element, row);
            SetColumn(element, column);
            SetColumnSpan(element, span);
            element.VerticalAlignment = VerticalAlignment.Center;
            grid.Children.Add(element);
        }

        var parts = plan.Scale;
        Put(DayText(day, today, parts.Label), 0, 0);

        if (plan.IconAndLow)
        {
            Put(DayIcon(day, parts.Icon), 0, 1);
            Put(Low(day, parts), 0, 2);
        }

        Put(High(day, parts), 0, 3);

        var sky = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
        sky.Text = WeatherReport.Describe(day.Code);
        sky.TextTrimming = TextTrimming.CharacterEllipsis;
        Put(sky, 1, 0, 3);

        if (_config.ShowPrecipitation)
        {
            Put(Rain(day.Precipitation, withUnit: false), 1, 3);
        }

        return grid;
    }

    /// <summary>[S-669] The sky's glyph, a step larger than a line's (and more with the row): it is the row's picture.</summary>
    private static FontIcon DayIcon(ForecastDay day, double size)
    {
        var icon = Fluent.Icon(LookGlyph(WeatherReport.Look(day.Code)), "TextFillColorSecondaryBrush", size);
        icon.Margin = new Thickness(Fluent.Space2, 0, Fluent.Space2, 0);
        return icon;
    }

    // ---- Columns [S-726] ---------------------------------------------------------------------

    /// <summary>
    /// The days that have a column's floor, side by side; past the column's cap the group is centred.
    /// Never a scroller: a day that does not fit whole is left out ([S-726]).
    /// </summary>
    private Grid BuildColumns(IReadOnlyList<ForecastDay> days, DateOnly today, Range range, ForecastColumnsPlan plan)
    {
        var count = Math.Min(plan.Days, days.Count);
        var grid = new Grid
        {
            Padding = new Thickness(WeatherForecastLayout.ColumnListPadding),
            Width = (plan.ColumnWidth * count) + (2 * WeatherForecastLayout.ColumnListPadding),
            HorizontalAlignment = HorizontalAlignment.Center,
        };

        for (var i = 0; i < count; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var column = BuildColumn(days[i], today, range, plan);
            SetColumn(column, i);
            grid.Children.Add(column);
        }

        return grid;
    }

    private Border BuildColumn(ForecastDay day, DateOnly today, Range range, ForecastColumnsPlan plan)
    {
        var scale = plan.Scale;
        var parts = new Grid { RowSpacing = WeatherForecastLayout.ColumnGap };

        void Add(FrameworkElement element, GridLength height)
        {
            parts.RowDefinitions.Add(new RowDefinition { Height = height });
            SetRow(element, parts.RowDefinitions.Count - 1);
            element.HorizontalAlignment = HorizontalAlignment.Center;
            parts.Children.Add(element);
        }

        Add(DayText(day, today, scale.Label), GridLength.Auto);

        if (plan.Icon)
        {
            Add(Fluent.Icon(LookGlyph(WeatherReport.Look(day.Code)), "TextFillColorSecondaryBrush", scale.Icon), GridLength.Auto);
        }

        if (plan.Description)
        {
            var sky = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
            sky.Text = WeatherReport.Describe(day.Code);
            sky.TextTrimming = TextTrimming.CharacterEllipsis;
            sky.MaxWidth = plan.ColumnWidth - (2 * WeatherForecastLayout.ColumnPadding);
            Add(sky, GridLength.Auto);
        }

        var high = Fluent.Text(scale.High);
        high.Text = WeatherReport.Degrees(day.High);
        Add(high, GridLength.Auto);

        var bar = plan.Bar && day.Low is { } low && day.High is { } highValue
            ? VerticalBar(low, highValue, range, scale.Bar, day.Day == today ? _nowTemperature : null)
            : null;

        if (bar is not null)
        {
            Add(bar, new GridLength(1, GridUnitType.Star));
        }

        if (plan.Low)
        {
            var lowText = Fluent.Text(scale.Low, "TextFillColorSecondaryBrush");
            lowText.Text = WeatherReport.Degrees(day.Low);
            Add(lowText, GridLength.Auto);
        }

        // A fixed line, rain or not, so the lows line up across a dry day and a wet one.
        if (plan.Precipitation && _config.ShowPrecipitation)
        {
            Add(Rain(day.Precipitation, withUnit: false), new GridLength(FluentType.Caption.LineHeight));
        }

        if (plan.Chance)
        {
            var chance = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
            chance.Text = WeatherReport.Chance(day.PrecipitationChance);
            Add(chance, new GridLength(FluentType.Caption.LineHeight));
        }

        if (plan.Wind)
        {
            var wind = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
            wind.Text = day.WindMax is null ? string.Empty : WeatherReport.Wind(day.WindMax);
            Add(wind, new GridLength(FluentType.Caption.LineHeight));
        }

        // Without a bar the slack goes under the column, so the low sits right under the high
        // instead of at the foot of the tile with a gap between them.
        if (bar is null)
        {
            parts.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        }

        var host = new Border
        {
            Child = parts,
            Padding = new Thickness(WeatherForecastLayout.ColumnPadding),
            CornerRadius = new CornerRadius(Fluent.ControlRadius),
        };

        if (day.Day == today)
        {
            ItemFill.Paint(host, ItemRole.Chip, ItemState.Selected);
        }

        ToolTipService.SetToolTip(host, Describe(day));
        return host;
    }

    // ---- parts -------------------------------------------------------------------------------

    /// <summary>"Today" or the weekday; today in the accent colour and semibold. [S-727]</summary>
    private static TextBlock DayText(ForecastDay day, DateOnly today, TypeStep step)
    {
        var isToday = day.Day == today;
        var label = Fluent.Text(isToday ? FluentType.Strong(step) : step, isToday ? "AccentTextFillColorPrimaryBrush" : "TextFillColorPrimaryBrush");
        label.Text = WeatherReport.DayLabel(day.Day, today);
        label.TextTrimming = TextTrimming.CharacterEllipsis;
        return label;
    }

    /// <summary>A droplet and the amount ("0.3", or "0.3 mm" where there is room); blank on a dry day. [S-727]</summary>
    private static StackPanel Rain(double? millimetres, bool withUnit)
    {
        var amount = withUnit ? WeatherReport.Precipitation(millimetres) : WeatherReport.PrecipitationAmount(millimetres);
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, HorizontalAlignment = HorizontalAlignment.Right };

        if (amount.Length == 0)
        {
            return panel;
        }

        var drop = Fluent.Icon(Glyphs.Droplet, "AccentTextFillColorPrimaryBrush", 10);
        drop.VerticalAlignment = VerticalAlignment.Center;

        var text = Fluent.Text(FluentType.Caption, "AccentTextFillColorPrimaryBrush");
        text.Text = amount;

        panel.Children.Add(drop);
        panel.Children.Add(text);
        return panel;
    }

    private static string Describe(ForecastDay day)
    {
        var parts = new List<string> { Formatting.ShortDate(day.Day), WeatherReport.Describe(day.Code) };
        var rain = WeatherReport.Precipitation(day.Precipitation);

        if (rain.Length > 0)
        {
            parts.Add(rain);
        }

        return string.Join(" · ", parts);
    }

    /// <summary>
    /// The day's range against the forecast's, left to right. Three star columns whose weights
    /// <i>are</i> the fractions, so the bar positions itself at layout time with no measurement.
    /// <paramref name="now"/> (today's bar only) adds the temperature now as a dot on it ([S-6172]).
    /// </summary>
    private FrameworkElement HorizontalBar(double low, double high, Range range, double thickness, double? now)
    {
        var (start, end) = WeatherReport.RangeBar(low, high, range.Coldest, range.Warmest);

        var track = Pill(null, thickness);
        ItemFill.Paint(track, ItemRole.Track);
        track.Height = thickness;
        var fill = Pill(_barBrush, thickness);
        fill.Height = thickness;

        var host = new Grid
        {
            Margin = new Thickness(0, 0, Fluent.Space3, 0),
            VerticalAlignment = VerticalAlignment.Center,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(start, GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(Math.Max(end - start, 0.02), GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(Math.Max(1 - end, 0), GridUnitType.Star) },
            },
        };

        SetColumnSpan(track, 3);
        SetColumn(fill, 1);
        host.Children.Add(track);
        host.Children.Add(fill);

        if (now is { } temperature)
        {
            // The dot's own column between two stars weighted by its place, so its centre lands on
            // the place with the dot kept whole inside the track's ends.
            var at = ForecastNow.OnBar(temperature, range.Coldest, range.Warmest);
            var layer = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(at, GridUnitType.Star) },
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = new GridLength(1 - at, GridUnitType.Star) },
                },
            };
            var dot = NowDot(thickness);
            SetColumn(dot, 1);
            layer.Children.Add(dot);
            SetColumnSpan(layer, 3);
            host.Children.Add(layer);
        }

        return host;
    }

    /// <summary>The same range, bottom (cold) to top (warm), for a column, with the same now dot.</summary>
    private FrameworkElement VerticalBar(double low, double high, Range range, double thickness, double? now)
    {
        var (start, end) = WeatherReport.RangeBar(low, high, range.Coldest, range.Warmest);

        var track = Pill(null, thickness);
        ItemFill.Paint(track, ItemRole.Track);
        track.Width = thickness;
        var fill = Pill(_barBrush, thickness);
        fill.Width = thickness;

        var host = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = new GridLength(Math.Max(1 - end, 0), GridUnitType.Star) },
                new RowDefinition { Height = new GridLength(Math.Max(end - start, 0.02), GridUnitType.Star) },
                new RowDefinition { Height = new GridLength(start, GridUnitType.Star) },
            },
        };

        SetRowSpan(track, 3);
        SetRow(fill, 1);
        host.Children.Add(track);
        host.Children.Add(fill);

        if (now is { } temperature)
        {
            var at = ForecastNow.OnBar(temperature, range.Coldest, range.Warmest);
            var layer = new Grid
            {
                RowDefinitions =
                {
                    new RowDefinition { Height = new GridLength(1 - at, GridUnitType.Star) },
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = new GridLength(at, GridUnitType.Star) },
                },
            };
            var dot = NowDot(thickness);
            SetRow(dot, 1);
            layer.Children.Add(dot);
            SetRowSpan(layer, 3);
            host.Children.Add(layer);
        }

        return host;
    }

    /// <summary>
    /// [S-6172] The temperature now on a bar: a dot a little wider than the bar, in the colour that
    /// stands on the accent, ringed in the accent so it also reads on the track.
    /// </summary>
    private Ellipse NowDot(double thickness)
    {
        var size = thickness + Fluent.Space1;
        return new Ellipse
        {
            Width = size,
            Height = size,
            Fill = _nowFill,
            Stroke = _barBrush,
            StrokeThickness = NowRing,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
    }

    private static Border Pill(Brush? brush, double thickness) => new()
    {
        Background = brush,
        CornerRadius = new CornerRadius(thickness / 2),
    };

    /// <summary>[S-669] The day's high: the row's strong figure, in the shared figure column, at the scale's step.</summary>
    private static TextBlock High(ForecastDay day, DayScale scale)
    {
        var block = WidgetParts.Figure(WeatherReport.Degrees(day.High), scale.High);
        block.MinWidth = scale.FigureWidth;
        block.Margin = new Thickness(Fluent.Space2, 0, 0, 0);
        return block;
    }

    /// <summary>[S-669] The day's low: the same column, regular and secondary, as Windows sets a range.</summary>
    private static TextBlock Low(ForecastDay day, DayScale scale)
    {
        var block = WidgetParts.Figure(WeatherReport.Degrees(day.Low), scale.Low, "TextFillColorSecondaryBrush");
        block.MinWidth = scale.FigureWidth;
        block.Margin = new Thickness(Fluent.Space2, 0, 0, 0);
        return block;
    }
}
