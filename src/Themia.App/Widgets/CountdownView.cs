using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Shell;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// Time left until the dates the user cares about. [C-01]
/// <para>
/// The rows are built when the timer list changes and only re-lettered on a tick, rather than rebuilt
/// each time: a list that is thrown away once a second drops the row the pointer is over, and with a
/// seconds format that happens every second. [S-143]
/// </para>
/// <para>
/// [S-1447] The tile has a ladder (<see cref="CountdownLayout.Plan"/>): the soonest timer is
/// <i>featured</i> as a hero readout — the whole of a small tile (with a "Next: …" line under it),
/// across the top of a tall or large one with the others as rows under it, on the left of a wide
/// strip with the others beside it. The hero takes what the rows leave. Rows are fixed Windows 11
/// list rows (40px, 32 compact) so a growing tile shows <i>more rows</i>, not bigger ones; large
/// tiles add a progress bar from the timer's creation to its target ([S-1448]).
/// </para>
/// </summary>
internal sealed partial class CountdownView : Grid, IWidgetView, IWidgetSizeAware, IWidgetSurfaceAware
{
    /// <summary>The hero's own column on a wide strip, as a share of the width.</summary>
    private const double HeroShare = 0.42;

    private const double ProgressHeight = 4;

    /// <summary>A compact list at least this wide shows each row's date in its own column.</summary>
    private const double InlineDateWidth = 260;

    private readonly StackPanel _list = new() { Spacing = 0 };
    private readonly ScrollViewer _scroll;
    private readonly List<Row> _rows = [];

    private readonly StackPanel _hero = new() { Spacing = 0 };
    private readonly TextBlock _heroLabel = Fluent.Text(FluentType.Body, "TextFillColorSecondaryBrush");
    private readonly TextBlock _heroValue = Fluent.Text(FluentType.Title);
    private readonly TextBlock _heroDate = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
    private readonly ProgressBar _heroProgress = new() { Minimum = 0, Maximum = 1, Height = ProgressHeight, MinHeight = ProgressHeight };
    private readonly TextBlock _next = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
    private readonly IDisposable _heroMenu;

    private readonly Brush _primary = FluentLayout.Themed("TextFillColorPrimaryBrush", Colors.White);
    private readonly Brush _critical = FluentLayout.Themed("SystemFillColorCriticalBrush", Colors.IndianRed);

    private IWidgetSurface? _surface;
    private Ticker? _ticker;
    private CountdownConfig _config = new();
    private IDisposable? _subscription;
    private TimeSpan _subscribedInterval = TimeSpan.Zero;
    private WidgetArea _area;
    private CountdownPlan _plan;
    private string? _featuredId;
    private bool _heroDateShown;
    private bool _heroProgressShown;
    private bool _nextShown;
    private bool _detached;

    public CountdownView()
    {
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        _scroll = new ScrollViewer
        {
            // Nothing either side, so rows reach both edges and carry the inset as their own padding;
            // a little under the last row so it is not flush against the card.
            Padding = Fluent.ListPadding,
            Content = _list,
            HorizontalScrollMode = ScrollMode.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
        };

        // [C-04] Tabular figures, so a ticking duration does not shuffle sideways.
        Microsoft.UI.Xaml.Documents.Typography.SetNumeralAlignment(_heroValue, FontNumeralAlignment.Tabular);
        _heroValue.MaxLines = 1;
        _heroValue.TextTrimming = TextTrimming.Clip;
        _heroLabel.MaxLines = 1;
        _heroLabel.TextTrimming = TextTrimming.CharacterEllipsis;
        _heroDate.MaxLines = 1;
        _next.MaxLines = 1;
        _next.TextTrimming = TextTrimming.CharacterEllipsis;
        _heroProgress.Margin = new Thickness(0, Fluent.Space1, 0, 0);
        _next.Margin = new Thickness(0, Fluent.Space1, 0, 0);

        _hero.Children.Add(_heroLabel);
        _hero.Children.Add(_heroValue);
        _hero.Children.Add(_heroDate);
        _hero.Children.Add(_heroProgress);
        _hero.Children.Add(_next);
        _hero.VerticalAlignment = VerticalAlignment.Center;

        // [S-1445] The featured timer has the same menu as its row would.
        _heroMenu = ItemMenu.Attach(_hero, () => _featuredId is null ? [] : TimerMenu(_featuredId));

        Children.Add(_hero);
        Children.Add(_scroll);
    }

