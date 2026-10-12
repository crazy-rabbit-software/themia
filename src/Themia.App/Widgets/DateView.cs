using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Themia.App.Services;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// [S-5300] The date without the time: the day of the month, large, with the weekday over it and the
/// month under it (or, on a strip, beside it), for the wallpaper. Which parts a size shows is
/// <see cref="DateLayout"/>; the words are <see cref="DateText"/>, through LocaleFormat. One tick a
/// minute on the shared ticker turns it over at midnight ([S-5304]). Clicking it does nothing.
/// </summary>
internal sealed partial class DateView : Grid, IWidgetView, IWidgetSizeAware
{
    private readonly StackPanel _stack = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly StackPanel _words = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _weekday = Fluent.Text(FluentType.BodyStrong, "AccentTextFillColorPrimaryBrush");
    private readonly TextBlock _number = Fluent.Text(FluentType.Title);
    private readonly TextBlock _month = Fluent.Text(FluentType.Body, "TextFillColorSecondaryBrush");

    private DateConfig _config = new();
    private IDisposable? _subscription;
    private WidgetArea _area;
    private DatePlan _plan = DateLayout.Plan(default, false);
    private DateFace? _face;

    /// <summary>The day drawn: the machine's, which a preview pins.</summary>
    private Func<DateTimeOffset> _clock = static () => DateTimeOffset.Now;

    public DateView()
    {
        // The same inset as the clock ([S-645]): the full one either side, half above and below.
        Padding = new Thickness(WidgetLayout.ContentInset, Fluent.Space2, WidgetLayout.ContentInset, Fluent.Space2);
        _weekday.TextTrimming = TextTrimming.CharacterEllipsis;
        _month.TextTrimming = TextTrimming.CharacterEllipsis;
        _number.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
        Typography.SetNumeralAlignment(_number, FontNumeralAlignment.Tabular);

        Children.Add(_stack);
    }

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _config = config as DateConfig ?? new DateConfig();
        Strings.Changed -= OnLanguage;
        Strings.Changed += OnLanguage;
        _subscription ??= ticker.Subscribe(_config.TickInterval, Render);
        Lay();
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as DateConfig ?? new DateConfig();
        Lay();
    }

    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Lay();
    }

    public void Detach()
    {
        Strings.Changed -= OnLanguage;
        _subscription?.Dispose();
        _subscription = null;
    }

    private void OnLanguage(Locale locale) => Render();

    /// <summary>Plan the size, put the parts where the face wants them, then write the words.</summary>
    private void Lay()
    {
        _plan = DateLayout.Plan(_area.Deflate(Padding.Left, Padding.Top), _config.ShowYear);
        Arrange(_plan.Face);

        _number.Apply(_plan.Number);
        _weekday.Apply(_plan.WeekdayStep);
        _month.Apply(_plan.MonthStep);
        _weekday.Visibility = _plan.Weekday ? Visibility.Visible : Visibility.Collapsed;
        _month.Visibility = _plan.Month ? Visibility.Visible : Visibility.Collapsed;
        // A bound, not a width: the pair is centred (or aligned) as a whole, the words trimmed at the bound.
        _words.MaxWidth = _plan.Face == DateFace.Row ? _plan.TextWidth : double.PositiveInfinity;

        ApplyAlignment();
        Render();
    }

    /// <summary>
    /// Column: weekday, day, month in one stack. Row: the day, then the weekday over the month.
    /// Rebuilt only when the face changes, never per pass.
    /// </summary>
    private void Arrange(DateFace face)
    {
        if (_face == face)
        {
            return;
        }

        _face = face;
        _stack.Children.Clear();
        _words.Children.Clear();

        if (face == DateFace.Row)
        {
            _stack.Orientation = Orientation.Horizontal;
            _stack.Spacing = DateLayout.Gap;
            _words.Children.Add(_weekday);
            _words.Children.Add(_month);
            _stack.Children.Add(_number);
            _stack.Children.Add(_words);
            return;
        }

        _stack.Orientation = Orientation.Vertical;
        _stack.Spacing = 0;
        _stack.Children.Add(_weekday);
        _stack.Children.Add(_number);
        _stack.Children.Add(_month);
    }

    /// <summary>[S-5303] Left, centre or right for the whole column, as the clock's [S-1484]; the words beside the day read from it.</summary>
    private void ApplyAlignment()
    {
        var (horizontal, text) = _config.Align switch
        {
            ClockAlign.Left => (HorizontalAlignment.Left, TextAlignment.Left),
            ClockAlign.Right => (HorizontalAlignment.Right, TextAlignment.Right),
            _ => (HorizontalAlignment.Center, TextAlignment.Center),
        };

        _stack.HorizontalAlignment = horizontal;
        var row = _plan.Face == DateFace.Row;

        foreach (var line in new[] { _weekday, _number, _month })
        {
            line.HorizontalAlignment = row ? HorizontalAlignment.Left : horizontal;
            line.TextAlignment = row ? TextAlignment.Left : text;
        }
    }

    private void Render()
    {
        var locale = Strings.Current;
        var today = DateOnly.FromDateTime(_clock().LocalDateTime);
        _number.Text = DateText.Number(locale, today);
        _weekday.Text = DateText.Weekday(locale, today, _plan.ShortWeekday);
        _month.Text = DateText.Month(locale, today, _plan.ShortMonth, _config.ShowYear);
        ToolTipService.SetToolTip(this, DateText.Full(locale, today));
    }
}
