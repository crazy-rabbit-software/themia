using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using P = Themia.Sdk.Kit.ShellMetrics.Page;

namespace Themia.App.Shell;

/// <summary>
/// [S-2311] The Appearance page's live preview, after Karpo's (karpo-app, Karpo.Dock/LookPreview.cs, docs/widgets.md
/// §592, §613, §671): a stretch of this display's desktop at the page's top, bare — its wallpaper, and on it a few real
/// widget cards (a clock, the weather now, the calendar) on their samples ([S-11850]), drawn by the card itself in the
/// design as it stands, so a style, a colour or a radius picked shows at once. The cards stand where the desktop's would
/// (their frost is the display's own blurred wallpaper, [S-2114]); the stretch is scaled to the page's width. Built once
/// while the page stands; each change of the state paints the cards again (<see cref="Refresh"/>), and only a design
/// replaced whole (an undo, an import) builds them again.
/// </summary>
internal sealed partial class AppearancePreview : Grid, IPagePreview
{
    private const string Scope = "appearance-preview";

    /// <summary>[S-2311] The kinds shown, left to right: one with a big readout, one with a row of facts, one with a grid.</summary>
    private static readonly Sdk.WidgetId[] Kinds = [new("clock"), new("weatherNow"), new("calendar")];

    /// <summary>[S-2311] Where on the display the stretch starts, as fractions of it: the middle, where a wallpaper has its picture.</summary>
    private const double At = 0.3;

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
    /// <param name="metrics">The display's size in DIPs: the cards are sized and their frost placed on it.</param>
    /// <param name="design">The design as it stands now.</param>
    public AppearancePreview(Backdrop? backdrop, ScreenMetrics metrics, Func<DesignState> design)
    {
        _backdrop = backdrop;
        _metrics = metrics;
        _design = design ?? throw new ArgumentNullException(nameof(design));
        var dispatcher = DispatcherQueue.GetForCurrentThread() ?? throw new InvalidOperationException("the preview is built on the UI thread");
        _ticker = new Services.Ticker(dispatcher);
        _samples = new SampleWorld(_ticker, dispatcher);

        Height = PreviewHeight;
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

    /// <summary>[S-2311] The preview's height on the page: a card row and its margins at the page's width.</summary>
    internal const double PreviewHeight = 220;

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

        Lay(design);
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

    /// <summary>The cards made again over the design as it stands: one change of it replaced it whole.</summary>
    private void Build()
    {
        Clear();
        var design = _design();
        _built = design;
        _ground.Source = _backdrop?.Picture;
        _ground.Width = _metrics.Width;
        _ground.Height = _metrics.Height;

        var host = new Host(_ticker, _backdrop, _metrics) { Design = design, Samples = _samples };
        foreach (var kind in Kinds)
        {
            try
            {
                var natural = Sdk.ModuleCatalog.Of(kind).Natural;
                var model = WidgetInstance.Create(kind, FracRect.FromPixels(0, 0, natural.Width, natural.Height, _metrics));
                var card = new WidgetCard(host, model);
                card.ApplyEditMode(false);
                _scene.Children.Add(card);
                _cards.Add(card);
            }
            catch (Exception ex)
            {
                Log.Warn(Scope, $"the preview's {kind} card could not be built; the preview shows the rest", ex);
            }
        }

        Lay(design);
        _ = ShowSamplesAsync();
    }

    /// <summary>The cards side by side at their natural sizes, a little apart, painted in the design.</summary>
    private void Lay(DesignState design)
    {
        var x = _metrics.Width * At;
        var y = _metrics.Height * At;
        var gap = Fluent.Space3;
        foreach (var card in _cards)
        {
            var natural = Sdk.ModuleCatalog.Of(card.Model.Kind).Natural;
            card.Width = natural.Width;
            card.Height = natural.Height;
            Canvas.SetLeft(card, x);
            Canvas.SetTop(card, y);
            card.ApplyDesign();
            x += card.Width + gap;
        }

        Fit();
    }

    /// <summary>The stretch the cards stand on, scaled to the page's width and centred in the preview's height.</summary>
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
        var scale = Math.Min(ActualWidth / (right - left), PreviewHeight / (bottom - top));
        var offsetX = ((ActualWidth / scale) - (right - left)) / 2;
        var offsetY = ((PreviewHeight / scale) - (bottom - top)) / 2;
        _scene.RenderTransform = new CompositeTransform
        {
            TranslateX = (-left + offsetX) * scale,
            TranslateY = (-top + offsetY) * scale,
            ScaleX = scale,
            ScaleY = scale,
        };
        Clip = new RectangleGeometry { Rect = new Windows.Foundation.Rect(0, 0, ActualWidth, PreviewHeight) };
    }

    /// <summary>[S-11850] Each card's sample, as the add page's pictures show them.</summary>
    private async Task ShowSamplesAsync()
    {
        try
        {
            foreach (var card in _cards.ToList())
            {
                if (_disposed)
                {
                    return;
                }

                if (card.ShowsSetup)
                {
                    card.RunForPreview();
                }

                if (card.View is IWidgetSample sample)
                {
                    await sample.ShowSampleAsync().ConfigureAwait(true);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, "a preview card's sample could not be shown; it shows what it has", ex);
        }
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

    /// <summary>The preview's cards' host: the display's screen and its blurred wallpaper, nothing persisted.</summary>
    private sealed class Host(Services.Ticker ticker, Backdrop? backdrop, ScreenMetrics metrics) : PreviewCardHost(ticker)
    {
        protected override ScreenMetrics? Screen => metrics;

        public override Backdrop? Backdrop => backdrop;
    }
}
