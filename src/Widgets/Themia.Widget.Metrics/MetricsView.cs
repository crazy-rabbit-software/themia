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

    /// <summary>Measures, never drawn: the name at the head's size.</summary>
    private readonly TextBlock _nameProbe = Family.Text(MetricFaceRules.Name);

    /// <summary>
    /// [S-16324] The head's reading in its held box: the reading, right-aligned, over the metric's widest reading drawn unseen
    /// (as wide as the box), the box scaled as one by its share of the Title line (<see cref="MetricFaceRules.ReadingShare"/>).
    /// </summary>
    private readonly Viewbox _readingBox = new() { Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top };
    private readonly Grid _readingCell = new();
    private readonly TextBlock _readingHeld = Family.Text(MetricFaceRules.Head);

    // ---- the value style's block: glyph and name over the value
    private readonly StackPanel _block = new() { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false };
    private readonly FontIcon _blockGlyph = Family.Glyph(Glyphs.Cpu);
    private readonly TextBlock _blockName = Family.Text(FamilyType.Body, ColourRole.TextSecondary);
    private readonly TextBlock _blockValue = Family.Text(FamilyType.Big28);

    /// <summary>[S-16324] The reading's held box: the reading over its template (the metric's widest reading, unseen), as wide as the wider.</summary>
    private readonly Grid _valueCell = new() { HorizontalAlignment = HorizontalAlignment.Left };
    private readonly TextBlock _blockHeld = Family.Text(FamilyType.Big28);
    private readonly StackPanel _caption = new();

    /// <summary>[S-16327] The value face's own problem mark and word, on its caption line: in the block, never over it.</summary>
    private readonly FontIcon _blockProblem = Family.Glyph(Glyphs.Warning, FamilyShape.Icon, ColourRole.Caution);
    private readonly TextBlock _blockWhy = Family.Text(FamilyType.Body, ColourRole.TextSecondary);
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

        // The value style: the caption row over the reading.
        _caption.Orientation = Orientation.Horizontal;
        _caption.Spacing = FamilySpace.GlyphGap;
        var caption = _caption;
        _blockGlyph.VerticalAlignment = VerticalAlignment.Center;
        _blockName.TextWrapping = TextWrapping.NoWrap;
        caption.Children.Add(_blockGlyph);
        caption.Children.Add(_blockName);
        _blockProblem.VerticalAlignment = VerticalAlignment.Center;
        _blockWhy.VerticalAlignment = VerticalAlignment.Center;
        _blockWhy.TextWrapping = TextWrapping.NoWrap;
        caption.Children.Add(_blockProblem);
        caption.Children.Add(_blockWhy);
        _blockValue.TextWrapping = TextWrapping.NoWrap;
        Family.Figures(_blockValue);

        // Both on the block's start edge: the reading under the caption's glyph, never centred in the block's width.
        caption.HorizontalAlignment = HorizontalAlignment.Left;
        _blockValue.HorizontalAlignment = HorizontalAlignment.Left;

        // [S-16324] The reading stands in its template's box, the metric's widest reading drawn unseen under it: the block is
        // laid out by that box, and the checks judge it by it (WR-15: a running value never moves or rescales the block).
        _blockHeld.Opacity = 0;
        _blockHeld.IsHitTestVisible = false;
        _blockHeld.TextWrapping = TextWrapping.NoWrap;
        Family.Figures(_blockHeld);
        _valueCell.Children.Add(_blockHeld);
        _valueCell.Children.Add(_blockValue);
        HoldMark.Mark(_valueCell);
        _block.Children.Add(caption);
        _block.Children.Add(_valueCell);
        _block.RenderTransform = _blockScale;

        // Measured unrounded: a box rounded up to the next device pixel is a fraction of a DIP the block's scale makes many.
        foreach (var part in new FrameworkElement[] { _block, _caption, _blockGlyph, _blockName, _blockValue, _valueCell, _blockHeld, _blockProblem, _blockWhy })
        {
            part.UseLayoutRounding = false;
        }

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
        _shown = null;
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
        var folded = MetricGraphRules.Fold(series, MetricGraphRules.MostSlots(type, series, _width), _config.Feed.Fold);
        int? hovered = _pointer is { } x && MetricGraphRules.SlotAt(type, x, folded.Slots.Count, _width) is { } slot
            ? MetricGraphRules.Nearest(folded.Slots, slot)
            : null;

        // [S-16327] Why there is no reading, in the status's compact line beside its glyph (whole, or the glyph alone).
        var inset = FamilySpace.Inset;
        _status.Say(unavailable ? _why : null, (_width - (2 * inset)) / 3);

        // [S-16321] Its glyph read to 12 DIP however far the head scales down.
        _headScale.ScaleX = face.Scale;
        _headScale.ScaleY = face.Scale;
        var glyph = MetricFaceRules.GlyphSize(face.Scale);
        _glyph.FontSize = glyph;
        _glyph.Width = glyph;
        _glyph.Height = glyph;

        // [S-16326] The status's problem glyph stands at the head's end, on its line; the reading keeps clear of it.
        var room = _status.Room > 0 ? _status.Room + FamilySpace.GlyphGap : 0;
        _head.Width = Math.Max(0, (_width - (2 * inset) - room) / face.Scale);

        // [S-16321] [S-16324] The name keeps its room first: the reading's held box (the metric's widest reading) at the share of
        // its Title line that leaves the name whole, the share following the head's width smoothly; under the pointer the name's
        // place says the time and the box the value there (nothing changes size, WR-12).
        _glyph.Glyph = def.Glyph;
        var name = _config.Caption();
        _nameProbe.Text = name;
        _nameProbe.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
        _readingHeld.Text = MetricFormat.Widest(locale, def, _config.Feed.Shape, _currency);
        _readingCell.Width = double.NaN;
        _readingCell.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
        var held = _readingHeld.DesiredSize.Width;
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

        _head.Margin = new Thickness(inset, face.HeadTop, 0, 0);
        _status.StandAt(face.HeadTop + (((face.HeadHeight) - KitStatus.Side) / 2), null);

        _plot.Width = _width;
        _plot.Height = _height;
        _plot.Draw(new PlotLook(folded, type, def.FromZero, def.Unit == MetricUnit.Share, face.PlotTop, face.Behind, hovered), _width, _height);
    }

    /// <summary>
    /// [S-16323] [S-16324] The value style: the caption over the reading, scaled as one block to fill the card. The block is laid
    /// out by the reading's held box (its template, the metric's widest reading), so whatever it shows ("31%", "100%", "--")
    /// stands in one place at one scale; its glyph is held at 12 DIP however far the block scales down (WR-14).
    /// </summary>
    private void DrawValue(MetricDef def, string reading, bool unavailable)
    {
        _blockGlyph.Glyph = def.Glyph;
        _blockName.Text = _config.Caption();
        _blockValue.Foreground = Family.Brush(unavailable ? ColourRole.TextDisabled : ColourRole.TextPrimary);
        _blockValue.Text = reading;
        _blockHeld.Text = MetricFormat.Widest(Strings.Current, def, _config.Feed.Shape, _currency);

        // [S-16327] In the value face the status's mark stands on the caption line, in the block (counted in its width, never
        // over it): the problem's glyph, its tooltip every problem, and without a reading the word that says why.
        _status.Visibility = Visibility.Collapsed;
        var problem = _problems.Count > 0;
        _blockProblem.Visibility = problem ? Visibility.Visible : Visibility.Collapsed;
        ToolTipService.SetToolTip(_blockProblem, problem ? string.Join("\n", _problems) : null);
        _blockWhy.Text = unavailable && problem ? _why ?? string.Empty : string.Empty;
        _blockWhy.Visibility = _blockWhy.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

        // Its own size alone (a width or margin set by the pass before would stand in the measure), again with its glyph held
        // where the scale it comes to would draw it under its floor.
        var glyph = FamilyShape.Icon;
        var face = default(ValueFace);
        var box = default(PartBox);
        for (var pass = 0; pass < 3; pass++)
        {
            foreach (var icon in new[] { _blockGlyph, _blockProblem })
            {
                icon.FontSize = glyph;
                icon.Width = glyph;
                icon.Height = glyph;
            }

            _block.Width = double.NaN;
            _block.Margin = default;
            _block.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
            box = Ink.Of(_block);

            // Across: from the block's start (the held box's) to the further of the held box's end and the caption's last ink.
            var last = _blockWhy.Visibility == Visibility.Visible ? _blockWhy : _blockProblem.Visibility == Visibility.Visible ? _blockProblem : (FrameworkElement)_blockName;
            var end = Math.Max(_valueCell.DesiredSize.Width, _caption.DesiredSize.Width - Ink.Of(last).Right);
            face = MetricFaceRules.Value(_width, _height, box.Width, box.Height, box.Ink, 0, Math.Max(0, box.Width - end));
            var held = MetricFaceRules.GlyphSize(face.Scale);
            if (Math.Abs(held - glyph) < 0.01)
            {
                break;
            }

            glyph = held;
        }

        _blockScale.ScaleX = face.Scale;
        _blockScale.ScaleY = face.Scale;
        _block.Width = box.Width;
        _block.Margin = new Thickness(face.Left, face.Top, 0, 0);


    }
}
