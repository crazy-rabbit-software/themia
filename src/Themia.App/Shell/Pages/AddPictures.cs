using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.App.Widgets;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell;

/// <summary>[S-11710] A tile's picture: the thing drawn, and its size in DIPs.</summary>
internal sealed record AddPicture(ImageSource Image, double Width, double Height);

/// <summary>
/// [S-11710] The add page's pictures: each tile's widget, or its ready-made group as one joined panel,
/// built as real cards (a preview card host, the kind's natural size, a group's boxes joined as the
/// desktop joins them, [S-2240]) with sample data only, in every build ([S-11850]): the cards' host hands
/// them a sample world of their own ([S-11851]: the sample PC's hub, [S-1931], a gallery service), each
/// view that has one shows its sample (<see cref="IWidgetSample"/>), a kit list reads no account — and
/// drawn to a bitmap off the UI's hot path: one at a time, after the page is up, the cards on a stage
/// under the page's ground and gone once drawn. Cached per look (the design, light or dark, the language)
/// and tile, so a page opened again shows them at once; a new look starts the cache again. The cache is
/// bounded ([S-11853]) and dropped a minute after the last page closed.
/// </summary>
internal sealed class AddPictures : IDisposable
{
    private const string Scope = "add-pictures";

    /// <summary>[S-11853] At most this many pictures are kept (every tab's tiles, and a search's, twice over).</summary>
    internal const int MaxPictures = 160;

    /// <summary>[S-11853] At most this many bytes of their pixels.</summary>
    internal const long MaxBytes = 48L * 1024 * 1024;

    /// <summary>[S-11853] How long the pictures stay once the last page closed: a page opened again soon shows them at once.</summary>
    private static readonly TimeSpan KeepAfterClose = TimeSpan.FromMinutes(1);

    /// <summary>[S-11852] The longest a picture waits for its rows' icons before it is drawn without them.</summary>
    private static readonly TimeSpan IconWait = TimeSpan.FromSeconds(2);

    /// <summary>The look the cache holds pictures for, and the pictures by tile key.</summary>
    private static string _look = string.Empty;

    private static readonly AddPictureCache<AddPicture> Cache = new(MaxPictures, MaxBytes);

    /// <summary>How many pages are open (one per display at most), and the timer that drops the cache once none is.</summary>
    private static int _open;

    private static DispatcherQueueTimer? _drop;

    private readonly Canvas _stage;
    private readonly Queue<(AddTile Tile, Action<AddPicture> Ready)> _wanted = new();
    private readonly HashSet<string> _queued = new(StringComparer.Ordinal);
    private readonly DispatcherQueue? _dispatcher;
    private TaskCompletionSource _idle = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _running;
    private bool _disposed;

    /// <summary>[S-11851] This page's sample world and the ticker its cards run on, made with the first picture.</summary>
    private Services.Ticker? _ticker;

    private SampleWorld? _samples;

    /// <param name="stage">Where the cards stand while drawn: in the live tree, under the page's ground.</param>
    public AddPictures(Canvas stage)
    {
        _stage = stage ?? throw new ArgumentNullException(nameof(stage));
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        _idle.SetResult();
        _open++;
        _drop?.Stop();
        var look = Look();
        if (!string.Equals(look, _look, StringComparison.Ordinal))
        {
            Cache.Clear();
            _look = look;
        }
    }

    /// <summary>Done once every picture asked for is drawn (or failed): a render waits for it.</summary>
    public Task Idle => _idle.Task;

    /// <summary>The tile's picture in the cache, or null.</summary>
    public static AddPicture? Cached(AddTile tile) => Cache.Get(tile.Key);

    /// <summary>[S-11853] How many pictures are kept, and the bytes of their pixels.</summary>
    internal static (int Count, long Bytes) Held => (Cache.Count, Cache.Bytes);

