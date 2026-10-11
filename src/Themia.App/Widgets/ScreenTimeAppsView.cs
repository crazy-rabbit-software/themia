using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Glyphs = Themia.App.Shell.Glyphs;

namespace Themia.App.Widgets;

/// <summary>
/// [S-3350] The apps of the day (or the week, or the month), busiest first: icon, name, time, and
/// over its own limit the time in the critical colour. A row is hovered or selected for every
/// screen-time widget on the view: the chart and the donut light the same app ([S-3353]).
/// <para>
/// The history is the app's one tracker ([S-458]); the widget has no timer of its own.
/// </para>
/// </summary>
internal sealed partial class ScreenTimeAppsView : Grid, IWidgetView, IWidgetSurfaceAware, IWidgetHeaderContent, IWidgetSizeAware, IWidgetKeys
{
    /// <summary>A row: Fluent's compact list row.</summary>
    private const double RowHeight = Fluent.RowHeightCompact;

    private readonly ScreenTimeLinks _links;
    private readonly ScreenTimeDayNav _nav;
    private readonly StackPanel _list = new() { VerticalAlignment = VerticalAlignment.Top };
    private readonly List<AppRow> _rows = [];
    private readonly StackPanel _empty;
    private readonly TextBlock _emptyText = new();

    private IWidgetSurface? _surface;
    private IScreenTimeSource? _source;
    private ScreenTimeAppsConfig _config = new();
    private ScreenTimeData? _data;
    private IReadOnlyList<ScreenTimeSegment> _segments = [];
    private WidgetArea _area;
    private bool _detached;

    public ScreenTimeAppsView()
    {
        _links = new ScreenTimeLinks(ApplyFocus);
        _nav = new ScreenTimeDayNav(Render);

        // The space under the rows takes taps, so a click there lets the selection go ([S-3302]).
        Background = new SolidColorBrush(Colors.Transparent);
        Tapped += (_, _) =>
        {
            if (_links.ClearSelection())
            {
                ApplyFocus();
            }
        };

        _empty = Fluent.EmptyState(Glyphs.ScreenTime, _emptyText, out _);
        _empty.Visibility = Visibility.Collapsed;

        Children.Add(_list);
        Children.Add(_empty);
    }

    /// <inheritdoc />
    public FrameworkElement? HeaderTrailing => _nav.Host;

