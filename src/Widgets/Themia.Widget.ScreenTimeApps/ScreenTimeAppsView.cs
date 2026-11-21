using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Links;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.ScreenTimeApps;
using Glyphs = Themia.App.Shell.Glyphs;

namespace Themia.App.Widgets;

/// <summary>
/// [S-3350] The apps of the day (or the week, or the month), busiest first, as a kit list ([S-7074]) shaped by
/// <see cref="ScreenTimeAppsSpec"/> under the day's line ([S-454]). A row is hovered or selected for every screen-time
/// widget on the view: the chart and the donut light the same app ([S-3353]), and a click picks it.
/// <para>
/// The history is the app's one tracker ([S-458]); the widget has no timer of its own.
/// </para>
/// </summary>
internal sealed partial class ScreenTimeAppsView : Grid, IWidgetView, IWidgetSurfaceAware, IWidgetKeys, IWidgetContextActions, IWidgetSizeAware
{
    /// <summary>Nothing on a row ages: the list is redrawn when the tracker says; this is only the binding's slowest tick.</summary>
    private static readonly TimeSpan Redraw = TimeSpan.FromMinutes(15);

    private readonly ScreenTimeLinks _links;
    private readonly ScreenTimeDayNav _nav;

    /// <summary>[S-11928] The day selected on the view, followed.</summary>
    private readonly WidgetLinks _dates = new();

    /// <summary>[S-450] The tracker, followed as every screen-time widget follows it.</summary>
    private readonly ScreenTimeFeed _feed;
    private readonly KitBinding _binding;
    private readonly AppsSource _source;

    /// <summary>Each row the list drew and its app, for the links' one look; and the rows already hooked for the pointer.</summary>
    private readonly Dictionary<FrameworkElement, string> _rows = [];
    private readonly ConditionalWeakTable<FrameworkElement, object> _hooked = [];

    private IWidgetSurface? _surface;
    private ScreenTimeAppsConfig _config = new();
    private ScreenTimeData? _data;
    private IReadOnlyList<ScreenTimeSegment> _segments = [];
    private WidgetArea _area;
    private bool _attached;

    private readonly IWidgetHost _host;

    public ScreenTimeAppsView(IWidgetHost host)
    {
        _host = host;
        _links = new ScreenTimeLinks(ApplyFocus, LinkTopics.HoveredApp, LinkTopics.SelectedApp);
        _nav = new ScreenTimeDayNav(Changed);
        _feed = new ScreenTimeFeed(Changed, _nav);
        _source = new AppsSource(this);
        _binding = new KitBinding(host, ScreenTimeAppsSpec.Spec, Glyphs.ScreenTime, ScreenTimeAppsSpec.Actions, Run)
        {
            Look = new KitRichLook { Lead = item => AppIcon(item), LargeOnTwoLines = true },
            Decorate = HookRow,
            Redrawing = _rows.Clear,
            CursorMoved = item => Hover(item?.Id),
        };

        // [S-11928] A day selected on the view (a calendar's click) is the day a Day period shows.
        _dates.Follow(LinkTopics.SelectedDate, value =>
        {
            if (ScreenTimeRules.ClampPeriod(_config.Period) == ScreenTimePeriod.Day && _nav.Select(value?.Date))
            {
                Changed();
            }
        });

        // The space under the rows takes taps, so a click there lets the selection go ([S-3302]).
        Background = Family.Brush(ColourRole.Clear);
        Tapped += (_, _) =>
        {
            if (_links.ClearSelection())
            {
                ApplyFocus();
            }
        };

        // [S-454] [S-12481] The day and its arrows, the content's first line; the list under it.
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _nav.Line.Margin = Family.SideInset;
        SetRow(_binding.List, 1);
        SetRow(_binding.State, 1);
        Children.Add(_nav.Line);
        Children.Add(_binding.List);
        Children.Add(_binding.State);
        _binding.Status.Place(this);
    }

    /// <summary>[S-454] Back to today, while a past day is pinned.</summary>
    public IReadOnlyList<WidgetAction> ContextActions => _nav.ContextActions;

