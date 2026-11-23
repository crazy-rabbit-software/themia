using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;

namespace Themia.Widget.Metrics;

/// <summary>
/// [S-16320]..[S-16326] The Metric widget's face, written for it alone: in the graph style the glyph and the name top left and
/// the reading top right, one head scaled with the card (<see cref="MetricFaceRules.Graph"/>), and under it the graph to the
/// card's sides and foot (<see cref="MetricsPlot"/>), behind the head where the card is too low for both; the pointer over the
/// graph puts the value it is on (and its time) where the reading stands, and marks it. In the value style the same head alone,
/// the glyph and the name at the left and the reading at the right, one row centred down the card (<see cref="MetricFaceRules.Row"/>).
/// Its data comes from the host's answers through <see cref="MetricsView"/>'s feeds, only the one its metric reads.
/// </summary>
internal sealed partial class MetricsView : Grid, IWidgetView, IWidgetSizeAware, IWidgetTopEdge, IWidgetContextActions, IWidgetOpens,
    IWidgetSurfaceAware, IWidgetKeys
{
    private readonly IWidgetHost _host;

    /// <summary>[S-16329] The card lit as the one item a click opens: the item layer's fill over its whole box (WR-13).</summary>
    private readonly OpensLight _light;
    private IWidgetSurface? _surface;
    private readonly KitStatus _status;
    private readonly MetricsPlot _plot = new();

    // ---- the graph style's head: glyph, name, reading
    private readonly Grid _head = new() { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false };
    private readonly FontIcon _glyph = Family.Glyph(Glyphs.Cpu);
    private readonly TextBlock _name = Family.Text(MetricFaceRules.Name, ColourRole.TextSecondary);
    private readonly TextBlock _value = Family.Text(MetricFaceRules.Head);
    private readonly ScaleTransform _headScale = new();

    /// <summary>Measures, never drawn: the name at the head's size.</summary>
    private readonly TextBlock _nameProbe = Family.Text(MetricFaceRules.Name);

    /// <summary>
    /// [S-16324] The head's reading in its held box: the reading, right-aligned, over the metric's widest reading drawn unseen
    /// (as wide as the box), the box scaled as one by its share of the Title line (<see cref="MetricFaceRules.ReadingShare"/>).
    /// </summary>
    private readonly Viewbox _readingBox = new() { Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top };
    private readonly Grid _readingCell = new();
    private readonly TextBlock _readingHeld = Family.Text(MetricFaceRules.Head);

    private MetricsConfig _config = new();
    private MetricSeries? _series;

    /// <summary>[S-16902] The graph's range as drawn last: kept while the readings fit it, so time passing never rescales the graph.</summary>
    private GraphRange? _range;
    private string _currency = string.Empty;
    private double _width;
    private double _height;
    private double? _pointer;
    private bool _detached = true;

    /// <summary>[S-11850] A sample's fixed moment; null on the desktop.</summary>
    private DateTimeOffset? _pinnedNow;

    public MetricsView(IWidgetHost host)
    {
        _host = host;
        _status = new KitStatus(host);
        _light = new OpensLight(this, LightsUp);
        Feeds(host);

        // The head: the glyph, the name (cut by its ellipsis), the reading at the end, all on the head's baseline.
        _head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _head.Height = MetricFaceRules.Head.LineHeight;
        _head.RenderTransform = _headScale;
        _glyph.VerticalAlignment = VerticalAlignment.Center;
        _glyph.Margin = new Thickness(0, 0, FamilySpace.GlyphGap, 0);
        _name.VerticalAlignment = VerticalAlignment.Top;
        _name.Margin = new Thickness(0, MetricFaceRules.NameDrop, FamilySpace.ColumnGap, 0);
        _name.TextWrapping = TextWrapping.NoWrap;
        _name.TextTrimming = TextTrimming.CharacterEllipsis;
        _value.VerticalAlignment = VerticalAlignment.Top;
        _value.HorizontalAlignment = HorizontalAlignment.Right;
        _value.TextAlignment = TextAlignment.Right;
        _value.TextWrapping = TextWrapping.NoWrap;
        _value.TextTrimming = TextTrimming.CharacterEllipsis;
        Family.Figures(_value);
        _readingHeld.Opacity = 0;
        _readingHeld.TextWrapping = TextWrapping.NoWrap;
        _readingHeld.IsHitTestVisible = false;
        Family.Figures(_readingHeld);
        _readingCell.Children.Add(_readingHeld);
        _readingCell.Children.Add(_value);
        _readingCell.Height = MetricFaceRules.Head.LineHeight;
        HoldMark.Mark(_readingCell);
        _readingBox.Child = _readingCell;
        SetColumn(_name, 1);
        SetColumn(_readingBox, 2);
        _head.Children.Add(_glyph);
        _head.Children.Add(_name);
        _head.Children.Add(_readingBox);

        // [S-16326] The graph first, the card's whole box; the head over it; the status's mark last.
        Children.Add(_plot);
        Children.Add(_head);
        _status.Place(this);

        _plot.PointerMoved += OnPointerMoved;
        _plot.PointerExited += OnPointerExited;
        _plot.PointerCanceled += OnPointerExited;
        // [S-16341] [S-16329] Under the pointer the graph's value there and, where a click opens something, the card's light:
        // the plot is over the card, so its look at a point is the one the renders show.
        PointerPreview.Register(_plot, (point, pressed) =>
        {
            Point(point.X);
            if (LightsUp())
            {
                LinkLook.Opens(this, pressed ? ItemState.Pressed : ItemState.Hover);
            }
        });
        SizeChanged += (_, e) => Resize(e.NewSize.Width, e.NewSize.Height);
    }

    /// <summary>[S-6754] The graph is drawn from the card's top edge (behind a low card's head); the head keeps its inset itself.</summary>
    public bool OwnsTopEdge => true;

    /// <summary>[S-730] [S-16329] The line that opens the metric's source first, where it has one; then Refresh, where its source has one.</summary>
    public IReadOnlyList<WidgetAction> ContextActions
    {
        get
        {
            var lines = new List<WidgetAction>();
            if (_config.Opener is { } opener && Opens is not null)
            {
                lines.Add(new WidgetAction(Glyphs.Open, opener.Label.Text(), () => Open()));
            }

            if (Refresher() is { } refresh)
            {
                lines.Add(new WidgetAction(Glyphs.Refresh, refresh.Label, refresh.Run));
            }

            return lines;
        }
    }

    /// <summary>[S-16329] What a click on the card opens now (<see cref="MetricsConfig.Opens"/>).</summary>
    public CardOpen? Opens => _config.Opens();

    /// <summary>[S-16329] A click takes the keyboard only where Enter opens something.</summary>
    public bool WantsKeys => Opens is not null;

    /// <summary>[S-16329] Enter opens the source while the card has the keyboard.</summary>
    public bool HandleKey(WidgetKey key) => key == WidgetKey.Enter && Open();

    public void Bind(IWidgetSurface surface) => _surface = surface;

    /// <summary>[S-16329] Whether the card lights under the pointer: it opens something now, out of edit mode.</summary>
    private bool LightsUp() => !_detached && _surface?.EditMode != true && Opens is not null;

    /// <summary>[S-16329] Open the source through the host's launcher; said in the log where nothing opened.</summary>
    private bool Open()
    {
        if (Opens is not { } target)
        {
            return false;
        }

        var outcome = _host.Launcher.OpenCard(target);
        if (outcome != LaunchOutcome.Opened)
        {
            Log.Warn("metrics", $"{_config.Metric}: opening its source opened nothing ({outcome})");
        }

        return true;
    }

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _detached = false;
        _ticker = ticker;
        _light.Attach();
        Strings.Changed -= OnLanguage;
        Strings.Changed += OnLanguage;
        ApplyConfig(config);
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as MetricsConfig ?? new MetricsConfig();
        _pointer = null;
        _shown = null;
        _range = null;
        Follow();
        Render();
    }

    public void Detach()
    {
        _detached = true;
        _light.Detach();
        Strings.Changed -= OnLanguage;
        Unfollow();
        _status.Detach();
    }

    public void ApplySize(WidgetArea area)
    {
        if (area.IsMeasured)
        {
            Resize(area.Width, area.Height);
        }
    }

    private void OnLanguage(Locale locale) => Render();

    private void Resize(double width, double height)
    {
        if (Math.Abs(width - _width) < 0.01 && Math.Abs(height - _height) < 0.01)
        {
            return;
        }

        // [S-16901] Read again: a new width folds into runs of another size, its oldest run taking other slots before the span.
        (_width, _height) = (width, height);
        Render();
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e) => Point(e.GetCurrentPoint(_plot).Position.X);

    private void OnPointerExited(object sender, PointerRoutedEventArgs e) => Point(null);

    /// <summary>[S-16326] The pointer over the graph at <paramref name="x"/> (null: off it): the head says the value there.</summary>
    private void Point(double? x)
    {
        if (_pointer == x)
        {
            return;
        }

        _pointer = x;
        Draw();
    }

    /// <summary>Read the metric's data as its series and draw it.</summary>
    private void Render()
    {
        _series = Read(_pinnedNow ?? DateTimeOffset.Now);
        Draw();
    }

    /// <summary>[S-16320] Lay the face out at the card's size and draw what it holds.</summary>
    private void Draw()
    {
        if (_series is not { } series || !(_width > 0) || !(_height > 0))
        {
            return;
        }

        var def = _config.Def;
        var locale = Strings.Current;
        var graph = _config.Style == MetricsStyle.Graph;
        _plot.Visibility = graph ? Visibility.Visible : Visibility.Collapsed;
        var reading = MetricFormat.Reading(locale, def, series.Now, _currency);
        var unavailable = series.Now is null;

        // [S-16327] Why there is no reading, in the status's compact line beside its glyph (whole, or the glyph alone).
        var inset = FamilySpace.Inset;
        _status.Say(unavailable ? _why : null, (_width - (2 * inset)) / 3);

        // [S-16326] The status's problem glyph stands at the head's end, on its line; the reading keeps clear of it.
        var room = _status.Room > 0 ? _status.Room + FamilySpace.GlyphGap : 0;

        // The name and the reading's held box (the metric's widest reading) at the head's own size.
        _glyph.Glyph = def.Glyph;
        var name = _config.Caption();
        _nameProbe.Text = name;
        _nameProbe.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
        _readingHeld.Text = MetricFormat.Widest(locale, def, _config.Feed.Shape, _currency);
        _readingCell.Width = double.NaN;
        _readingCell.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
        var held = _readingHeld.DesiredSize.Width;

        // [S-16321] The graph face's head over its plot; [S-16903] the value face's the head alone, one row centred down the card.
        var face = MetricFaceRules.Graph(_width, _height);
        var row = MetricFaceRules.Row(_width, _height, MetricFaceRules.RowWidth(_nameProbe.DesiredSize.Width, held), room);
        var (scale, headTop) = graph ? (face.Scale, face.HeadTop) : (row.Scale, row.Top);
        var type = _config.GraphInForce;
        var folded = MetricGraphRules.Fold(series, MetricGraphRules.MostSlots(type, series, _width), _config.Feed.Fold);
        int? hovered = graph && _pointer is { } x && MetricGraphRules.SlotAt(type, x, folded.Slots.Count, _width) is { } slot
            ? MetricGraphRules.Nearest(folded.Slots, slot)
            : null;

        // [S-16321] Its glyph read to 12 DIP however far the head scales down.
        _headScale.ScaleX = scale;
        _headScale.ScaleY = scale;
        var glyph = MetricFaceRules.GlyphSize(scale);
        _glyph.FontSize = glyph;
        _glyph.Width = glyph;
        _glyph.Height = glyph;
        _head.Width = Math.Max(0, (_width - (2 * inset) - room) / scale);

        // [S-16321] [S-16324] The name keeps its room first: the reading's held box at the share of its Title line that leaves
        // the name whole, the share following the head's width smoothly; under the pointer the name's place says the time and the
        // box the value there (nothing changes size, WR-12).
        var share = MetricFaceRules.ReadingShare(_head.Width - glyph - FamilySpace.GlyphGap - FamilySpace.ColumnGap, _nameProbe.DesiredSize.Width, held);
        _readingCell.Width = held;
        _readingBox.Width = held * share;
        _readingBox.Height = MetricFaceRules.Head.LineHeight * share;
        _readingBox.Margin = new Thickness(0, MetricFaceRules.Head.LineHeight * LineInk.FaceBaseline * (1 - share), 0, 0);

        _value.Inlines.Clear();
        if (hovered is { } at && folded.Slots[at] is { } shown)
        {
            _name.Text = MetricFormat.When(locale, folded, at, _pinnedNow ?? DateTimeOffset.Now);
            _value.Inlines.Add(Family.Run(MetricFaceRules.Head, ColourRole.TextPrimary, MetricFormat.Reading(locale, def, shown, _currency)));
        }
        else
        {
            _name.Text = name;
            _value.Inlines.Add(Family.Run(MetricFaceRules.Head, unavailable ? ColourRole.TextDisabled : ColourRole.TextPrimary, reading));
        }

        _head.Margin = new Thickness(inset, headTop, 0, 0);
        _status.StandAt(headTop + (((MetricFaceRules.Head.LineHeight * scale) - KitStatus.Side) / 2), null);
        if (!graph)
        {
            return;
        }

        _plot.Width = _width;
        _plot.Height = _height;
        _range = MetricGraphRules.Steady(_range, folded, def.FromZero, def.Unit == MetricUnit.Share);
        _plot.Draw(new PlotLook(folded, type, _range.Value, def.Unit == MetricUnit.Share, face.PlotTop, face.Behind, hovered), _width, _height);
    }
}
