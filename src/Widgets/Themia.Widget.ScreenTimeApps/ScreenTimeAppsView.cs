using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Glyphs = Themia.App.Shell.Glyphs;

namespace Themia.App.Widgets;

/// <summary>
/// [S-3350] The apps of the day (or the week, or the month), busiest first, as the family's rows
/// ([S-6711]): icon, name, time, and over its own limit the time in the critical role. A row is hovered or selected for every
/// screen-time widget on the view: the chart and the donut light the same app ([S-3353]).
/// <para>
/// The history is the app's one tracker ([S-458]); the widget has no timer of its own.
/// </para>
/// </summary>
internal sealed partial class ScreenTimeAppsView : Grid, IWidgetView, IWidgetSurfaceAware, IWidgetHeaderContent, IWidgetKeys, IWidgetContextActions
{
    private readonly ScreenTimeLinks _links;
    private readonly ScreenTimeDayNav _nav;
    private readonly StackPanel _list = new() { VerticalAlignment = VerticalAlignment.Top };
    private readonly List<AppRow> _rows = [];

    /// <summary>[S-6714] Nothing to list: the Status block's one row at the top.</summary>
    private readonly StatusBlock _empty = new() { Visibility = Visibility.Collapsed };

    private IWidgetSurface? _surface;
    private IScreenTimeSource? _source;
    private ScreenTimeAppsConfig _config = new();
    private ScreenTimeData? _data;
    private IReadOnlyList<ScreenTimeSegment> _segments = [];
    private bool _detached;

    private readonly IWidgetHost _host;

    public ScreenTimeAppsView(IWidgetHost host)
    {
        _host = host;
        _links = new ScreenTimeLinks(ApplyFocus);
        _nav = new ScreenTimeDayNav(Render);

        // The space under the rows takes taps, so a click there lets the selection go ([S-3302]).
        Background = Family.Brush(ColourRole.Clear);
        Tapped += (_, _) =>
        {
            if (_links.ClearSelection())
            {
                ApplyFocus();
            }
        };

        Children.Add(_list);
        Children.Add(_empty);
    }

    /// <inheritdoc />
    public FrameworkElement? HeaderTrailing => _nav.Host;

    /// <summary>[S-454] Back to today, while a past day is pinned.</summary>
    public IReadOnlyList<WidgetAction> ContextActions => _nav.ContextActions;

    public void Bind(IWidgetSurface surface) => _surface = surface;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _detached = false;
        _config = config as ScreenTimeAppsConfig ?? new ScreenTimeAppsConfig();
        Strings.Changed -= OnLanguage;
        Strings.Changed += OnLanguage;
        UseSource(_host.ScreenTime);
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

        ClearRows();
    }

    private void ClearRows()
    {
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

        _nav.Update(_config.Period);

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
        _empty.Show(StatusKind.Empty, Glyphs.ScreenTime, message);
        _empty.Visibility = Visibility.Visible;
        _list.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// [S-3351] [S-11510] The config's rows; when apps are left over the last row is "Other", so the
    /// rows add up to the total. Rows are reused by position as the data changes.
    /// </summary>
    private void Lay(ScreenTimeData data)
    {
        var average = data.Window && _config.ShowAverage;
        var plan = ScreenTimeAppsLayout.Apps(_config);
        var named = ScreenTimeRules.ListNamed(plan.Rows, data.Apps.Count);
        _segments = plan.Rows == 0 ? [] : ScreenTimeRules.Segments(data.Apps, data.Total, named, Str.Screentime_Other.Text());
        var rows = Math.Min(plan.Rows, _segments.Count);
        var tones = ScreenTimeRules.Tones(_segments);

        // A row's lead is made for its size: rows of another lead are made again.
        if (_rows.Count > 0 && !_rows[0].Lead.Equals(plan.Lead))
        {
            ClearRows();
        }

        while (_rows.Count < rows)
        {
            var row = new AppRow(this, plan.Lead);
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
            var perDay = ScreenTimeRules.DailyAverage(segment.Seconds, data.Days);
            var judged = data.Window ? perDay : segment.Seconds;
            var goal = segment.IsOther ? null : ScreenTimeRules.Goal(judged, data.AppLimit(segment.Key));

            // [S-3350] The second line: the share, then a window's average, as far as they are on.
            List<string> figures = [];
            if (_config.ShowPercent)
            {
                figures.Add(ScreenTimeLook.Percent(segment.Seconds, data.Total));
            }

            if (average)
            {
                figures.Add(Str.Screentime_PerDay.Format(("value", ScreenTimeRules.FormatDuration(perDay))));
            }

            _rows[i].Host.Visibility = Visibility.Visible;
            _rows[i].Show(segment, ScreenTimeRules.ToneOf(tones, segment.Key), figures.Count == 0 ? null : string.Join(" · ", figures), goal, plan);
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