    /// <summary>Draw <paramref name="tile"/>'s picture when its turn comes; <paramref name="ready"/> runs with it.</summary>
    public void Want(AddTile tile, Action<AddPicture> ready)
    {
        ArgumentNullException.ThrowIfNull(tile);
        ArgumentNullException.ThrowIfNull(ready);
        if (_disposed || tile is AddItemTile || !_queued.Add(tile.Key))
        {
            return;
        }

        _wanted.Enqueue((tile, ready));
        if (!_running)
        {
            _running = true;
            _idle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            // After the page's own frame: the page shows first, the pictures follow.
            if (_dispatcher is null || !_dispatcher.TryEnqueue(DispatcherQueuePriority.Low, () => _ = RunAsync()))
            {
                Log.Warn(Scope, "could not queue the pictures; the tiles keep their glyphs");
                _running = false;
                _idle.TrySetResult();
            }
        }
    }

    /// <summary>The page shows other tiles now: what was asked for and not yet drawn waits no more (the one being drawn finishes).</summary>
    public void Forget()
    {
        _wanted.Clear();
        _queued.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _wanted.Clear();
        _samples?.Dispose();
        _samples = null;
        _ticker?.Dispose();
        _ticker = null;

        // [S-11853] The last page closed: the pictures stay a minute for a page opened again, then go.
        if (--_open == 0 && _dispatcher is not null)
        {
            _drop ??= CreateDrop(_dispatcher);
            _drop.Start();
        }
    }

    private static DispatcherQueueTimer CreateDrop(DispatcherQueue dispatcher)
    {
        var timer = dispatcher.CreateTimer();
        timer.Interval = KeepAfterClose;
        timer.IsRepeating = false;
        timer.Tick += (_, _) =>
        {
            if (_open == 0)
            {
                Log.Info(Scope, $"no add page for {KeepAfterClose.TotalSeconds:0} s: {Cache.Count} pictures ({Cache.Bytes / 1024} KB) let go");
                Cache.Clear();
            }
        };
        return timer;
    }

    /// <summary>The look a picture belongs to: the design as stored, light or dark, the language.</summary>
    private static string Look()
    {
        var design = DesignApplier.Current ?? new DesignState();
        return $"{Strings.Current}|{DesignApplier.IsDark}|{System.Text.Json.JsonSerializer.Serialize(design, StateJson.DesignState)}";
    }