    /// <inheritdoc />
    public void Bind(IWidgetSurface surface) => _surface = surface;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _ticker = ticker;
        Strings.Changed += OnLanguage;
        ApplyConfig(config);
    }

    /// <summary>[S-1109] Every reading and date is worded again in the new language.</summary>
    private void OnLanguage(Locale locale) => Rebuild();

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as CountdownConfig ?? new CountdownConfig();
        Rebuild();
    }

    /// <inheritdoc />
    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Rebuild();
    }

    /// <summary>The list or the featured timer changed: rows, cadence, layout and letters.</summary>
    private void Rebuild()
    {
        if (_detached)
        {
            return;
        }

        var now = DateTimeOffset.Now;
        var featured = CountdownLayout.Featured(_config.Timers, now);
        _plan = CountdownLayout.Plan(_area, _config.Timers.Count);
        _featuredId = _plan.Hero == CountdownHero.None || featured < 0 ? null : _config.Timers[featured].Id;

        BuildRows();
        Resubscribe();
        Lay();
        Refresh();
    }

    /// <summary>
    /// Only when the cadence actually changed. Dropping and remaking the subscription on every
    /// config touch makes dragging a settings slider stutter. [C-20]
    /// </summary>
    private void Resubscribe()
    {
        // Nothing to re-letter means nothing to wake for. [S-142]
        if (_config.Timers.Count == 0)
        {
            _subscription?.Dispose();
            _subscription = null;
            _subscribedInterval = TimeSpan.Zero;
            return;
        }

        var interval = _config.TickInterval;

        if (_subscription is not null && interval == _subscribedInterval)
        {
            return;
        }

        _subscription?.Dispose();
        _subscribedInterval = interval;
        _subscription = _ticker?.Subscribe(interval, OnTick);
    }

    private void BuildRows()
    {
        foreach (var existing in _rows)
        {
            existing.Detach();
        }

        _rows.Clear();
        _list.Children.Clear();

        foreach (var timer in _config.Timers)
        {
            // The featured timer is the hero; listing it again under itself says it twice.
            if (timer.Id == _featuredId)
            {
                continue;
            }

            var row = new Row(timer, _primary, _critical, id => TimerMenu(id));
            row.SetDensity(_plan.CompactRows, _plan.RowProgress, InlineDates());
            _rows.Add(row);
            _list.Children.Add(row.Host);
        }
    }

    /// <summary>
    /// [S-1447] A compact row on a wide list still has room for its date — beside the label rather
    /// than under it (a Wide strip: label, date and duration in three columns).
    /// </summary>
    private bool InlineDates()
    {
        var listWidth = _plan.Hero == CountdownHero.Left ? _area.Width * (1 - HeroShare) : _area.Width;
        return _plan.CompactRows && listWidth >= InlineDateWidth;
    }

    /// <summary>
    /// [S-1447] Place the hero and the list for the plan, and give the hero what the rest leaves.
    /// </summary>
    private void Lay()
    {
        // [S-1862] No timers: the card shows its Set up button over this, so draw nothing.
        var empty = _config.Timers.Count == 0;

        var hero = empty ? CountdownHero.None : _plan.Hero;
        _hero.Visibility = hero == CountdownHero.None ? Visibility.Collapsed : Visibility.Visible;
        _scroll.Visibility = empty || hero == CountdownHero.Only || _rows.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

        var width = _area.Width;
        var heroWidth = hero == CountdownHero.Left ? Math.Floor(width * HeroShare) : width;

        ColumnDefinitions[0].Width = hero == CountdownHero.Left ? new GridLength(heroWidth) : GridLength.Auto;

        switch (hero)
        {
            case CountdownHero.Only:
                Place(_hero, row: 0, column: 0, rowSpan: 2, columnSpan: 2);
                break;
            case CountdownHero.Top:
                Place(_hero, row: 0, column: 0, rowSpan: 1, columnSpan: 2);
                Place(_scroll, row: 1, column: 0, rowSpan: 1, columnSpan: 2);
                break;
            case CountdownHero.Left:
                Place(_hero, row: 0, column: 0, rowSpan: 2, columnSpan: 1);
                Place(_scroll, row: 0, column: 1, rowSpan: 2, columnSpan: 1);
                break;
            default:
                Place(_scroll, row: 0, column: 0, rowSpan: 2, columnSpan: 2);
                break;
        }

        // A small tile centres its one readout; a large one reads from the top left like a list.
        var centred = hero == CountdownHero.Only && _area.Least == SizeClass.Compact;
        _hero.HorizontalAlignment = centred ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
        _hero.Margin = new Thickness(WidgetLayout.ContentInset, hero == CountdownHero.Top ? Fluent.Space1 : 0, WidgetLayout.ContentInset, hero == CountdownHero.Top ? Fluent.Space2 : 0);
        foreach (var text in (TextBlock[])[_heroLabel, _heroValue, _heroDate, _next])
        {
            text.TextAlignment = centred ? TextAlignment.Center : TextAlignment.Left;
        }

        if (hero == CountdownHero.None || !_area.IsMeasured)
        {
            RowDefinitions[0].Height = GridLength.Auto;
            RowDefinitions[1].Height = new GridLength(1, GridUnitType.Star);
            return;
        }

        // The hero's ladder: the label and the readout at its smallest always; then the date, the
        // progress bar and the "next" line as they fit; the list (Top) takes whole rows; whatever is
        // left goes to the readout.
        var boxWidth = heroWidth - (2 * WidgetLayout.ContentInset);
        // Top: less the hero's own margins and the padding under the list.
        var boxHeight = hero == CountdownHero.Top
            ? _area.Height - Fluent.Space1 - Fluent.Space2 - Fluent.ListPadding.Bottom
            : _area.Height - Fluent.Space2;
        var room = new Disclosure(boxWidth, boxHeight, gap: 0);

        var labelStep = _area.HeightClass == SizeClass.Compact ? FluentType.Caption : FluentType.Body;
        _heroLabel.Apply(labelStep);
        room.Reserve(labelStep.LineHeight);
        room.Reserve(FluentType.Subtitle.LineHeight);

        _heroDateShown = _area.HeightClass >= SizeClass.Regular && room.Take(FluentType.Caption.LineHeight);
        _heroProgressShown = _plan.HeroProgress && room.Take(ProgressHeight + Fluent.Space1);
        _nextShown = _plan.NextLine && room.Take(FluentType.Caption.LineHeight + Fluent.Space1);

        // When every row fits, the rows sit at the foot and the hero is centred in the rest, so a
        // readout that is limited by its width does not leave a band of nothing under the list.
        var allFit = false;

        if (hero == CountdownHero.Top)
        {
            var rowHeight = _plan.CompactRows ? Fluent.RowHeightCompact : Fluent.RowHeight;
            if (_plan.RowProgress)
            {
                // Label, date and the bar with its margins.
                rowHeight = Math.Max(rowHeight, FluentType.Body.LineHeight + FluentType.Caption.LineHeight + ProgressHeight + (2 * Fluent.Space1));
            }

            allFit = room.TakeRows(rowHeight, _rows.Count) >= _rows.Count;
        }

        RowDefinitions[0].Height = allFit ? new GridLength(1, GridUnitType.Star) : GridLength.Auto;
        RowDefinitions[1].Height = allFit ? GridLength.Auto : new GridLength(1, GridUnitType.Star);
        _hero.VerticalAlignment = hero == CountdownHero.Top && !allFit ? VerticalAlignment.Top : VerticalAlignment.Center;

        _heroDate.Visibility = _heroDateShown ? Visibility.Visible : Visibility.Collapsed;
        _heroProgress.Visibility = _heroProgressShown ? Visibility.Visible : Visibility.Collapsed;
        _next.Visibility = _nextShown ? Visibility.Visible : Visibility.Collapsed;

        var featured = _config.Timers.Find(t => t.Id == _featuredId);
        var elapsed = featured is not null && featured.Target <= DateTimeOffset.Now;
        var characters = CountdownLayout.HeroCharacters(featured?.Format ?? CountdownFormat.Auto, elapsed);
        var step = FluentType.Hero(boxWidth, room.Remaining + FluentType.Subtitle.LineHeight, characters);

        _heroValue.Apply(step);
    }

    private static void Place(FrameworkElement element, int row, int column, int rowSpan, int columnSpan)
    {
        SetRow(element, row);
        SetColumn(element, column);
        SetRowSpan(element, rowSpan);
        SetColumnSpan(element, columnSpan);
    }

    private void OnTick()
    {
        if (_detached)
        {
            return;
        }

        // The featured timer changes when it goes by and another is now soonest: that is a new
        // layout, not just new letters.
        var featured = CountdownLayout.Featured(_config.Timers, DateTimeOffset.Now);
        var id = _plan.Hero == CountdownHero.None || featured < 0 ? null : _config.Timers[featured].Id;

        if (id != _featuredId)
        {
            Rebuild();
            return;
        }

        Refresh();
    }

    /// <summary>Re-letter the hero and the rows. Called on every tick, so it allocates nothing but strings.</summary>
    private void Refresh()
    {
        var now = DateTimeOffset.Now;
        var zone = TimeZoneInfo.Local;

        foreach (var row in _rows)
        {
            row.Update(now, zone);
        }

        var featured = _featuredId is null ? null : _config.Timers.Find(t => t.Id == _featuredId);

        if (featured is null)
        {
            return;
        }

        var reading = CountdownFormatter.Read(featured.Target, now, featured.Format, Strings.Current);
        _heroLabel.Text = string.IsNullOrWhiteSpace(featured.Label) ? CountdownFormatter.Untitled(Strings.Current) : featured.Label;
        _heroValue.Text = reading.Text;

        // [C-40] Gone by: the readout carries it in the theme's critical colour.
        _heroValue.Foreground = reading.Elapsed ? _critical : _primary;
        _heroDate.Text = CountdownFormatter.TargetLabel(featured.Target, now, zone, Strings.Current);
        ToolTipService.SetToolTip(_hero, CountdownFormatter.FullTargetLabel(featured.Target, zone, Strings.Current));

        if (_heroProgressShown && CountdownLayout.Progress(featured, now) is { } progress)
        {
            _heroProgress.Value = progress;
            _heroProgress.Visibility = Visibility.Visible;
        }
        else
        {
            _heroProgress.Visibility = Visibility.Collapsed;
        }

        _next.Text = _nextShown ? CountdownLayout.NextLine(_config.Timers, _config.Timers.IndexOf(featured), now, Strings.Current) ?? string.Empty : string.Empty;
        _next.Visibility = _nextShown && _next.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    // -----------------------------------------------------------------------------------------
    // The timer menu ([S-1445], the original C-60)
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// The timer's own menu: Remove. [S-1746] The display format is how the timer looks: it is chosen
    /// in the widget's settings only.
    /// </summary>
    private IEnumerable<ItemMenuEntry> TimerMenu(string id)
    {
        if (_detached || _config.Timers.Find(t => t.Id == id) is null)
        {
            return [];
        }

        return [new(Glyphs.Delete, Str.WidgetsA_Common_RemoveEllipsis.Text(), () => _ = RemoveAsync(id), Danger: true, Quick: QuickCommand.Delete)];
    }

    private async Task RemoveAsync(string id)
    {
        var timer = _config.Timers.Find(t => t.Id == id);

        if (_detached || timer is null)
        {
            return;
        }

        var name = string.IsNullOrWhiteSpace(timer.Label) ? CountdownFormatter.Untitled(Strings.Current) : timer.Label;
        var confirmed = await FluentLayout.ConfirmAsync(this, Str.WidgetsA_Countdown_RemoveConfirm_Title.Format(("name", name)),
            Str.WidgetsA_Countdown_RemoveConfirm_Body.Text(), Str.Common_Remove.Text());

        if (!confirmed || _detached || !_config.Timers.Remove(timer))
        {
            return;
        }

        _surface?.RequestSave();
        Rebuild();
    }

    public void Detach()
    {
        if (_detached)
        {
            return;
        }

        _detached = true;
        Strings.Changed -= OnLanguage;
        _subscription?.Dispose();
        _subscription = null;
        _heroMenu.Dispose();

        foreach (var row in _rows)
        {
            row.Detach();
        }

        _rows.Clear();
        _list.Children.Clear();
    }

    /// <summary>One timer's row: label and target on the left, the duration on the right. [C-03]</summary>
    private sealed class Row
    {
        private readonly CountdownTimer _timer;
        private readonly TextBlock _label = Fluent.Text(FluentType.Body);
        private readonly TextBlock _date = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
        private readonly TextBlock _remaining = Fluent.Text(FluentType.BodyStrong);
        private readonly TextBlock _inlineDate = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
        private readonly ProgressBar _progress = new() { Minimum = 0, Maximum = 1, Height = ProgressHeight, MinHeight = ProgressHeight };

        private readonly Brush _primary;
        private readonly Brush _secondary = FluentLayout.Themed("TextFillColorSecondaryBrush", Colors.Gray);
        private readonly Brush _critical;

        private readonly IDisposable _hover;
        private readonly IDisposable _menu;
        private bool _showProgress;

        public Row(CountdownTimer timer, Brush primary, Brush critical, Func<string, IEnumerable<ItemMenuEntry>> menu)
        {
            _timer = timer;
            _primary = primary;
            _critical = critical;

            _label.Text = string.IsNullOrWhiteSpace(timer.Label) ? CountdownFormatter.Untitled(Strings.Current) : timer.Label;
            _label.MaxLines = 1;
            _label.TextTrimming = TextTrimming.CharacterEllipsis;

            _date.MaxLines = 1;

            _remaining.VerticalAlignment = VerticalAlignment.Center;
            _remaining.Margin = new Thickness(Fluent.Space3, 0, 0, 0);

            // [C-04] Tabular figures, so a ticking duration does not shuffle sideways.
            Microsoft.UI.Xaml.Documents.Typography.SetNumeralAlignment(_remaining, FontNumeralAlignment.Tabular);
            Microsoft.UI.Xaml.Documents.Typography.SetNumeralAlignment(_date, FontNumeralAlignment.Tabular);

            _progress.Margin = new Thickness(0, Fluent.Space1, 0, Fluent.Space1);

            var text = new StackPanel { Spacing = 0, VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(_label);
            text.Children.Add(_date);
            text.Children.Add(_progress);

            _inlineDate.MaxLines = 1;
            _inlineDate.VerticalAlignment = VerticalAlignment.Center;
            _inlineDate.Margin = new Thickness(Fluent.Space3, 0, 0, 0);
            Microsoft.UI.Xaml.Documents.Typography.SetNumeralAlignment(_inlineDate, FontNumeralAlignment.Tabular);

            SetColumn(text, 0);
            SetColumn(_inlineDate, 1);
            SetColumn(_remaining, 2);

            var grid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = GridLength.Auto },
                },
                Children = { text, _inlineDate, _remaining },
            };

            // A Windows 11 list row: a fixed height, the inset as its own padding so the highlight
            // reaches both edges of the widget, and nothing ruled underneath it.
            Host = Fluent.Row(grid);

            // Held, and dropped in Detach: a handler left hooked keeps the row alive.
            _hover = Fluent.Hover(Host);
            _menu = ItemMenu.Attach(Host, () => menu(_timer.Id));
        }

        public Border Host { get; }

        public void Update(DateTimeOffset now, TimeZoneInfo zone)
        {
            var reading = CountdownFormatter.Read(_timer.Target, now, _timer.Format, Strings.Current);

            _remaining.Text = reading.Text;
            _date.Text = CountdownFormatter.TargetLabel(_timer.Target, now, zone, Strings.Current);
            _inlineDate.Text = _date.Text;

            // [C-40] [S-140] Gone by: the label steps back, the duration carries it in critical.
            _remaining.Foreground = reading.Elapsed ? _critical : _primary;
            _label.Foreground = reading.Elapsed ? _secondary : _primary;

            var progress = _showProgress ? CountdownLayout.Progress(_timer, now) : null;
            _progress.Visibility = progress is null ? Visibility.Collapsed : Visibility.Visible;
            _progress.Value = progress ?? 0;

            // [C-05] The whole instant, for a row whose subtitle is necessarily abbreviated.
            ToolTipService.SetToolTip(Host, CountdownFormatter.FullTargetLabel(_timer.Target, zone, Strings.Current));
        }

        /// <summary>
        /// [C-06] Standard or compact. The second line goes with the compact row rather than being
        /// squeezed into it; the progress bar ([S-1448]) only on the largest tiles.
        /// </summary>
        public void SetDensity(bool compact, bool progress, bool inlineDate)
        {
            Fluent.SetDensity(Host, compact);
            _date.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
            _inlineDate.Visibility = compact && inlineDate ? Visibility.Visible : Visibility.Collapsed;
            _showProgress = progress && !compact;
        }

        /// <summary>Safe to call twice: the handles are idempotent and the child is already gone.</summary>
        public void Detach()
        {
            _hover.Dispose();
            _menu.Dispose();
            Host.Child = null;
        }
    }
}
