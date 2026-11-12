using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.App.Widgets;
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
/// line under it (a day's limit, a window's average and limit), the period in the header as its label
/// (a day with its arrows, [S-454]). [S-11503] It joins the view's links: a focused app's time over the
/// period, and its share, stand in place of the total.
/// </summary>
internal sealed partial class ScreenTimeView : Grid, IWidgetView, IWidgetSurfaceAware, IWidgetHeaderContent, IWidgetContextActions
{
    private readonly ScreenTimeLinks _links;
    private readonly ScreenTimeDayNav _nav;
    private readonly ReadoutBlock _readout = new() { Margin = Family.SideInset, VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Left };

    /// <summary>[S-6714] Nothing to read: the Status block's one row at the top.</summary>
    private readonly StatusBlock _empty = new() { Visibility = Visibility.Collapsed };

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
        _links = new ScreenTimeLinks(ApplyFocus);
        _nav = new ScreenTimeDayNav(Render);
        _readout.Use(FamilyType.Big28);
        Children.Add(_readout);
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
        _config = config as ScreenTimeConfig ?? new ScreenTimeConfig();
        Strings.Changed -= OnLanguage;
        Strings.Changed += OnLanguage;
        UseSource(_host.ScreenTime);
        _links.Join(_surface?.Links);
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
            ShowEmpty(Str.WidgetsC_ScreenTime_NotRecording.Text());
            return;
        }

        _data = ScreenTimeData.Build(_source, _config.Period, _nav.Pinned);
        if (_data.Total <= 0)
        {
            ShowEmpty((_data.Window ? Str.WidgetsC_ScreenTime_NoActivityPeriod : Str.Screentime_NoActivity).Text());
            return;
        }

        _segments = ScreenTimeRules.Segments(_data.Apps, _data.Total, ScreenTimeRules.DrawingApps, Str.Screentime_Other.Text());
        _empty.Visibility = Visibility.Collapsed;
        _readout.Visibility = Visibility.Visible;
        ApplyFocus();
    }

    private void ShowEmpty(string message)
    {
        _data = null;
        _segments = [];
        _empty.Show(StatusKind.Empty, Glyphs.Hours, message);
        _empty.Visibility = Visibility.Visible;
        _readout.Visibility = Visibility.Collapsed;
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
