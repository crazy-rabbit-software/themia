using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Windows.Foundation;

namespace Themia.App.Widgets;

/// <summary>
/// [S-14600]..[S-14603] [S-14700]..[S-14707] The one face of every single-metric widget (owner, 2026-11-20: "split all the
/// other widgets that are alike to the weather and air facts into individual metrics too. use the same base component for
/// all of them"): the kind's glyph, the metric's name, its value with its unit at the end and, as the widget's trend says,
/// an arrow beside the value or its graph at the card's foot. Its parts are the same at every size; how they stand is the
/// box's (owner: "the widgets like the PM2.5 one should be resizable in height and change its style so that it fits",
/// <see cref="MetricRules.Look"/>): one row tall the facts' row (<see cref="FactList"/>, the arrow in its inline slot); taller
/// the readout, the value as the reading and the name as its caption, one block scaled up together
/// (<see cref="ReadingFit"/>, beside or stacked as fits, <see cref="MetricRules.Arrange"/>); a graph (<see cref="Chart"/>,
/// in the chart look its page sets, [S-14703]) takes the foot and the reading the top. The whole metric is one item, the
/// card edge to edge, lit under the pointer where it opens something ([S-14705]); a click, Enter and the card menu's first
/// line open its kind's source ([S-14706], [S-14707]). Without a reading the face stays with "--" drawn unavailable and why
/// as its tooltip ([S-14602]); a reading kept through a failed refresh stays and the status's problem glyph says so. A
/// family subclasses it for its data and hands each read to <see cref="Present"/>; the face, the trend, the readings an
/// arrow compares and what a click opens are this base's alone.
/// </summary>
public abstract partial class MetricView : Grid, IWidgetView, IWidgetOpens, IWidgetContextActions, IWidgetSizeAware, IWidgetKeys, IWidgetCorners, IWidgetTopEdge
{
    private const string Scope = "metrics";

    private readonly IWidgetHost _host;
    private readonly MetricKind _kind;
    private readonly KitStatus _status;

    /// <summary>[S-14705] The metric's one item: the card from its top edge to its foot, side to side, the face inside it.</summary>
    private readonly Border _item = new();
    private readonly Face _face = new();
    private readonly ItemLight _light;

    /// <summary>[S-12811] One row tall, its row is the facts' row, the one component every fact row is (a list of one).</summary>
    private readonly FactList _row = new();

    // [S-14701] The readout: the caption (the glyph and the name) and the value with its arrow, one block scaled as one.
    private readonly ReadingFit _fit = new() { Spacing = 0 };
    private readonly StackPanel _caption = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly FontIcon _captionGlyph;
    private readonly TextBlock _name = Family.Text(FamilyType.Caption, ColourRole.TextSecondary);
    private readonly StackPanel _value = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly ReadoutBlock _reading = new();
    private readonly FontIcon _readoutArrow = Family.Glyph(Glyphs.TrendFlat, FamilyShape.Icon, ColourRole.TextSecondary);
    private readonly StackPanel _beside = new() { Orientation = Orientation.Horizontal, Spacing = FamilySpace.ColumnGap, HorizontalAlignment = HorizontalAlignment.Center };
    private MetricArrangement? _arrangement;

    private readonly MetricReadings _readings = new();
    private FontIcon? _rowArrow;
    private Chart? _graph;

    /// <summary>[S-14712] The graph as the row's sparkline beside the value, built the first time a card one row tall draws it.</summary>
    private Chart? _spark;
    private MetricShown? _shown;
    private MetricTrend _trend;
    private WidgetConfig? _config;
    private WidgetArea _area;

    // [S-12813] The status glyph's width over the card's top end corner while it shows (0: none).
    private double _statusRoom;

    /// <param name="host">The card's host: the status glyph's account link, the launcher a click opens through.</param>
    /// <param name="kind">The kind: its glyph, its history (which trends it can draw), its name and what it opens.</param>
    protected MetricView(IWidgetHost host, MetricKind kind)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _kind = kind ?? throw new ArgumentNullException(nameof(kind));
        _status = new KitStatus(host);

        // [S-14705] The item stands on the card's top edge as a list's first row does, and runs to its foot: the view owns
        // the edge ([S-5700]), so its box is the card's at every height, one cell too ([S-14712]).
        ItemMark.Mark(_item);
        // The item layer's light, clear at rest (ItemLight).
        _light = new ItemLight(_item);
        _item.Child = _face;
        _face.Room = width => _statusRoom = width;
        _row.Margin = default;
        _face.Children.Add(_row);

