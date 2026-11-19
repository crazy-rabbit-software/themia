using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Services.Web;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.RainNextHour;

namespace Themia.App.Widgets;

/// <summary>
/// [S-5120] [S-11140] Rain in the next two hours at a place, on the family's tokens: one sentence ("Rain
/// in 15 min") on a Title line at the content's top, the intensity, total and chance the Body line under
/// it, then a column per quarter hour on a fixed scale (the Chart block's columns) with the window's two
/// ends under them ([S-5126]); the same at every size in its range (<see cref="RainLayout"/>).
/// A dry window, one too short to speak for the hour, or no nowcast yet keeps the columns (their empty tracks)
/// with its one plain sentence as the caption over them ([S-6753]). The sentence is re-read from the held nowcast every minute, so "in 30 min" counts down
/// between fetches.
/// </summary>
internal sealed partial class RainNextHourView : Grid, IWidgetView, IWidgetOpens,
    IWidgetContextActions
{
    /// <inheritdoc />
    public CardOpen? Opens => RainNextHourModule.Opens(_config, RegionFormat.Current.Temperature, Strings.Current);

    /// <summary>[S-7064] The status's problem glyph ([S-7042]): the one stale rule, no age.</summary>
    private readonly KitStatus _status;
    private readonly TextBlock _headline = Family.Text(FamilyType.Title);
    private readonly TextBlock _caption = Family.Text(FamilyType.Body, ColourRole.TextSecondary);
    private readonly StackPanel _top = new();
    private readonly Grid _chart = new() { RowSpacing = RainLayout.LabelGap };
    private readonly Grid _bars = new() { IsHitTestVisible = false };
    private readonly List<ChartBar> _columns = [];
    private readonly TextBlock _timeStart = Family.Text(FamilyType.Caption, ColourRole.TextTertiary);
    private readonly TextBlock _timeEnd = Family.Text(FamilyType.Caption, ColourRole.TextTertiary);

    private RainNextHourConfig _config = new();
    private readonly SkyFeed<RainInterest, RainNowcast> _feed;

    /// <summary>Debug renders pin the clock so the fixture's window stays where it was drawn.</summary>
    /// <summary>[S-11850] A sample's fixed moment; null on the desktop.</summary>
    private DateTimeOffset? _pinnedNow;

    public RainNextHourView(IWidgetHost host)
    {
        _status = new KitStatus(host);
        _feed = new(
            () => new(h => host.Weather.Updated += h, h => host.Weather.Updated -= h, host.Weather.Key, host.Weather.Rain, host.Weather.Show),
            () => _config.Interest(),
            Render);
        _top.Margin = Family.SideInset;
        _top.VerticalAlignment = VerticalAlignment.Top;
        _headline.TextWrapping = TextWrapping.NoWrap;
        _headline.TextTrimming = TextTrimming.CharacterEllipsis;
        _caption.TextTrimming = TextTrimming.CharacterEllipsis;
        _top.Children.Add(_headline);
        _top.Children.Add(_caption);

        // [S-1113] Time runs left to right in every language, as on the stock chart.
        _chart.FlowDirection = FlowDirection.LeftToRight;
        _chart.Margin = new Thickness(FamilySpace.Inset, 0, FamilySpace.Inset, FamilySpace.Inset);
        _chart.VerticalAlignment = VerticalAlignment.Bottom;
        _chart.Height = RainLayout.Chart;
        _chart.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _chart.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (var i = 0; i < RainRules.WindowSteps; i++)
        {
            _bars.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var column = new ChartBar(vertical: true);
            SetColumn(column, i);
            _columns.Add(column);
            _bars.Children.Add(column);
        }

        _timeEnd.HorizontalAlignment = HorizontalAlignment.Right;
        SetRow(_timeStart, 1);
        SetRow(_timeEnd, 1);
        _chart.Children.Add(_bars);
        _chart.Children.Add(_timeStart);
        _chart.Children.Add(_timeEnd);

        Children.Add(_top);
        Children.Add(_chart);
        _status.Place(this);
    }

    public IReadOnlyList<WidgetAction> ContextActions =>
        _config.HasLocation ? [new WidgetAction(Glyphs.Refresh, OnlineParts.RefreshLabel(_feed.Held), _feed.Refresh)] : [];

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _config = config as RainNextHourConfig ?? new RainNextHourConfig();

        // The sentence counts down with the clock; a minute is the finest it changes.
        _feed.Attach(ticker, TimeSpan.FromMinutes(1));
        ApplyConfig(config);
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as RainNextHourConfig ?? new RainNextHourConfig();
        _feed.Reload();
    }

    /// <summary>
    /// [S-6753] The window's columns are always drawn (owner, 2026-11-19: "even if there is no rain in the next two hours
    /// ... it should show the default values in the graph"): no nowcast, a dry window or one too short is the empty
    /// tracks with its sentence as the caption over them, never a Status row in their place.
    /// </summary>
    private void Render()
    {
        var now = _pinnedNow ?? DateTimeOffset.Now;
        var outlook = _feed.Held is { Value: { } cast } ? RainRules.Outlook(cast, now) : null;
        if (_feed.Held is { Value: not null } held)
        {
            _status.Show(OnlineParts.Problems(held), busy: false);
        }
        else
        {
            _status.Clear();
        }

        var warns = outlook is not null && RainLayout.Warns(outlook.Headline);
        _headline.Visibility = warns ? Visibility.Visible : Visibility.Collapsed;
        _headline.Text = warns ? RainText.Headline(outlook!) : string.Empty;
        _caption.Text = warns ? RainText.Caption(outlook!) : outlook is not null ? RainText.Headline(outlook) : OnlineParts.Caption(_feed.Status);

        var start = (outlook is { Steps.Count: > 0 } ? outlook.Steps[0].Start : now).ToLocalTime();
        _timeStart.Text = RainNextHourStr.Online_Rain_Now.Text();
        _timeEnd.Text = LocaleFormat.Time(TimeOnly.FromDateTime(start.AddHours(2).DateTime));

        // [S-5125] A column per quarter hour on a fixed scale; a dry one is its empty track.
        for (var i = 0; i < _columns.Count; i++)
        {
            _columns[i].Show(warns && i < outlook!.Steps.Count ? RainRules.BarShare(outlook.Steps[i].Millimetres) : 0);
        }
    }

    public void Detach() => _feed.Detach();

}
