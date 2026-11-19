using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.App.Widgets;
using Themia.Core.Links;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Glyphs = Themia.App.Shell.Glyphs;

namespace Themia.Widget.ScreenTime;

/// <summary>
/// [S-11500] [S-11501] The period's screen time as a reading: the family's 28 at the content's top, one
/// line under it (a day's limit, a window's average and limit), the period on its own first line, a tool
/// line (a day with its arrows, [S-454], [S-12481]). [S-11503] It joins the view's links: a focused app's time over the
/// period, and its share, stand in place of the total.
/// </summary>
internal sealed partial class ScreenTimeView : Grid, IWidgetView, IWidgetSurfaceAware, IWidgetContextActions
{
    private readonly ScreenTimeLinks _links;
    private readonly ScreenTimeDayNav _nav;

    /// <summary>[S-11928] The day selected on the view, followed.</summary>
    private readonly WidgetLinks _dates = new();
    /// <summary>[S-12410] One block, centred in the room under its day line with the foot's inset counted with it ([S-12483]: the same joined or alone).</summary>
    private readonly ReadoutBlock _readout = new() { HorizontalAlignment = HorizontalAlignment.Left };

    /// <summary>[S-12808] The block, planned at the natural box and scaled up as one.</summary>
    private readonly ReadingFit _fit = new();

    private readonly IWidgetHost _host;
    private IWidgetSurface? _surface;
    private IScreenTimeSource? _source;
    private ScreenTimeConfig _config = new();
    private ScreenTimeData? _data;
    private IReadOnlyList<ScreenTimeSegment> _segments = [];
    private bool _detached;

    public ScreenTimeView(IWidgetHost host)
    {
        _host = host;
        _links = new ScreenTimeLinks(ApplyFocus, LinkTopics.HoveredApp, LinkTopics.SelectedApp);
        _nav = new ScreenTimeDayNav(Render);

        // [S-11928] A day selected on the view (a calendar's click) is the day a Day period shows.
        _dates.Follow(LinkTopics.SelectedDate, value =>
        {
            if (ScreenTimeRules.ClampPeriod(_config.Period) == ScreenTimePeriod.Day && _nav.Select(value?.Date))
            {
                Render();
            }
        });
        _readout.Use(FamilyType.Big28);

        // [S-454] [S-12481] The day and its arrows, the content's first line.
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _nav.Line.Margin = Family.SideInset;
        SetRow(_fit, 1);
        Children.Add(_nav.Line);
        _fit.Parts.Add(_readout);
        Children.Add(_fit);
    }

    /// <summary>[S-454] Back to today, while a past day is pinned.</summary>
    public IReadOnlyList<WidgetAction> ContextActions => _nav.ContextActions;

    public void Bind(IWidgetSurface surface) => _surface = surface;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _detached = false;
        _config = config as ScreenTimeConfig ?? new ScreenTimeConfig();
        Strings.Changed -= OnLanguage;
        Strings.Changed += OnLanguage;
        UseSource(_host.ScreenTime);
        _links.Join(_surface?.Links);
        _dates.Join(_surface?.Links);
        Render();
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as ScreenTimeConfig ?? new ScreenTimeConfig();
        Render();
    }

    public void Detach()
    {
        _detached = true;
        Strings.Changed -= OnLanguage;
        UseSource(null);
        _links.Leave();
        _dates.Leave();
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

    private void Render()
    {
        if (_detached)
        {
            return;
        }

        _nav.Update(_config.Period);

        if (_source is null)
        {
            ShowEmpty("--", Str.WidgetsC_ScreenTime_NotRecording.Text());
            return;
        }

        _data = ScreenTimeData.Build(_source, _config.Period, _nav.Pinned);
        if (_data.Total <= 0)
        {
            ShowEmpty(ScreenTimeRules.FormatDuration(0), (_data.Window ? Str.WidgetsC_ScreenTime_NoActivityPeriod : Str.Screentime_NoActivity).Text());
            return;
        }

        _segments = ScreenTimeRules.Segments(_data.Apps, _data.Total, ScreenTimeRules.DrawingApps, Str.Screentime_Other.Text());
        _readout.Visibility = Visibility.Visible;
        ApplyFocus();
    }

    /// <summary>
    /// [S-12813] Nothing to read: the readout stays, <paramref name="value"/> ("--" while nothing records, none
    /// spent in a period that recorded) drawn unavailable, <paramref name="message"/> its line under.
    /// </summary>
    private void ShowEmpty(string value, string message)
    {
        _data = null;
        _segments = [];
        _readout.Visibility = Visibility.Visible;
        _readout.Show(value, under: message, valueRole: ColourRole.TextDisabled);
        _readout.UnderText.TextTrimming = TextTrimming.CharacterEllipsis;
    }

    /// <summary>[S-11503] The total and its line, or the focused app's time over the period and its share.</summary>
    private void ApplyFocus()
    {
        if (_detached || _data is not { } data)
        {
            return;
        }

        var part = ScreenTimeRules.FocusPart(_links.Focus, _segments, data.Apps);
        var reading = part is not null && _segments.FirstOrDefault(s => s.Key == part) is { } focused
            ? new ScreenTimeReadingText(focused.Seconds, $"{focused.Name} · {ScreenTimeLook.Percent(focused.Seconds, data.Total)}", false)
            : ScreenTimeReading.Headline(data.Window, data.Total, data.Days, data.DayLimit);

        _readout.Show(
            ScreenTimeRules.FormatDuration(reading.Seconds),
            under: reading.Line.Length == 0 ? null : reading.Line,
            valueRole: reading.Over ? ColourRole.Critical : ColourRole.TextPrimary);
        _readout.UnderText.TextTrimming = TextTrimming.CharacterEllipsis;
    }
}
