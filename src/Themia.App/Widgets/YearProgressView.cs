using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// [S-4800] How much of the day, week, month and year has passed: a label, a percentage and a bar
/// per span. The spans and their fractions are <see cref="TimeProgress"/>; which of them a size
/// shows is <see cref="YearProgressLayout"/>. One tick a minute on the shared ticker ([S-4804]).
/// </summary>
internal sealed partial class YearProgressView : Grid, IWidgetView, IWidgetSizeAware
{
    private readonly StackPanel _content = new() { VerticalAlignment = VerticalAlignment.Center, Margin = WidgetLayout.Inset };

    private IDisposable? _subscription;
    private WidgetArea _area;
    private YearProgressPlan _plan = YearProgressLayout.Plan(default);

    /// <summary>The instant drawn: the machine's clock, which a preview pins.</summary>
    private Func<DateTimeOffset> _clock = static () => DateTimeOffset.Now;

    public YearProgressView()
    {
        Children.Add(_content);
    }

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        Strings.Changed -= OnLanguage;
        Strings.Changed += OnLanguage;
        _subscription ??= ticker.Subscribe(((config as YearProgressConfig) ?? new YearProgressConfig()).TickInterval, Render);
    }

    public void ApplyConfig(WidgetConfig config) => Render();

    public void ApplySize(WidgetArea area)
    {
        _area = area;
        _plan = YearProgressLayout.Plan(area.Deflate(WidgetLayout.ContentInset, WidgetLayout.ContentInset));
        Render();
    }

    public void Detach()
    {
        Strings.Changed -= OnLanguage;
        _subscription?.Dispose();
        _subscription = null;
    }

    private void OnLanguage(Locale locale) => Render();

    private void Render()
    {
        var now = _clock();
        var zone = TimeZoneInfo.Local;
        var weekStart = RegionFormat.Current.WeekStart;
        var locale = Strings.Current;
        _content.Children.Clear();

        if (_plan.Hero)
        {
            RenderHero(TimeProgress.Of(ProgressSpan.Year, now, zone, weekStart), locale);
            return;
        }

        if (_plan.Columns)
        {
            RenderColumns(now, zone, weekStart, locale);
            return;
        }

        _content.Margin = WidgetLayout.Inset;
        _content.Spacing = YearProgressLayout.RowGap;
        _content.HorizontalAlignment = HorizontalAlignment.Stretch;

        if (_plan.Headline)
        {
            _content.Children.Add(Headline(TimeProgress.Of(ProgressSpan.Year, now, zone, weekStart), locale));
        }

        foreach (var span in _plan.Rows)
        {
            var progress = TimeProgress.Of(span, now, zone, weekStart);
            var label = Fluent.Text(FluentType.Body);
            label.Text = Label(span);
            label.TextTrimming = TextTrimming.CharacterEllipsis;

            var percent = Fluent.Text(FluentType.BodyStrong);
            percent.Text = PercentText(locale, progress.Fraction);
            Tabular(percent);

            var line = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = GridLength.Auto },
                },
            };
            SetColumn(percent, 1);
            line.Children.Add(label);
            line.Children.Add(percent);

            var row = new StackPanel { Spacing = YearProgressLayout.BarGap, Children = { line, Bar(progress.Fraction) } };
            if (_plan.Details)
            {
                row.Children.Add(Detail(progress, now, weekStart, locale));
            }

            ToolTipService.SetToolTip(row, Tip(progress, now, weekStart, locale));
            _content.Children.Add(row);
        }
    }

    /// <summary>[S-4803] The year's percentage, large, over "2026", on a tile with room to spare.</summary>
    private static StackPanel Headline(SpanProgress year, Locale locale)
    {
        var percent = Fluent.Text(FluentType.TitleLarge);
        percent.Text = PercentText(locale, year.Fraction, 1);
        Tabular(percent);

        var caption = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
        caption.Text = YearText(locale, year);
        return new StackPanel { Children = { percent, caption } };
    }

    private static string YearText(Locale locale, SpanProgress year)
        => LocaleFormat.Digits(locale, year.First.Year.ToString(System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>[S-4803] The smallest tiles: the year alone, its percentage as large as the tile allows.</summary>
    private void RenderHero(SpanProgress year, Locale locale)
    {
        var text = PercentText(locale, year.Fraction);

        // The hero keeps a half inset top and bottom: on XS the full one would leave it caption-sized.
        var inner = _area.Deflate(WidgetLayout.ContentInset, Fluent.Space2);
        var room = Math.Max(0, inner.Height - YearProgressLayout.Bar - YearProgressLayout.BarGap);
        var withCaption = room - FluentType.Caption.LineHeight - YearProgressLayout.BarGap >= FluentType.Title.LineHeight;
        var step = FluentType.Hero(inner.Width, withCaption ? room - FluentType.Caption.LineHeight - YearProgressLayout.BarGap : room, text.Length);

        var percent = Fluent.Text(FluentType.Strong(step));
        percent.Text = text;
        percent.HorizontalAlignment = HorizontalAlignment.Center;
        Tabular(percent);

        var caption = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
        caption.Text = YearText(locale, year);
        caption.HorizontalAlignment = HorizontalAlignment.Center;
        caption.Visibility = withCaption ? Visibility.Visible : Visibility.Collapsed;

        _content.Margin = new Thickness(WidgetLayout.ContentInset, Fluent.Space2, WidgetLayout.ContentInset, Fluent.Space2);
        _content.Spacing = YearProgressLayout.BarGap;
        _content.HorizontalAlignment = HorizontalAlignment.Stretch;
        _content.Children.Add(percent);
        _content.Children.Add(Bar(year.Fraction));
        _content.Children.Add(caption);
        ToolTipService.SetToolTip(_content, Tip(year, _clock(), RegionFormat.Current.WeekStart, locale));
    }

    /// <summary>[S-4803] A strip: the four spans side by side, each its label over its percentage and bar.</summary>
    private void RenderColumns(DateTimeOffset now, TimeZoneInfo zone, WeekStartDay weekStart, Locale locale)
    {
        var grid = new Grid { ColumnSpacing = YearProgressLayout.ColumnGap };
        var column = 0;

        foreach (var span in TimeProgress.All)
        {
            var progress = TimeProgress.Of(span, now, zone, weekStart);
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var label = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
            label.Text = Label(span);
            label.TextTrimming = TextTrimming.CharacterEllipsis;

            var percent = Fluent.Text(FluentType.Strong(FluentType.Subtitle));
            percent.Text = PercentText(locale, progress.Fraction);
            Tabular(percent);

            var cell = new StackPanel { Spacing = YearProgressLayout.BarGap, Children = { label, percent, Bar(progress.Fraction) } };
            if (_plan.Details)
            {
                cell.Children.Add(Detail(progress, now, weekStart, locale));
            }

            ToolTipService.SetToolTip(cell, Tip(progress, now, weekStart, locale));
            SetColumn(cell, column++);
            grid.Children.Add(cell);
        }

        _content.Margin = WidgetLayout.Inset;
        _content.Spacing = 0;
        _content.Children.Add(grid);
    }

    private static ProgressBar Bar(double fraction) => new()
    {
        Minimum = 0,
        Maximum = 1,
        Value = fraction,
        MinHeight = YearProgressLayout.Bar,
        Height = YearProgressLayout.Bar,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        IsTabStop = false,
    };

    private static TextBlock Detail(SpanProgress progress, DateTimeOffset now, WeekStartDay weekStart, Locale locale)
    {
        var detail = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
        detail.Text = DetailText(progress, now, weekStart, locale);
        detail.TextTrimming = TextTrimming.CharacterEllipsis;
        Tabular(detail);
        return detail;
    }

    /// <summary>[S-4805] "9 h 28 min left", "Week 42", "Day 12 of 31", "Day 285 of 365".</summary>
    private static string DetailText(SpanProgress progress, DateTimeOffset now, WeekStartDay weekStart, Locale locale)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, TimeZoneInfo.Local).DateTime);

        return progress.Span switch
        {
            ProgressSpan.Day => Strings.Format(locale, Str.YearProgress_Left, ("time", Formatting.Duration(locale, progress.Left(now)))),
            ProgressSpan.Week => Strings.Format(locale, Str.YearProgress_WeekNumber, ("number", TimeProgress.WeekNumber(today, weekStart))),
            _ => Strings.Format(locale, Str.YearProgress_DayOf,
                ("day", progress.DayOf(today)),
                ("days", progress.Days)),
        };
    }

    private static string Tip(SpanProgress progress, DateTimeOffset now, WeekStartDay weekStart, Locale locale)
        => $"{Label(progress.Span)} · {PercentText(locale, progress.Fraction, 1)} · {DetailText(progress, now, weekStart, locale)}";

    private static string PercentText(Locale locale, double fraction, int decimals = 0)
        => LocaleFormat.Percent(locale, TimeProgress.Percent(fraction, decimals), decimals);

    private static string Label(ProgressSpan span) => (span switch
    {
        ProgressSpan.Day => Str.YearProgress_Day,
        ProgressSpan.Week => Str.YearProgress_Week,
        ProgressSpan.Month => Str.YearProgress_Month,
        _ => Str.YearProgress_Year,
    }).Text();

    private static void Tabular(TextBlock text)
        => Microsoft.UI.Xaml.Documents.Typography.SetNumeralAlignment(text, FontNumeralAlignment.Tabular);
}
