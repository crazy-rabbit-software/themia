using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Settings;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using P = Themia.Sdk.Kit.ShellMetrics.Page;

namespace Themia.App.Shell;

/// <summary>
/// [S-2311] The Appearance page's live preview, after Karpo's (karpo-app, Karpo.Dock/LookPreview.cs, docs/widgets.md
/// §592, §613, §671): a stretch of this display's desktop at the page's top, bare — its wallpaper, and on it a small
/// desktop of real widget cards laid as a bento (<see cref="AppearanceBento"/>: a clock, a month, the weather station
/// joined, a meter, a toggle, the tasks, a graph, the music...) on their samples ([S-11850]), drawn by the card itself
/// in the design as it stands, so a style, a colour or a radius picked shows at once. The cards stand on the display's
/// grid where the desktop's would, in its middle (their frost is the display's own blurred wallpaper, [S-2114]), and
/// touching ones join as on the desktop ([S-2240]); the stretch is scaled to the page's width, its height following.
/// Built once while the page stands; each change of the state paints the cards again (<see cref="Refresh"/>), and
/// only a design replaced whole (an undo, an import) builds them again. [S-2364] On the desktop it is kept from one
/// opening of the page to the next while edit mode lasts (<see cref="Kept"/>), its cards not ticking while it is not shown.
/// </summary>
internal sealed partial class AppearancePreview : Grid, IPagePreview
{
    private const string Scope = "appearance-preview";

    private readonly Backdrop? _backdrop;
    private readonly ScreenMetrics _metrics;
    private readonly Func<DesignState> _design;
    private readonly Canvas _scene = new() { IsHitTestVisible = false };
    private readonly Image _ground = new() { Stretch = Stretch.Fill, IsHitTestVisible = false };
    private readonly List<WidgetCard> _cards = [];
    private readonly Services.Ticker _ticker;
    private readonly SampleWorld _samples;
    private DesignState? _built;
    private bool _disposed;

    /// <param name="backdrop">The display's blurred wallpaper, or null for none (the ground is the cards' secondary fill).</param>
    /// <param name="metrics">The display's size in DIPs: the cards are sized on its grid and their frost placed on it.</param>
    /// <param name="design">The design as it stands now.</param>
    public AppearancePreview(Backdrop? backdrop, ScreenMetrics metrics, Func<DesignState> design)
    {
        _backdrop = backdrop;
        _metrics = metrics;
        _design = design ?? throw new ArgumentNullException(nameof(design));
        var dispatcher = DispatcherQueue.GetForCurrentThread() ?? throw new InvalidOperationException("the preview is built on the UI thread");
        _ticker = new Services.Ticker(dispatcher);
        _samples = new SampleWorld(_ticker, dispatcher);

        CornerRadius = new CornerRadius(P.CardRadius);
        BorderBrush = ShellBrush.Of(ShellRole.CardStroke);
        BorderThickness = new Thickness(FamilyShape.Hairline);
        Background = ShellBrush.Of(ShellRole.CardSecondary);
        Children.Add(_scene);
        _scene.Children.Add(_ground);
        if (_backdrop is not null)
        {
            _backdrop.Changed += OnBackdrop;
        }

        SizeChanged += (_, _) => Fit();
        Build();
    }

    /// <summary>[S-2364] The display's size it was built for: a preview kept for another is built again.</summary>
    internal ScreenMetrics Metrics => _metrics;

    /// <summary>[S-2364] Shown on a page, or put away until the next: its cards tick only while shown.</summary>
    internal void Shown(bool shown)
    {
        if (!_disposed)
        {
            _ticker.SetPaused(!shown);
        }
    }

    public void Refresh()
    {
        if (_disposed)
        {
            return;
        }

        var design = _design();
        if (!ReferenceEquals(design, _built))
        {
            Build();
            return;
        }

        Lay();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_backdrop is not null)
        {
            _backdrop.Changed -= OnBackdrop;
        }

