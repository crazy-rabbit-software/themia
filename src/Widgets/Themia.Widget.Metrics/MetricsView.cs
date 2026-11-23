using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.App.Widgets;
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
/// graph puts the value it is on (and its time) where the reading stands, and marks it. In the value style the glyph and the
/// name over the value, one block scaled to fill (<see cref="MetricFaceRules.Value"/>). Its data comes from the host's
/// answers through <see cref="MetricsView"/>'s feeds, only the one its metric reads.
/// </summary>
internal sealed partial class MetricsView : Grid, IWidgetView, IWidgetSizeAware, IWidgetTopEdge, IWidgetContextActions
{
    private readonly IWidgetHost _host;
    private readonly KitStatus _status;
    private readonly MetricsPlot _plot = new();

    // ---- the graph style's head: glyph, name, reading
    private readonly Grid _head = new() { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false };
    private readonly FontIcon _glyph = Family.Glyph(Glyphs.Cpu);
    private readonly TextBlock _name = Family.Text(MetricFaceRules.Name, ColourRole.TextSecondary);
    private readonly TextBlock _value = Family.Text(MetricFaceRules.Head);
    private readonly ScaleTransform _headScale = new();

    // ---- the value style's block: glyph and name over the value
    private readonly StackPanel _block = new() { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false };
    private readonly FontIcon _blockGlyph = Family.Glyph(Glyphs.Cpu);
    private readonly TextBlock _blockName = Family.Text(FamilyType.Body, ColourRole.TextSecondary);
    private readonly TextBlock _blockValue = Family.Text(FamilyType.Big28);
    private readonly ScaleTransform _blockScale = new();

    private MetricsConfig _config = new();
    private MetricSeries? _series;
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
        _value.TextWrapping = TextWrapping.NoWrap;
        Family.Figures(_value);
        SetColumn(_name, 1);
        SetColumn(_value, 2);
        _head.Children.Add(_glyph);
        _head.Children.Add(_name);
        _head.Children.Add(_value);

        // The value style: the caption row over the reading.
        var caption = new StackPanel { Orientation = Orientation.Horizontal, Spacing = FamilySpace.GlyphGap };
        _blockGlyph.VerticalAlignment = VerticalAlignment.Center;
        _blockName.TextWrapping = TextWrapping.NoWrap;
        caption.Children.Add(_blockGlyph);
        caption.Children.Add(_blockName);
        _blockValue.TextWrapping = TextWrapping.NoWrap;
        Family.Figures(_blockValue);
        _block.Children.Add(caption);
        _block.Children.Add(_blockValue);
        _block.RenderTransform = _blockScale;

        // [S-16326] The graph first, the card's whole box; the head over it; the status's mark last.
        Children.Add(_plot);
        Children.Add(_head);
        Children.Add(_block);
        _status.Place(this);

        _plot.PointerMoved += OnPointerMoved;
        _plot.PointerExited += OnPointerExited;
        _plot.PointerCanceled += OnPointerExited;
        PointerPreview.Register(_plot, (point, _) => Point(point.X));
        SizeChanged += (_, e) => Resize(e.NewSize.Width, e.NewSize.Height);
    }

    /// <summary>[S-6754] The graph is drawn from the card's top edge (behind a low card's head); the head keeps its inset itself.</summary>
    public bool OwnsTopEdge => true;

    /// <summary>[S-730] Refresh, where the metric's source has one.</summary>
    public IReadOnlyList<WidgetAction> ContextActions
        => Refresher() is { } refresh ? [new WidgetAction(Glyphs.Refresh, refresh.Label, refresh.Run)] : [];

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _detached = false;
        _ticker = ticker;
        Strings.Changed -= OnLanguage;
        Strings.Changed += OnLanguage;
        ApplyConfig(config);
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as MetricsConfig ?? new MetricsConfig();
        _pointer = null;
        Follow();
        Render();
    }

    public void Detach()
    {
        _detached = true;
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

        (_width, _height) = (width, height);
        Draw();
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
        _head.Visibility = graph ? Visibility.Visible : Visibility.Collapsed;
        _plot.Visibility = graph ? Visibility.Visible : Visibility.Collapsed;
        _block.Visibility = graph ? Visibility.Collapsed : Visibility.Visible;
        var reading = MetricFormat.Reading(locale, def, series.Now, _currency);
        var unavailable = series.Now is null;

        if (!graph)
        {
            DrawValue(def, reading, unavailable);
            return;
        }

        var face = MetricFaceRules.Graph(_width, _height);
        var type = _config.GraphInForce;
        var folded = MetricGraphRules.Fold(series, MetricGraphRules.MaxSlots(type, _width), _config.Feed.Fold);
        int? hovered = _pointer is { } x && MetricGraphRules.SlotAt(type, x, folded.Slots.Count, _width) is { } slot
            ? MetricGraphRules.Nearest(folded.Slots, slot)
            : null;

        // The head: its glyph, its name, the reading (or the value under the pointer and its time).
        _glyph.Glyph = def.Glyph;
        _name.Text = def.Name.Text();
        _value.Inlines.Clear();
        if (hovered is { } at && folded.Slots[at] is { } shown)
        {
            _value.Inlines.Add(Family.Run(MetricFaceRules.Name, ColourRole.TextSecondary, MetricFormat.When(locale, folded, at, _pinnedNow ?? DateTimeOffset.Now) + "  "));
            _value.Inlines.Add(Family.Run(MetricFaceRules.Head, ColourRole.TextPrimary, MetricFormat.Reading(locale, def, shown, _currency)));
        }
        else
        {
            _value.Inlines.Add(Family.Run(MetricFaceRules.Head, unavailable ? ColourRole.TextDisabled : ColourRole.TextPrimary, reading));
        }

        var inset = FamilySpace.Inset;
        _headScale.ScaleX = face.Scale;
        _headScale.ScaleY = face.Scale;
        _head.Width = Math.Max(0, (_width - (2 * inset)) / face.Scale);
        _head.Margin = new Thickness(inset, face.HeadTop, 0, 0);

        _plot.Width = _width;
        _plot.Height = _height;
        _plot.Draw(new PlotLook(folded, type, def.FromZero, def.Unit == MetricUnit.Share, face.PlotTop, face.Behind, hovered), _width, _height);
    }

    /// <summary>[S-16323] The value style: the caption over the reading, scaled as one block to fill the card.</summary>
    private void DrawValue(MetricDef def, string reading, bool unavailable)
    {
        _blockGlyph.Glyph = def.Glyph;
        _blockName.Text = def.Name.Text();
        _blockValue.Foreground = Family.Brush(unavailable ? ColourRole.TextDisabled : ColourRole.TextPrimary);

        // Measured with its template, so a running reading keeps its block's size (WR-15).
        _blockValue.Text = MetricFaceRules.Template(reading);
        _block.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = _block.DesiredSize;
        _blockValue.Text = reading;

        var caption = LineInk.Of(FamilyType.Body).With(FamilyShape.Icon, FamilyType.Body.LineHeight);
        var ink = new LineInk(caption.Over, LineInk.Of(FamilyType.Big28).Under);
        var face = MetricFaceRules.Value(_width, _height, size.Width, size.Height, ink);
        _blockScale.ScaleX = face.Scale;
        _blockScale.ScaleY = face.Scale;
        _block.Width = size.Width;
        _block.Margin = new Thickness(face.Left, face.Top, 0, 0);
    }
}