    public void Bind(IWidgetSurface surface) => _surface = surface;

    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Render();
    }

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _detached = false;
        _config = config as ScreenTimeAppsConfig ?? new ScreenTimeAppsConfig();
        Strings.Changed -= OnLanguage;
        Strings.Changed += OnLanguage;
        UseSource(AppServices.ScreenTime);
        _links.Join(_surface?.Links);
        Render();
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as ScreenTimeAppsConfig ?? new ScreenTimeAppsConfig();
        Render();
    }

    public void Detach()
    {
        _detached = true;
        Strings.Changed -= OnLanguage;
        UseSource(null);
        _links.Leave();

        foreach (var row in _rows)
        {
            row.Detach();
        }

        _rows.Clear();
        _list.Children.Clear();
    }

    private void UseSource(IScreenTimeSource? source)
    {
        if (ReferenceEquals(_source, source))
        {
            return;
        }

        if (_source is not null)
        {
            _source.Changed -= Render;
        }

        _source = source;

        if (_source is not null)
        {
            _source.Changed += Render;
        }

        _nav.Today = source is null ? null : () => source.Today;
    }

    private void OnLanguage(Locale locale)
    {
        _nav.Retext();
        Render();
    }

    // ---- the list -----------------------------------------------------------------------------

    private void Render()
    {
        if (_detached)
        {
            return;
        }

        _nav.Update(_config.Period, _area);

        if (_source is null)
        {
            ShowEmpty(Str.WidgetsC_ScreenTime_NotRecording.Text());
            return;
        }

        _data = ScreenTimeData.Build(_source, _config.Period, _nav.Pinned);

        if (_data.Total <= 0)
        {
            ShowEmpty((_data.Window ? Str.WidgetsC_ScreenTime_NoActivityPeriod : Str.Screentime_NoActivity).Text());
            return;
        }

        _empty.Visibility = Visibility.Collapsed;
        _list.Visibility = Visibility.Visible;
        Lay(_data);
    }

    private void ShowEmpty(string message)
    {
        _data = null;
        _segments = [];
        _emptyText.Text = message;
        _empty.Visibility = Visibility.Visible;
        _list.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// [S-3351] [S-3352] As many whole rows as the tile has (up to the setting's ceiling); when apps
    /// are left over the last row is "Other", so the rows add up to the total. Rows are reused by
    /// position as the data changes.
    /// </summary>
    private void Lay(ScreenTimeData data)
    {
        if (!_area.IsMeasured)
        {
            return;
        }

        var wanted = ScreenTimeRules.ListWanted(_config.MaxApps, data.Apps.Count);
        var plan = ScreenTimeLayout.Apps(_area, RowHeight, wanted, data.Window, _config.ShowAverage, _config.ShowPercent);
        var named = ScreenTimeRules.ListNamed(plan.Rows, data.Apps.Count);
        _segments = plan.Rows == 0 ? [] : ScreenTimeRules.Segments(data.Apps, data.Total, named, Str.Screentime_Other.Text());
        var rows = Math.Min(plan.Rows, _segments.Count);

        while (_rows.Count < rows)
        {
            var row = new AppRow(this);
            _rows.Add(row);
            _list.Children.Add(row.Host);
        }

        for (var i = 0; i < _rows.Count; i++)
        {
            if (i >= rows)
            {
                _rows[i].Host.Visibility = Visibility.Collapsed;
                continue;
            }

            var segment = _segments[i];
            var average = ScreenTimeRules.DailyAverage(segment.Seconds, data.Days);
            var judged = data.Window ? average : segment.Seconds;
            var goal = segment.IsOther ? null : ScreenTimeRules.Goal(judged, data.AppLimit(segment.Key));

            _rows[i].Host.Visibility = Visibility.Visible;
            _rows[i].Show(
                segment,
                plan.Average ? Str.Screentime_PerDay.Format(("value", ScreenTimeRules.FormatDuration(average))) : null,
                plan.Percent ? ScreenTimeLook.Percent(segment.Seconds, data.Total) : null,
                goal,
                plan.Names);
        }

        ApplyFocus();
    }

    private void Hover(string? key)
    {
        if (_links.PointerHover(key))
        {
            ApplyFocus();
        }
    }

    private void Select(string key)
    {
        _links.Toggle(key);
        ApplyFocus();
    }

    /// <summary>[S-3353] Light the focused row, dim the rest, mark the selection and the keyboard cursor.</summary>
    private void ApplyFocus()
    {
        if (_detached || _data is not { } data)
        {
            return;
        }

        var part = ScreenTimeRules.FocusPart(_links.Focus, _segments, data.Apps);
        var selected = ScreenTimeRules.FocusPart(_links.Selected, _segments, data.Apps);

        foreach (var row in _rows)
        {
            if (row.Key is not { } key || row.Host.Visibility != Visibility.Visible)
            {
                continue;
            }

            row.Mark(ScreenTimeRules.Emphasis(key, part), key == selected, key == _links.Cursor);
        }
    }

    // ---- keys ([S-3307], [S-1989]) -------------------------------------------------------------

    private List<string> VisibleKeys()
        => [.. _rows.Where(row => row.Host.Visibility == Visibility.Visible && row.Key is not null).Select(row => row.Key!)];

    /// <summary>[S-1989] The list, or the days to step, take the keyboard on a click.</summary>
    public bool WantsKeys => !_detached && (VisibleKeys().Count > 0 || ScreenTimeRules.ClampPeriod(_config.Period) == ScreenTimePeriod.Day);

    /// <summary>
    /// [S-3307] [S-1989] Up/Down, Page Up/Down, Home/End move the cursor down the list and light the
    /// app in every screen-time widget on the view; Enter/Space select it; the Menu key hides it;
    /// Left/Right step the day (a day period); Escape lets the cursor go.
    /// </summary>
    public bool HandleKey(WidgetKey key)
    {
        if (_detached)
        {
            return false;
        }

        if (key is WidgetKey.Left or WidgetKey.Right)
        {
            return ScreenTimeRules.ClampPeriod(_config.Period) == ScreenTimePeriod.Day && _nav.StepKey(key == WidgetKey.Right);
        }

        var used = _links.HandleKey(key, VisibleKeys(), app => _rows.Find(r => r.Key == app)?.Host, _source);
        if (used)
        {
            ApplyFocus();
        }

        return used;
    }
}
