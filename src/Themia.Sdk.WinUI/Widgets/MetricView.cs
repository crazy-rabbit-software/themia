using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;

namespace Themia.App.Widgets;

/// <summary>
/// [S-14600]..[S-14603] The one face of every single-metric widget (owner, 2026-11-20: "split all the other widgets
/// that are alike to the weather and air facts into individual metrics too. use the same base component for all of
/// them"): one fact row that is the card, from its top edge to its foot edge (<see cref="MetricRules.Sizing"/>): the
/// kind's 16 glyph, the metric's name, its value with its unit at the end and, as the widget's trend says, an arrow
/// beside the value (in the row's inline slot, <see cref="RowBlock.Meter"/>) or the trend line: the meters' history band
/// (<see cref="SparkBand"/>) behind the whole row, the card edge to edge, the row standing over it ([S-14220]). Without a
/// reading the row stays with "--" drawn unavailable and why as its tooltip ([S-14602]); a reading kept through a failed
/// refresh stays and the status's problem glyph says so, the row leaving the glyph its room at its end. A family
/// subclasses it for its data and hands each read to <see cref="Present"/>; the face, the trend and the readings an
/// arrow compares are this base's alone.
/// </summary>
public abstract partial class MetricView : Grid, IWidgetView, IWidgetOpens, IWidgetContextActions
{
    private readonly KitStatus _status;

    /// <summary>[S-12811] Its one row is the facts' row, the one component every fact row is (a list of one).</summary>
    private readonly FactList _row = new();
    private readonly string _glyph;
    private readonly MetricReadings _readings = new();
    private FontIcon? _arrow;
    private SparkBand? _band;
    private MetricShown? _shown;
    private MetricTrend _trend;
    private WidgetConfig? _config;

    /// <param name="host">The card's host: the status glyph's account link.</param>
    /// <param name="glyph">The kind's glyph, leading the row.</param>
    /// <param name="history">What history the kind's metric has: which trends it can draw.</param>
    protected MetricView(IWidgetHost host, string glyph, MetricHistory history)
    {
        History = history;
        _glyph = glyph;
        _status = new KitStatus(host);
        Children.Add(_row);
        _status.Place(this);
    }

    /// <summary>[S-14603] What history the kind's metric has.</summary>
    protected MetricHistory History { get; }

    /// <summary>[S-14603] The trend drawn now (the config's, as the kind can draw it).</summary>
    protected MetricTrend Trend => _trend;

    /// <summary>The metric's name in the row, in the current language.</summary>
    protected abstract string MetricName { get; }

    /// <inheritdoc />
    public virtual CardOpen? Opens => null;

