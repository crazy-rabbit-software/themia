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
using Themia.Sdk.WinUI;

namespace Themia.App.Shell;

/// <summary>[S-11710] A tile's picture: the thing drawn, and its size in DIPs.</summary>
internal sealed record AddPicture(ImageSource Image, double Width, double Height);

/// <summary>
/// [S-11710] The add page's pictures: each tile's widget, or its ready-made group as one joined panel,
/// built as real cards (the renders' path: a preview card host, the kind's natural size, a group's boxes
/// joined as the desktop joins them, [S-2240]) with sample data only — the sample PC's hub for the system
/// readings ([S-1931]), each view's "sample" preview state (else its first, never an "are you sure?"),
/// the providers' recordings for a kit list ([S-9929]) — and drawn to a bitmap off the UI's hot path:
/// one at a time, after the page is up, the cards on a stage under the page's ground and gone once
/// drawn. Cached per look (the design, light or dark, the language) and tile, so a page opened again
/// shows them at once; a new look starts the cache again.
/// <para>
/// Sample data lives in Debug builds only (the views' preview states, the sample PC); a Release build
/// draws no picture, and every tile keeps its kind's glyph on its box ([S-11711]).
/// </para>
/// </summary>
internal sealed class AddPictures : IDisposable
{
    private const string Scope = "add-pictures";

    /// <summary>The look the cache holds pictures for, and the pictures by tile key.</summary>
    private static string _look = string.Empty;

    private static readonly Dictionary<string, AddPicture> Cache = new(StringComparer.Ordinal);

    private readonly Canvas _stage;
    private readonly Queue<(AddTile Tile, Action<AddPicture> Ready)> _wanted = new();
    private readonly HashSet<string> _queued = new(StringComparer.Ordinal);
    private readonly DispatcherQueue? _dispatcher;
    private TaskCompletionSource _idle = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _running;
    private bool _disposed;

#if DEBUG
    private Services.Ticker? _ticker;
    private Services.SystemData.SystemHub? _hub;
#endif

    /// <param name="stage">Where the cards stand while drawn: in the live tree, under the page's ground.</param>
    public AddPictures(Canvas stage)
    {
        _stage = stage ?? throw new ArgumentNullException(nameof(stage));
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        _idle.SetResult();
        var look = Look();
        if (!string.Equals(look, _look, StringComparison.Ordinal))
        {
            Cache.Clear();
            _look = look;
        }
    }

    /// <summary>[S-11711] Whether this build draws pictures at all (its samples exist only in Debug).</summary>
    public static bool Draws =>
#if DEBUG
        true;
#else
        false;
#endif

    /// <summary>Done once every picture asked for is drawn (or failed): a render waits for it.</summary>
    public Task Idle => _idle.Task;

    /// <summary>The tile's picture in the cache, or null.</summary>
    public static AddPicture? Cached(AddTile tile) => Cache.GetValueOrDefault(tile.Key);

    /// <summary>Draw <paramref name="tile"/>'s picture when its turn comes; <paramref name="ready"/> runs with it.</summary>
    public void Want(AddTile tile, Action<AddPicture> ready)
    {
        ArgumentNullException.ThrowIfNull(tile);
        ArgumentNullException.ThrowIfNull(ready);
        if (!Draws || _disposed || tile is AddItemTile || !_queued.Add(tile.Key))
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
        _disposed = true;
        _wanted.Clear();
#if DEBUG
        _hub?.Dispose();
        _hub = null;
        _ticker?.Dispose();
        _ticker = null;
#endif
    }

    /// <summary>The look a picture belongs to: the design as stored, light or dark, the language.</summary>
    private static string Look()
    {
        var design = DesignApplier.Current ?? new DesignState();
        return $"{Strings.Current}|{DesignApplier.IsDark}|{System.Text.Json.JsonSerializer.Serialize(design, StateJson.DesignState)}";
    }

    private async Task RunAsync()
    {
        try
        {
            while (!_disposed && _wanted.TryDequeue(out var next))
            {
                _queued.Remove(next.Tile.Key);
                if (Cache.GetValueOrDefault(next.Tile.Key) is not { } picture)
                {
                    picture = await DrawAsync(next.Tile).ConfigureAwait(true);
                    if (picture is null || _disposed)
                    {
                        continue;
                    }

                    Cache[next.Tile.Key] = picture;
                }

                next.Ready(picture);
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

#if DEBUG
    /// <summary>One tile's cards on the stage, their samples shown, drawn to a bitmap, then taken away.</summary>
    private async Task<AddPicture?> DrawAsync(AddTile tile)
    {
        var dispatcher = _dispatcher!;
        _ticker ??= new Services.Ticker(dispatcher);
        _hub ??= Services.SystemData.SampleMachine.Hub(_ticker, dispatcher);

        var design = DesignApplier.Current ?? new DesignState();
        var host = new Gallery.NoticeRender.RenderCardHost(_ticker) { Design = design };
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
                return null;
        }

        var panel = new Canvas();
        var cards = new List<WidgetCard>();
        var before = Services.AppServices.System;
        try
        {
            // [S-1931] The sample PC's hub, for these cards alone: a view watches its hub as it attaches.
            Services.AppServices.System = _hub;
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
        }
        finally
        {
            Services.AppServices.System = before;
        }

        try
        {
            var right = cards.Max(c => Canvas.GetLeft(c) + c.Width);
            var bottom = cards.Max(c => Canvas.GetTop(c) + c.Height);
            panel.Width = right;
            panel.Height = bottom;
            _stage.Children.Add(panel);

            // [S-2240] A group's cards join as on the desktop.
            WidgetCard.Join(cards, metrics, design.WidgetGapPx, panel.XamlRoot?.RasterizationScale ?? 1, editMode: false, _ => 0);
            await SettleAsync(panel).ConfigureAwait(true);

            foreach (var card in cards)
            {
                if (card.View is Gallery.IWidgetPreviewState preview && SampleOf(preview) is { } state)
                {
                    if (card.ShowsSetup)
                    {
                        card.RunForPreview();
                        await SettleAsync(panel).ConfigureAwait(true);
                    }

                    if (card.View is Gallery.IWidgetPreviewState shown)
                    {
                        await shown.ShowPreviewStateAsync(state).ConfigureAwait(true);
                    }
                }
            }

            await SettleAsync(panel).ConfigureAwait(true);
            if (_disposed || panel.XamlRoot is null)
            {
                return null;
            }

            var bitmap = new RenderTargetBitmap();
            await bitmap.RenderAsync(panel);
            return new AddPicture(bitmap, right, bottom);
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, $"the picture of {tile.Key} could not be drawn; its tile keeps its glyph", ex);
            return null;
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

    /// <summary>[S-11710] The state a picture shows: "sample" where the view has it, else its first, never an "are you sure?".</summary>
    private static string? SampleOf(Gallery.IWidgetPreviewState preview)
        => preview.PreviewStates.Contains("sample", StringComparer.Ordinal)
            ? "sample"
            : preview.PreviewStates.FirstOrDefault(s => !string.Equals(s, "confirm", StringComparison.Ordinal));

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
#else
    private static Task<AddPicture?> DrawAsync(AddTile tile) => Task.FromResult<AddPicture?>(null);
#endif
}