        Clear();
        _samples.Dispose();
        _ticker.Dispose();
    }

    private void OnBackdrop()
    {
        _ground.Source = _backdrop?.Picture;
        foreach (var card in _cards)
        {
            card.ApplyDesign();
        }
    }

    /// <summary>
    /// The cards made again over the design as it stands (one change of it replaced it whole): one per bento tile,
    /// on its box on the display's grid, the bento in the display's middle.
    /// </summary>
    private void Build()
    {
        Clear();
        var design = _design();
        _built = design;
        _ground.Source = _backdrop?.Picture;
        _ground.Width = _metrics.Width;
        _ground.Height = _metrics.Height;

        var (cols, rows) = GridGeometry.GridDims(_metrics);
        var left = Math.Max(0, (cols - AppearanceBento.Width) / 2);
        var top = Math.Max(0, (rows - AppearanceBento.Height) / 2);
        var host = new Host(_ticker, _backdrop, _metrics) { Design = design, Samples = _samples };
        var tiles = AppearanceBento.Tiles;
        var boxes = AppearanceBento.Boxes(tiles);
        for (var i = 0; i < tiles.Count; i++)
        {
            var (tile, box) = (tiles[i], boxes[i]);
            try
            {
                var rect = new FracRect((left + box.X) / (double)cols, (top + box.Y) / (double)rows, box.Width / (double)cols, box.Height / (double)rows);
                var cell = GridGeometry.PaintedCell(rect, _metrics, 1);
                var model = WidgetInstance.Create(tile.Id, rect);
                if (tile.Graph && model.Config is IMetricTrend trend)
                {
                    trend.Trend = MetricTrend.Line;
                }

                var card = new WidgetCard(host, model) { Width = cell.Width, Height = cell.Height };
                card.SetLiveRect(rect);
                card.ApplyEditMode(false);
                Canvas.SetLeft(card, cell.X);
                Canvas.SetTop(card, cell.Y);
                _scene.Children.Add(card);
                _cards.Add(card);
            }
            catch (Exception ex)
            {
                Log.Warn(Scope, $"the preview's {tile.Kind} card could not be built; the preview shows the rest", ex);
            }
        }

        Lay();
        _ = SampleCards.ShowAsync([.. _cards], () => _disposed, Scope);
    }

    /// <summary>The cards painted in the design as it stands, then joined where they touch (a fill made Transparent joins nothing).</summary>
    private void Lay()
    {
        foreach (var card in _cards)
        {
            card.ApplyDesign();
        }

        // [S-2240] The cells were put on whole DIPs (scale 1), so the join measures them the same way.
        WidgetCard.Join(_cards, _metrics, 1, editMode: false, _ => 0);
        Fit();
    }

    /// <summary>The stretch the cards stand on, scaled to the page's width; the preview as tall as that makes it.</summary>
    private void Fit()
    {
        if (_cards.Count == 0 || ActualWidth <= 0)
        {
            return;
        }

        var margin = Fluent.Space6;
        var left = _cards.Min(Canvas.GetLeft) - margin;
        var top = _cards.Min(Canvas.GetTop) - margin;
        var right = _cards.Max(c => Canvas.GetLeft(c) + c.Width) + margin;
        var bottom = _cards.Max(c => Canvas.GetTop(c) + c.Height) + margin;
        var scale = ActualWidth / (right - left);
        var height = Math.Round((bottom - top) * scale);
        if (Math.Abs(Height - height) > 0.5 || double.IsNaN(Height))
        {
            Height = height;
        }

        _scene.RenderTransform = new CompositeTransform
        {
            TranslateX = -left * scale,
            TranslateY = -top * scale,
            ScaleX = scale,
            ScaleY = scale,
        };
        Clip = new RectangleGeometry { Rect = new Windows.Foundation.Rect(0, 0, ActualWidth, height) };
    }

    private void Clear()
    {
        foreach (var card in _cards)
        {
            card.Detach();
            _scene.Children.Remove(card);
        }

        _cards.Clear();
    }

    /// <summary>
    /// [S-2364] A kept preview on one page: shown while the page stands, put away (not disposed) when the page goes, so the
    /// next opening shows the same cards instead of building them again. Its owner disposes the preview.
    /// </summary>
    internal sealed partial class Kept : ContentControl, IPagePreview
    {
        private readonly AppearancePreview _preview;

        public Kept(AppearancePreview preview)
        {
            _preview = preview ?? throw new ArgumentNullException(nameof(preview));
            IsTabStop = false;
            HorizontalContentAlignment = HorizontalAlignment.Stretch;
            VerticalContentAlignment = VerticalAlignment.Stretch;
            Content = preview;
            preview.Shown(true);
        }

        public void Refresh() => _preview.Refresh();

        public void Dispose()
        {
            Content = null;
            _preview.Shown(false);
        }
    }

    /// <summary>The preview's cards' host: the display's screen and its blurred wallpaper, nothing persisted.</summary>
    private sealed class Host(Services.Ticker ticker, Backdrop? backdrop, ScreenMetrics metrics) : PreviewCardHost(ticker)
    {
        protected override ScreenMetrics? Screen => metrics;

        public override Backdrop? Backdrop => backdrop;
    }
}