    /// <inheritdoc />
    public virtual IReadOnlyList<WidgetAction> ContextActions => [];

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        Strings.Changed -= OnLanguage;
        Strings.Changed += OnLanguage;
        Follow(config);
        Draw();
        OnAttach(ticker, config);
    }

    public void ApplyConfig(WidgetConfig config)
    {
        Follow(config);
        OnApply(config);
        Draw();
    }

    public void Detach()
    {
        Strings.Changed -= OnLanguage;
        OnDetach();
    }

    /// <summary>Hold the family's data for <paramref name="config"/>, presenting what it holds at once.</summary>
    protected abstract void OnAttach(Ticker ticker, WidgetConfig config);

    /// <summary>A changed config: what the data is about may differ (another place, drive, host).</summary>
    protected abstract void OnApply(WidgetConfig config);

    /// <summary>Let go of the data. Safe twice.</summary>
    protected abstract void OnDetach();

    /// <summary>
    /// [S-14603] The readings an arrow compares are this metric's: another place, drive, adapter or host starts them again.
    /// A family calls it when what its data is about changes.
    /// </summary>
    protected void Restart() => _readings.Clear();

    /// <summary>[S-14601] Put one read on the row: its value, its trend and its state.</summary>
    protected void Present(MetricShown shown)
    {
        ArgumentNullException.ThrowIfNull(shown);
        _shown = shown;
        if (shown.Value.Number is not null)
        {
            _readings.Add(shown.Reading, shown.Value);
        }

        Draw();
    }

    private void Follow(WidgetConfig config)
    {
        _config = config;
        _trend = config is IMetricTrend trend ? MetricRules.Effective(trend.Trend, History) : MetricTrend.None;
    }

    /// <summary>Another language: the family writes its value again, the row its name.</summary>
    private void OnLanguage(Locale locale)
    {
        if (_config is { } config)
        {
            OnApply(config);
        }

        Draw();
    }

    private void Draw()
    {
        var shown = _shown ?? new MetricShown(MetricValue.Unread);
        var value = shown.Value;
        _row.Show([new FactLine(_glyph, shown.Name ?? MetricName, value.Text, value.Available)]);

        // [S-14602] "--" says there is no reading; why is the row's tooltip (a status glyph would stand over the value).
        // A reading kept through a failed refresh stays, and the status's problem glyph says so, its room at the row's end.
        ToolTipService.SetToolTip(_row, !value.Available && shown.Why is { Length: > 0 } why ? why : null);
        if (value.Available)
        {
            _status.Show(shown.Problems, busy: false);
        }
        else
        {
            _status.Clear();
        }

        var trend = _trend;
        PreviewTrend(ref shown, ref trend);
        DrawTrend(shown, trend);
    }

    /// <summary>Debug renders: a trend state's arrow or line over the reading shown ([S-14605]); nothing in a release.</summary>
    partial void PreviewTrend(ref MetricShown shown, ref MetricTrend trend);

    /// <summary>
    /// [S-14603] The trend: the arrow beside the value against the reading before (as the source says, or against the last
    /// this widget saw), the line over the metric's history on the band behind the row ([S-14220]); with nothing to compare
    /// the arrow's room stays empty and the band lies flat at its baseline (never no trend, WR-10, never a row that shifts
    /// when one arrives, WR-12).
    /// </summary>
    private void DrawTrend(MetricShown shown, MetricTrend trend)
    {
        if (_band is not null)
        {
            _band.Visibility = trend == MetricTrend.Line ? Visibility.Visible : Visibility.Collapsed;
        }

        switch (trend)
        {
            case MetricTrend.Arrow:
                _arrow ??= Family.Glyph(Glyphs.TrendFlat, FamilyShape.Icon, ColourRole.TextSecondary);
                var direction = shown.Direction ?? MetricRules.Direction(_readings.Previous, shown.Value);
                _arrow.Glyph = MetricRules.ArrowGlyph(direction ?? TrendDirection.Flat);
                _arrow.Opacity = direction is null ? 0 : 1;
                _arrow.Foreground = Family.Brush(shown.Value.Available ? ColourRole.TextSecondary : ColourRole.TextDisabled);
                _row.Beside(0, _arrow, FamilyShape.Icon);
                break;

            case MetricTrend.Line:
                _row.Beside(0, null, 0);
                var band = Band();
                band.Over = MetricRules.LineOver(band.ActualHeight, FamilyType.Body);
                band.Role(shown.Value.Available ? ColourRole.Accent : ColourRole.TextDisabled);
                band.Load(MetricRules.Band(shown.Series));
                break;

            default:
                _row.Beside(0, null, 0);
                break;
        }
    }

    /// <summary>
    /// [S-14220] The line's band, built the first time a line is drawn: the meters' history band (owner, 2026-11-19: "they
    /// should reuse more of the same components") with no caption of its own (the row is what is written over it), behind
    /// the row and standing where it stands, on the card's top edge and down to its foot, side to side ("Plot edge to
    /// edge": the plot and its fill reach the card's edges, the labels stand inside it, over the plot). The row stands
    /// over the plot's top as its owner's part (<see cref="SparkBand.Over"/>): the line keeps to the room under the row's
    /// text at its foot, so it never runs through a glyph and is seen as this row's, not the one above.
    /// </summary>
    private SparkBand Band()
    {
        if (_band is null)
        {
            var band = new SparkBand(MetricRules.LinePoints) { Captions = false };
            ListEdge.Apply(band);
            band.SizeChanged += (_, args) => band.Over = MetricRules.LineOver(args.NewSize.Height, FamilyType.Body);
            Children.Insert(0, band);
            _band = band;
        }

        return _band;
    }
}