        // The row's 16 glyph: a glyph is read down to 12 and a Caption to 9, so the block scales down alike to 0.75 (WR-14).
        // In a box of its own its ink counts as the caption's (PartInk reads a bare glyph beside a text as inside the text's
        // line): standing higher than the name's letters, it is the block's first ink, on the room's top, never over it.
        _captionGlyph = Family.Glyph(kind.Glyph, FamilyShape.Icon, ColourRole.TextSecondary);
        _caption.Children.Add(new Border { Child = _captionGlyph, Margin = new Thickness(0, 0, FamilySpace.GlyphGap, 0), VerticalAlignment = VerticalAlignment.Center });
        _name.VerticalAlignment = VerticalAlignment.Center;
        _caption.Children.Add(_name);
        _reading.Use(FamilyType.Big28, HorizontalAlignment.Center);
        _readoutArrow.Margin = new Thickness(FamilySpace.GlyphGap, 0, 0, 0);
        _readoutArrow.VerticalAlignment = VerticalAlignment.Center;
        _value.Children.Add(_reading);
        _value.Children.Add(_readoutArrow);
        _fit.Visibility = Visibility.Collapsed;
        _face.Children.Add(_fit);

        Children.Add(_item);
        _status.Place(this);
    }

    /// <summary>
    /// [S-5700] [S-14712] The view owns the card's top edge: its one item is the card's box, its row and its graph stand on
    /// the top edge, so a card one cell tall (under the family's inset) is still the whole metric's box.
    /// </summary>
    public bool OwnsTopEdge => true;

    /// <summary>[S-14603] What history the kind's metric has.</summary>
    protected MetricHistory History => _kind.History;

    /// <summary>
    /// [S-14705] The metric's one item, the card edge to edge: what a family lights as a whole (a drive's card while its
    /// drive is pointed at elsewhere on the view, [S-11903]), so one fill lights it, never two over each other.
    /// </summary>
    protected FrameworkElement Item => _item;

    /// <summary>[S-14603] The trend drawn now (the config's, as the kind can draw it).</summary>
    protected MetricTrend Trend => _trend;

    /// <summary>The metric's name in the current language: the kind's row name unless the family says it otherwise.</summary>
    protected virtual string MetricName => _kind.Name.Text();

    /// <summary>[S-14706] What a click opens: the kind's source for the card's config; null for a kind with none or none now.</summary>
    public CardOpen? Opens => _config is { } config && _kind.Opens is { } open ? open.Target(config) : null;

    /// <summary>[S-14707] The card menu's own lines: the line that opens the source first, then the family's.</summary>
    public IReadOnlyList<WidgetAction> ContextActions
        => _kind.Opens is { } open && Opens is not null
            ? [new WidgetAction(Glyphs.Open, open.Label.Text(), () => Open()), .. OwnActions]
            : OwnActions;

    /// <summary>The family's own lines on the card menu (Refresh, where the source has one).</summary>
    protected virtual IReadOnlyList<WidgetAction> OwnActions => [];

    /// <summary>[S-14707] A click takes the keyboard only where Enter opens something.</summary>
    public bool WantsKeys => Opens is not null;

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
        Interactive(false);
        OnDetach();
    }

    /// <summary>[S-14700] The card's box: the face lays its parts in it.</summary>
    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Lay();
    }

    /// <summary>[S-11883] The hover fill reaches the card's edges: it takes the card's corners.</summary>
    public void ApplyCorners(CornerRadius corners) => _item.CornerRadius = corners;

    /// <summary>[S-14707] Enter opens the source while the card has the keyboard; nothing else is the metric's.</summary>
    public bool HandleKey(WidgetKey key) => key == WidgetKey.Enter && Open();

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

    /// <summary>[S-14601] Put one read on the face: its value, its trend and its state.</summary>
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

    /// <summary>Another language: the family writes its value again, the face its name.</summary>
    private void OnLanguage(Locale locale)
    {
        if (_config is { } config)
        {
            OnApply(config);
        }

        Draw();
    }

    /// <summary>[S-14703] The graph's chart look: the config's, as its kind offers it.</summary>
    private (ChartType Type, ChartDetail Detail) Looks()
        => _config is IChartLooks looks ? (ChartLooks.Effective(looks), ChartLooks.EffectiveDetail(looks)) : (ChartType.Area, ChartDetail.Simple);

    private void Draw()
    {
        var shown = _shown ?? new MetricShown(MetricValue.Unread);
        var value = shown.Value;
        var name = shown.Name ?? MetricName;
        _row.Show([new FactLine(_kind.Glyph, name, value.Text, value.Available)]);

        // [S-14701] The readout's parts are the row's: the name its caption, the value its reading (the unit smaller after it).
        _name.Text = name;
        var captionRole = value.Available ? ColourRole.TextSecondary : ColourRole.TextDisabled;
        _name.Foreground = Family.Brush(captionRole);
        _captionGlyph.Foreground = Family.Brush(captionRole);
        var split = ReadoutRules.Split(value.Text);
        _reading.Show(split.Value, split.Unit, valueRole: value.Available ? ColourRole.TextPrimary : ColourRole.TextDisabled);

        // [S-14602] A reading kept through a failed refresh stays, and the status's problem glyph says so, its room at the
        // row's end; "--" says there is no reading, why its tooltip (Lay's, with the face).
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
        _drawn = (shown, trend);
        DrawTrend(shown, trend);
        Interactive(Opens is not null);
        Lay();
    }

    // What the face draws now: the read and its trend (a Debug render's trend state included).
    private (MetricShown Shown, MetricTrend Trend) _drawn = (new MetricShown(MetricValue.Unread), MetricTrend.None);

    /// <summary>Debug renders: a trend state's arrow or line over the reading shown ([S-14605]); nothing in a release.</summary>
    partial void PreviewTrend(ref MetricShown shown, ref MetricTrend trend);

    /// <summary>
    /// [S-14603] The arrow beside the value against the reading before (as the source says, or against the last this widget
    /// saw), in the row's inline slot and beside the readout's value alike; with nothing to compare its room stays empty
    /// (never a face that shifts when one arrives, WR-12).
    /// </summary>
    private void DrawTrend(MetricShown shown, MetricTrend trend)
    {
        if (trend != MetricTrend.Arrow)
        {
            _row.Beside(0, null, 0);
            _readoutArrow.Visibility = Visibility.Collapsed;
            return;
        }

        _rowArrow ??= Family.Glyph(Glyphs.TrendFlat, FamilyShape.Icon, ColourRole.TextSecondary);
        var direction = shown.Direction ?? MetricRules.Direction(_readings.Previous, shown.Value);
        foreach (var arrow in new[] { _rowArrow, _readoutArrow })
        {
            arrow.Glyph = MetricRules.ArrowGlyph(direction ?? TrendDirection.Flat);
            arrow.Opacity = direction is null ? 0 : 1;
            arrow.Foreground = Family.Brush(shown.Value.Available ? ColourRole.TextSecondary : ColourRole.TextDisabled);
        }

        _readoutArrow.Visibility = Visibility.Visible;
        _row.Beside(0, _rowArrow, FamilyShape.Icon);
    }

    /// <summary>
    /// [S-14700]..[S-14702] Lay the face in the card's box by the one rule (<see cref="MetricRules.Look"/>): the row or the
    /// readout over the graph's share at the foot. The box arranges the parts; it never chooses them.
    /// </summary>
    private void Lay()
    {
        var (shown, trend) = _drawn;
        var (type, chosen) = Looks();

        // The item stands on the card's top edge: the view owns it, so the card's box is the view's.
        var height = _area.IsMeasured ? _area.Height : 0;
        var look = MetricRules.Look(height, trend, chosen, _area.IsMeasured ? _area.Grid() : ItemGrid.Default());
        var detail = look.Detail;
        var graphs = trend == MetricTrend.Line;
        var alone = look.Face == MetricFace.Graph;
        var slots = shown.Slots ?? MetricRules.LinePoints;
        var peak = _kind.Graph?.Peak ?? false;
        var role = shown.Value.Available ? ColourRole.Accent : ColourRole.TextDisabled;

        // [S-14602] "--" says there is no reading, why the face's tooltip (a status glyph would stand over the value); [S-14712]
        // the graph alone (one cell tall) says the reading in its tooltip, the name and the value it has no room to write.
        var value = shown.Value;
        ToolTipService.SetToolTip(_item, !value.Available && shown.Why is { Length: > 0 } why ? why
            : alone ? $"{shown.Name ?? MetricName}  {value.Text}" : null);

        // [S-14220] [S-14703] The graph: the card's whole box, its plot edge to edge, the reading the owner's part over its top;
        // [S-14712] one row tall it is the row's sparkline beside the value instead, one cell tall the graph alone.
        if (graphs && !look.Spark)
        {
            var graph = Graph();
            graph.Visibility = Visibility.Visible;
            graph.Role(role);
            var over = alone ? 0 : look.Graph > 0 ? look.Reading : height / 2;
            graph.Show(size => MetricRules.Graph(shown.Series, size.Width, type, detail, over, shown.Figure, slots, peak, shown.Added));
        }
        else if (_graph is not null)
        {
            _graph.Visibility = Visibility.Collapsed;
        }

        if (look.Spark)
        {
            _spark ??= new Chart { FlowDirection = FlowDirection.LeftToRight, Height = FamilyShape.Icon, VerticalAlignment = VerticalAlignment.Center };
            _spark.Role(role);
            _spark.Show(size => MetricRules.Graph(shown.Series, size.Width, type, ChartDetail.Simple, 0, null, slots, peak, shown.Added));
            _row.Beside(0, _spark, MetricRules.SparkWidth);
        }
        else if (graphs)
        {
            _row.Beside(0, null, 0);
        }

        var readout = look.Face == MetricFace.Readout;
        _row.Visibility = readout || alone ? Visibility.Collapsed : Visibility.Visible;
        _fit.Visibility = readout ? Visibility.Visible : Visibility.Collapsed;
        if (alone)
        {
            return;
        }

        if (!readout)
        {
            // One row: the card's, or the reading's share over the graph.
            // [S-14705] The one row is the whole face (one item, the card edge to edge), not a repeated item: it is the box.
            var under = look.Graph > 0;
            var face = under ? look.Reading : height;
            _row.VerticalAlignment = under ? VerticalAlignment.Top : VerticalAlignment.Stretch;
            _row.Height = under ? face : double.NaN;
            _row.Face(_area.IsMeasured && face > 0 ? face : double.NaN);
            return;
        }

        // [S-14701] The readout in the reading's share: the family's inset off the card's edges, a gap over the graph;
        // [S-12813] the status glyph's room at its end while one shows, so the block never stands under it.
        var foot = look.Graph > 0 ? look.Graph + FamilySpace.S2 : FamilySpace.Inset;
        var end = FamilySpace.Inset + _statusRoom;
        _fit.Margin = new Thickness(FamilySpace.Inset, FamilySpace.Inset, end, foot);
        var room = new Size(Math.Max(0, _area.Width - FamilySpace.Inset - end), Math.Max(0, look.Reading - FamilySpace.Inset - (look.Graph > 0 ? FamilySpace.S2 : FamilySpace.Inset)));
        Arrange(MetricRules.Arrange(room.Width, room.Height, Natural(_caption), Natural(_value), FamilySpace.ColumnGap));
    }

    private static SizeDip Natural(FrameworkElement part)
    {
        part.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return new SizeDip(part.DesiredSize.Width, part.DesiredSize.Height);
    }

    /// <summary>[S-14701] Stand the caption and the value in the block as <paramref name="arrangement"/> says.</summary>
    private void Arrange(MetricArrangement arrangement)
    {
        if (_arrangement == arrangement)
        {
            return;
        }

        _arrangement = arrangement;
        _fit.Parts.Clear();
        _beside.Children.Clear();
        if (arrangement == MetricArrangement.Beside)
        {
            _beside.Children.Add(_caption);
            _beside.Children.Add(_value);
            _fit.Parts.Add(_beside);
        }
        else
        {
            _fit.Parts.Add(_caption);
            _fit.Parts.Add(_value);
        }
    }

    /// <summary>
    /// [S-14220] [S-14703] The graph, built the first time one is drawn: the one chart ([S-6750]) behind the reading, the card's
    /// whole box from its top edge to its foot, side to side ("Plot edge to edge": the plot and its fill reach the card's
    /// edges, the labels stand inside it, over the plot). Time runs left to right in every language.
    /// </summary>
    private Chart Graph()
    {
        if (_graph is null)
        {
            _graph = new Chart { FlowDirection = FlowDirection.LeftToRight };
            _face.Children.Insert(0, _graph);
        }

        return _graph;
    }

    /// <summary>
    /// [S-14705] The item lit under the pointer and pressed (owner, 2026-11-20: "the metrics should also have a hover
    /// effect"): <see cref="ItemLight"/>. A metric that opens nothing lights nothing.
    /// </summary>
    private void Interactive(bool on) => _light.Set(on);

    /// <summary>
    /// [S-12813] The face: the status glyph over the card's top end corner tells it its width (<see cref="IStatusRoom"/>), as it
    /// tells the row in it, so the readout leaves it room too.
    /// </summary>
    private sealed partial class Face : Grid, IStatusRoom
    {
        public Action<double>? Room { get; set; }

        public void StatusRoom(double width) => Room?.Invoke(width);
    }

    /// <summary>[S-14706] [S-14707] Open the source through the host's launcher; said in the log where nothing opened.</summary>
    private bool Open()
    {
        if (Opens is not { } target)
        {
            return false;
        }

        var outcome = _host.Launcher.OpenCard(target);
        if (outcome != LaunchOutcome.Opened)
        {
            Log.Warn(Scope, $"{_kind.Id}: opening its source opened nothing ({outcome})");
        }

        return true;
    }
}
