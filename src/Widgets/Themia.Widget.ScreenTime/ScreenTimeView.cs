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
    private readonly WidgetLinks _dates;
    /// <summary>[S-12410] [S-14282] The reading under its day line: one block ([S-12483]: the same joined or alone).</summary>
    private readonly ReadoutBlock _readout = new() { HorizontalAlignment = HorizontalAlignment.Left };

    /// <summary>[S-12808] The block, planned at the natural box and scaled up as one.</summary>
    private readonly ReadingFit _fit = new();

    private IWidgetSurface? _surface;
    /// <summary>[S-450] [S-3310] The tracker, the day line, the app links and the dates: joined and left as one.</summary>
    private readonly ScreenTimeWiring _wiring;
    /// <summary>[S-450] The tracker, followed as every screen-time widget follows it.</summary>
    private readonly ScreenTimeFeed _feed;
    private ScreenTimeConfig _config = new();
    private ScreenTimeData? _data;
    private IReadOnlyList<ScreenTimeSegment> _segments = [];

    public ScreenTimeView(IWidgetHost host)
    {
        _wiring = new ScreenTimeWiring(host, Render, ApplyFocus);
        (_links, _nav, _feed, _dates) = (_wiring.Links, _wiring.Nav, _wiring.Feed, _wiring.Dates);

        // [S-11928] A day selected on the view (a calendar's click) is the day a Day period shows.
        _wiring.FollowSelectedDay(() => _config.Period);
        _readout.Use(FamilyType.Big28);

        // [S-454] [S-12481] [S-14282] The day and its arrows stand right over the reading, as wide as it, one block with
        // it that the card scales as one (WR-7, owner 2026-11-19: "Keep them together"; "All together, scaled").
        _fit.Parts.Add(new StackPanel { HorizontalAlignment = HorizontalAlignment.Left, Children = { _nav.Line, _readout } });
        _fit.Face = ScreenTimeReading.Face();
        Children.Add(_fit);
    }

    /// <summary>[S-454] Back to today, while a past day is pinned.</summary>
    public IReadOnlyList<WidgetAction> ContextActions => _nav.ContextActions;

    public void Bind(IWidgetSurface surface) => _surface = surface;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _config = config as ScreenTimeConfig ?? new ScreenTimeConfig();
        _wiring.Attach(_surface);
        Render();
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as ScreenTimeConfig ?? new ScreenTimeConfig();
        Render();
    }

    public void Detach()
    {
        _wiring.Detach();
    }

    private void Render()
    {
        if (_wiring.Detached)
        {
            return;
        }

        _nav.Update(_config.Period);

        if (_feed.Source is null)
        {
            ShowEmpty("--", Str.WidgetsC_ScreenTime_NotRecording.Text());
            return;
        }

        _data = ScreenTimeData.Build(_feed.Source, _config.Period, _nav.Pinned);
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
        if (_wiring.Detached || _data is not { } data)
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