    public void Bind(IWidgetSurface surface) => _surface = surface;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _config = config as ScreenTimeAppsConfig ?? new ScreenTimeAppsConfig();
        _attached = true;
        _feed.Attach(_host.ScreenTime);
        _links.Join(_surface?.Links);
        _dates.Join(_surface?.Links);
        _nav.Update(_config.Period);
        _binding.Attach(ticker, _source, _config, Redraw);
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as ScreenTimeAppsConfig ?? new ScreenTimeAppsConfig();
        if (_attached)
        {
            _nav.Update(_config.Period);
            Size();
            _binding.Apply(_source, _config, Redraw, show: false);
        }
    }

    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Size();
    }

    public void Detach()
    {
        _attached = false;
        _feed.Detach();
        _links.Leave();
        _dates.Leave();
        _binding.Dispose();
        _rows.Clear();
    }

    /// <summary>[S-12815] Under the day line the rows start under it and have the room under it; with none, on the card's top edge.</summary>
    private void Size()
    {
        var line = _nav.Line.Visibility == Visibility.Visible;
        _binding.List.UnderLine = line;
        var over = line ? ScreenTimeAppsModule.DayLine(_config.Period) : 0;
        _binding.Size(_area.IsMeasured ? _area with { Height = Math.Max(0, _area.Height - over) } : _area);
    }

    private void Changed()
    {
        if (_attached)
        {
            _nav.Update(_config.Period);
            _source.Raise();
        }
    }

    /// <summary>
    /// [S-3351] [S-11510] The rows the card reaches; when apps are left over the last row is "Other", so the rows add up to
    /// the total. Nothing recorded, or no activity in the period, is the state row.
    /// </summary>
    private KitRead Read()
    {
        if (_feed.Source is null)
        {
            _data = null;
            _segments = [];
            return new KitRead([], [], Stated: new KitStated(Str.WidgetsC_ScreenTime_NotRecording.Text(), Problem: false));
        }

        var data = ScreenTimeData.Build(_feed.Source, _config.Period, _nav.Pinned);
        _data = data;
        if (data.Total <= 0)
        {
            _segments = [];
            return new KitRead([], [], Stated: new KitStated((data.Window ? Str.WidgetsC_ScreenTime_NoActivityPeriod : Str.Screentime_NoActivity).Text(), Problem: false));
        }

        var shown = _config.RowsShown;
        var held = _binding.Area.IsMeasured ? KitPlanner.Reach(_binding.Area, shown.Lines).Shown : shown.Rows;
        var plan = ScreenTimeAppsLayout.Apps(_config, held);
        var named = ScreenTimeRules.ListNamed(plan.Rows, data.Apps.Count);
        _segments = plan.Rows == 0 ? [] : ScreenTimeRules.Segments(data.Apps, data.Total, named, Str.Screentime_Other.Text());
        var average = data.Window && _config.ShowAverage;
        var locale = Strings.Current;

        var items = new List<KitItem>();
        foreach (var segment in _segments.Take(plan.Rows))
        {
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

            items.Add(ScreenTimeAppsSpec.Item(segment, figures.Count == 0 ? null : string.Join(" · ", figures), goal, locale));
        }

        return new KitRead(items, [new KitOutcome(null, FetchStatus.Ok)]);
    }

    /// <summary>[S-2272] The app's icon at the lead's size; while it loads, or when it has none, the dot of its tone.</summary>
    private FrameworkElement AppIcon(KitItem item)
    {
        var size = _config.RowsShown.Lines > 1 ? FamilyShape.IconLarge : FamilyShape.Icon;
        var tones = ScreenTimeRules.Tones(_segments);
        var tone = ScreenTimeRules.ToneOf(tones, item.Id);
        var dot = new Border
        {
            Width = FamilyShape.Icon,
            Height = FamilyShape.Icon,
            CornerRadius = Family.Round(FamilyShape.Icon, FamilyShape.Icon),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Background = ScreenTimeLook.Brush(tone),
            Opacity = tone.Alpha,
        };

        var icon = new Grid { Width = size, Height = size, VerticalAlignment = VerticalAlignment.Center, Children = { dot } };
        var lead = new AppIconLead(dot, () => icon);
        lead.Image.Width = size;
        lead.Image.Height = size;
        icon.Children.Add(lead.Image);
        icon.ActualThemeChanged += (_, _) => lead.ThemeChanged();
        lead.Show(_segments.FirstOrDefault(s => s.Key == item.Id)?.Path);
        return icon;
    }

    /// <summary>[S-3350] Hide an app from every screen-time widget.</summary>
    private void Run(KitItem item, KitAction action)
    {
        if (action.Id == ScreenTimeAppsSpec.HideId && item.Id != ScreenTimeRules.OtherKey)
        {
            ScreenTimeLook.Hide(_feed.Source, item.Id);
        }
    }

    /// <summary>[S-3353] A row the list drew: its app, lit by the links, the pointer on it the view's hover, a click its pick.</summary>
    private void HookRow(KitItem item, FrameworkElement row)
    {
        _rows[row] = item.Id;
        if (_hooked.TryGetValue(row, out _))
        {
            return;
        }

        _hooked.Add(row, row);
        row.PointerEntered += (_, _) => Hover(_rows.GetValueOrDefault(row), pointer: true);
        row.PointerExited += (_, _) => Hover(null, pointer: true);
        row.PointerCanceled += (_, _) => Hover(null, pointer: true);
        row.Tapped += (_, _) =>
        {
            if (_rows.GetValueOrDefault(row) is { } app)
            {
                Select(app);
            }
        };
        ApplyFocus();
    }

    private void Hover(string? key, bool pointer = false)
    {
        if (pointer ? _links.PointerHover(key) : _links.Hover(key))
        {
            ApplyFocus();
        }
    }

    private void Select(string key)
    {
        _links.Toggle(key);
        ApplyFocus();
    }

    /// <summary>[S-3353] [S-11901] Light the row pointed at and the row picked (the links' one look).</summary>
    private void ApplyFocus()
    {
        if (!_attached || _data is not { } data)
        {
            return;
        }

        var pointed = ScreenTimeRules.FocusPart(_links.Pointed, _segments, data.Apps);
        var picked = ScreenTimeRules.FocusPart(_links.Picked, _segments, data.Apps);
        foreach (var (row, key) in _rows)
        {
            LinkLook.Row(row, LinkEmphasis.Light(key, pointed, picked));
        }
    }

    // ---- keys ([S-3307], [S-1989]) -------------------------------------------------------------

    /// <summary>[S-1989] The list, or the days to step, take the keyboard on a click.</summary>
    public bool WantsKeys => _attached && (_binding.List.WantsKeys || ScreenTimeRules.ClampPeriod(_config.Period) == ScreenTimePeriod.Day);

    /// <summary>
    /// [S-3307] [S-1989] The kit's keys move the cursor down the list, which lights the app in every screen-time widget on
    /// the view; Enter and Space pick it; Left/Right step the day (a day period); Escape lets the cursor go.
    /// </summary>
    public bool HandleKey(WidgetKey key)
    {
        if (!_attached)
        {
            return false;
        }

        if (key is WidgetKey.Left or WidgetKey.Right)
        {
            return ScreenTimeRules.ClampPeriod(_config.Period) == ScreenTimePeriod.Day && _nav.StepKey(key == WidgetKey.Right);
        }

        var index = _binding.List.CursorIndex;
        if (key is WidgetKey.Enter or WidgetKey.Space && index >= 0 && index < _binding.List.Items.Count)
        {
            Select(_binding.List.Items[index].Id);
            return true;
        }

        return _binding.HandleKey(key);
    }

    /// <summary>[S-450] The tracker as a kit source: the apps of the period as the card holds them.</summary>
    private sealed class AppsSource(ScreenTimeAppsView owner) : IKitSource
    {
        public event Action? Changed;

        public bool Busy => false;

        public void Show(bool refresh)
        {
            // ignore: the tracker says every change itself.
        }

        public KitRead Read() => owner.Read();

        public void Raise() => Changed?.Invoke();
    }
}