    private async Task RunAsync()
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var drawn = 0;
        try
        {
            while (!_disposed && _wanted.TryDequeue(out var next))
            {
                _queued.Remove(next.Tile.Key);
                if (Cache.Get(next.Tile.Key) is not { } picture)
                {
                    var (bitmap, bytes) = await DrawAsync(next.Tile).ConfigureAwait(true);
                    if (bitmap is null || _disposed)
                    {
                        continue;
                    }

                    picture = bitmap;
                    drawn++;
                    Cache.Put(next.Tile.Key, picture, bytes);
                }

                next.Ready(picture);
            }

            if (drawn > 0)
            {
                Log.Info(Scope, $"drew {drawn} pictures in {clock.ElapsedMilliseconds} ms; {Cache.Count} kept, {Cache.Bytes / 1024} KB");
            }
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "drawing the add page's pictures failed; the rest keep their glyphs", ex);
        }
        finally
        {
            _running = false;
            _idle.TrySetResult();
        }
    }

    /// <summary>One tile's cards on the stage, their samples shown, drawn to a bitmap (and the bytes of its pixels), then taken away.</summary>
    private async Task<(AddPicture? Picture, long Bytes)> DrawAsync(AddTile tile)
    {
        var dispatcher = _dispatcher!;
        _ticker ??= new Services.Ticker(dispatcher);
        _samples ??= new SampleWorld(_ticker, dispatcher);

        var design = DesignApplier.Current ?? new DesignState();

        // [S-11851] The cards are handed the sample world through their host: nothing static is swapped.
        var host = new PreviewCardHost(_ticker) { Design = design, Samples = _samples };
        var metrics = host.Metrics;
        List<WidgetInstance> models;
        switch (tile)
        {
            case AddWidgetTile one:
                var natural = Sdk.ModuleCatalog.Of(one.Kind).Natural;
                models = [WidgetInstance.Create(one.Kind, Core.Geometry.FracRect.FromPixels(0, 0, natural.Width, natural.Height, metrics))];
                break;
            case AddGroupTile group when ReadyMadeGroups.Build(group.Group, metrics) is { } built:
                models = built.Widgets;
                break;
            default:
                return (null, 0);
        }

        var panel = new Canvas();
        var cards = new List<WidgetCard>();
        try
        {
            foreach (var model in models)
            {
                var shown = Core.Geometry.SizeFit.Shown(model.Rect, Core.Geometry.SizeFit.Limits(model, metrics, 1, design.WidgetGapPx), metrics);
                var cell = Core.Geometry.GridGeometry.ToPixels(shown, metrics);
                var card = new WidgetCard(host, model) { Width = cell.Width, Height = cell.Height };
                card.SetLiveRect(shown);
                card.ApplyEditMode(false);
                card.ApplyGap(design.WidgetGapPx);
                Canvas.SetLeft(card, cell.X);
                Canvas.SetTop(card, cell.Y);
                panel.Children.Add(card);
                cards.Add(card);
            }

            var right = cards.Max(c => Canvas.GetLeft(c) + c.Width);
            var bottom = cards.Max(c => Canvas.GetTop(c) + c.Height);
            panel.Width = right;
            panel.Height = bottom;
            _stage.Children.Add(panel);

            // [S-2240] A group's cards join as on the desktop.
            WidgetCard.Join(cards, metrics, design.WidgetGapPx, panel.XamlRoot?.RasterizationScale ?? 1, editMode: false, _ => 0);
            await SettleAsync(panel).ConfigureAwait(true);

            // [S-11850] Each view with a sample shows it; one without draws what its host hands it (the sample PC's readings).
            foreach (var card in cards)
            {
                if (card.View is IWidgetSample)
                {
                    if (card.ShowsSetup)
                    {
                        card.RunForPreview();
                        await SettleAsync(panel).ConfigureAwait(true);
                    }

                    if (card.View is IWidgetSample shown)
                    {
                        await shown.ShowSampleAsync().ConfigureAwait(true);
                    }
                }
            }

            await SettleAsync(panel).ConfigureAwait(true);

            // [S-11852] The rows' program and file icons come from the shell asynchronously: wait for them, within a bound.
            if (!AppIconSource.Quiet.IsCompleted)
            {
                await Task.WhenAny(AppIconSource.Quiet, Task.Delay(IconWait)).ConfigureAwait(true);
                await SettleAsync(panel).ConfigureAwait(true);
            }

            if (_disposed || panel.XamlRoot is null)
            {
                return (null, 0);
            }

            var bitmap = new RenderTargetBitmap();
            await bitmap.RenderAsync(panel);
            return (new AddPicture(bitmap, right, bottom), 4L * bitmap.PixelWidth * bitmap.PixelHeight);
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, $"the picture of {tile.Key} could not be drawn; its tile keeps its glyph", ex);
            return (null, 0);
        }
        finally
        {
            foreach (var card in cards)
            {
                card.Detach();
            }

            _stage.Children.Remove(panel);
        }
    }

    /// <summary>
    /// What is on the stage laid out, and the work it queued (a view's first fill, its bindings) run: the
    /// dispatcher's normal work goes before this low-priority turn. Not a frame count: nothing changing on
    /// screen draws no frame, so waiting for frames waited for ever.
    /// </summary>
    private Task SettleAsync(FrameworkElement panel)
    {
        panel.UpdateLayout();
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_dispatcher!.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            panel.UpdateLayout();
            done.TrySetResult();
        }))
        {
            done.TrySetResult();
        }

        return done.Task;
    }
}
