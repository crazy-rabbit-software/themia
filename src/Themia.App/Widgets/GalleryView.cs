using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// Pictures from a folder on this PC, one picture file, or Bing's photo of the day. [S-300]
/// <para>
/// Built on the same shape as the folder widget, because it has the same hazards: the path is
/// validated in Core before anything opens a handle, the walk happens on the thread pool because a
/// disconnected share can sit for the better part of a minute, a reply that arrives after the source
/// changed is discarded rather than drawn, and every outcome with no picture in it says <i>which</i>
/// outcome it is.
/// </para>
/// <para>
/// It also has one the folder widget does not: decoding. A folder of phone photographs is tens of
/// megapixels each, and drawing one into a 300px tile at full resolution costs a second of UI thread
/// and ninety megabytes of surface. So nothing is ever decoded at more than the size it is drawn
/// at, and <see cref="ImageLoader"/> bounds both how many are held and how many are decoding at
/// once. [S-302]
/// </para>
/// <para>
/// This file is the lifecycle: config, scans and the slideshow's timer. The ladder and the drawing
/// are in <c>GalleryView.Layout.cs</c>; the hover bar, menus and commands in
/// <c>GalleryView.Commands.cs</c>.
/// </para>
/// </summary>
internal sealed partial class GalleryView : Grid, IWidgetView, IWidgetHeaderContent, IWidgetSurfaceAware,
    IWidgetSizeAware, IWidgetContextActions, IWidgetKeys
{
    private const string Scope = "gallery-view";

    private readonly ImageLoader _loader = new();
    private readonly DispatcherQueueTimer? _slideTimer;

    private Ticker? _ticker;
    private GalleryConfig _config = new();

    /// <summary>
    /// [S-13a] A copy of the settings this view last acted on, which is what a change is measured
    /// against. Not <c>_config</c>: the settings page edits the live config object in place, so by
    /// the time a change arrives the "previous" config already holds the new values, and comparing
    /// the two found no difference — a new address or place stayed unfetched until the next tick.
    /// </summary>
    private GalleryConfig _applied = new();

    /// <summary>The card this view draws on, which owns the header it names.</summary>
    private IWidgetSurface? _surface;

    /// <summary>The validated path of whichever local source is configured (the folder or the file).</summary>
    private FolderPathResult _resolved;

    private GalleryScan _scan = GalleryScan.Of(GalleryStatus.Loading);
    private IDisposable? _subscription;
    private TimeSpan _subscribedInterval = TimeSpan.Zero;
    private CancellationTokenSource? _inFlight;

    /// <summary>
    /// Which scan is current. A reply carrying an older number is a source the widget has stopped
    /// pointing at, and a share that took thirty seconds to answer must not overwrite the folder the
    /// user has since chosen.
    /// </summary>
    private int _generation;

    /// <summary>The slide on show; also where the thumbnail strip starts counting "up next".</summary>
    private int _index;

    /// <summary>
    /// [S-764]/[S-765] The seed the scatter and mosaic are dealt from. Rolled when the set of
    /// pictures changes and on "Refresh images", never on a resize, so a card being dragged bigger
    /// moves its prints rather than dealing a new hand.
    /// </summary>
    private int _seed = Environment.TickCount;

    /// <summary>[S-769] The user paused the slideshow from the hover bar or a menu. Not persisted, as in the original.</summary>
    private bool _paused;

    private bool _everScanned;
    private bool _detached;

    public GalleryView()
    {
        BuildParts();

        // The slideshow owns its own timer rather than counting shared one-second ticks. The shared
        // ticker only knows seconds and minutes, and waking the whole desktop once a second to find
        // out whether ten of them have passed is the cost of a clock for the benefit of a picture.
        _slideTimer = DispatcherQueue.GetForCurrentThread()?.CreateTimer();

        if (_slideTimer is null)
        {
            // A design-time or test host. The pictures still show; only the advance is lost.
            Log.Warn(Scope, "no dispatcher queue on this thread; the slideshow will not advance");
        }
        else
        {
            _slideTimer.IsRepeating = true;
        }
    }

    /// <summary>
    /// The "3/12" counter, in the card's header. Built in the constructor because the card reads
    /// this once, when it builds that header.
    /// </summary>
    public FrameworkElement? HeaderTrailing => _counter;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _ticker = ticker;

        // [S-1881] Subscribed in Attach, not here: a timer's Tick roots the view from the native
        // side, and a view parked under the Set up button is built but never attached or detached.
        if (_slideTimer is not null)
        {
            _slideTimer.Tick -= OnSlideTick;
            _slideTimer.Tick += OnSlideTick;
        }

        ApplyConfig(config);
    }

    /// <inheritdoc />
    public void Bind(IWidgetSurface surface)
    {
        _surface = surface;
        PublishTitle();
    }

    public void ApplyConfig(WidgetConfig config)
    {
        var next = config as GalleryConfig ?? new GalleryConfig();
        var previous = _applied;
        var previouslyResolved = _resolved;
        _config = next;
        _applied = (GalleryConfig)next.Clone();
        PublishTitle();

        // [S-300] The trust boundary, before anything opens a handle. An empty path is not an error
        // here and not a fallback either: it is "nothing chosen", which is its own state.
        var configured = next.Source switch
        {
            GallerySource.Folder => next.Path,
            GallerySource.File => next.FilePath,
            _ => string.Empty,
        };

        _resolved = configured.Trim().Length == 0
            ? new FolderPathResult(FolderPathProblem.Empty, string.Empty, false)
            : FolderPath.Validate(configured);

        // Only the fields that change what comes back are worth a rescan. Dragging the count slider
        // must not fire a directory walk per frame.
        var sourceChanged = previous.Source != next.Source
            || _resolved.Path != previouslyResolved.Path
            || _resolved.Problem != previouslyResolved.Problem;

        var needsScan = !_everScanned
            || sourceChanged
            || previous.Recursive != next.Recursive
            || previous.Shuffle != next.Shuffle
            || previous.EffectiveMaxImages != next.EffectiveMaxImages;

        if (sourceChanged)
        {
            // Pictures from the source the widget has left are dead weight, and holding them would
            // only evict the ones it is about to want. The old scan goes too, so its pictures are
            // not drawn under the new source's name while the new one loads.
            _loader.Clear();
            _index = 0;
            _scan = GalleryScan.Of(GalleryStatus.Loading);
        }

        ApplySlideCadence();

        // Subscribing fires a tick of its own, and that tick rescans with the config already in
        // place above; rescanning here as well would read the source twice.
        if (Resubscribe())
        {
            return;
        }

        if (needsScan)
        {
            Rescan();
        }
        else
        {
            Lay();
        }
    }

    /// <summary>
    /// Rebuild the ticker subscription only when the cadence actually changed, and say whether it
    /// did — a new subscription ticks immediately, so the caller has nothing left to do.
    /// </summary>
    private bool Resubscribe()
    {
        var interval = _config.TickInterval;

        if (_subscription is not null && interval == _subscribedInterval)
        {
            return false;
        }

        _subscription?.Dispose();
        _subscribedInterval = interval;
        _subscription = _ticker?.Subscribe(interval, Rescan);
        return _subscription is not null;
    }

    /// <summary>
    /// [S-770] Run the slideshow's timer only while it has something to do: the slideshow layout, a
    /// folder with more than one picture, and nobody has paused it.
    /// </summary>
    private void ApplySlideCadence()
    {
        if (_slideTimer is null)
        {
            return;
        }

        if (!IsSlideshow || _paused)
        {
            // Nothing to advance to means nothing to wake for. A one-picture folder is a picture
            // frame, not a slideshow.
            _slideTimer.Stop();
            return;
        }

        if (_slideTimer.Interval != _config.SlideInterval)
        {
            _slideTimer.Interval = _config.SlideInterval;
        }

        _slideTimer.Start();
    }

    /// <summary>A folder in the slideshow layout with more than one picture to move between.</summary>
    private bool IsSlideshow => !_config.IsSingleSource
        && _config.Layout == GalleryLayout.Slideshow
        && _scan.Status == GalleryStatus.Ok
        && _scan.Paths.Count > 1;

    private void OnSlideTick(DispatcherQueueTimer sender, object args)
    {
        if (_detached || _scan.Paths.Count == 0)
        {
            return;
        }

        _index = (_index + 1) % _scan.Paths.Count;
        Lay();
    }

    private void Rescan() => Rescan(reroll: false);

    /// <summary>
    /// Start a read of the source. The work itself happens on the thread pool: a mapped drive whose
    /// server has gone away can leave a single directory call sitting for most of a minute, and on
    /// the UI thread that is every widget on the desktop frozen with it.
    /// </summary>
    /// <param name="reroll">"Refresh images": deal a new scatter / mosaic even if the set is the same.</param>
    private void Rescan(bool reroll)
    {
        if (_detached)
        {
            return;
        }

        _everScanned = true;

        if (_config.Source != GallerySource.Bing && !_resolved.IsValid)
        {
            var empty = _resolved.Problem != FolderPathProblem.Empty ? GalleryStatus.InvalidPath
                : _config.Source == GallerySource.File ? GalleryStatus.NoFile
                : GalleryStatus.NoFolder;

            _scan = GalleryScan.Of(empty);
            ApplySlideCadence();
            Lay();
            return;
        }

        _inFlight?.Cancel();

        var cts = new CancellationTokenSource();
        _inFlight = cts;
        var generation = ++_generation;

        // Say something immediately, but only when there is nothing on screen to keep: a backstop
        // rescan of the same folder should not blank the picture it is about to redraw.
        if (_scan.Paths.Count == 0)
        {
            _scan = GalleryScan.Of(GalleryStatus.Loading);
            Lay();
        }

        _ = ScanAsync(Read(cts.Token), generation, reroll, cts);
    }

    /// <summary>The read for the configured source, already started on the thread pool.</summary>
    private Task<GalleryScan> Read(CancellationToken token)
    {
        switch (_config.Source)
        {
            case GallerySource.Bing:
                return Task.Run(() => ImageGallery.ReadBingAsync(token), token);

            case GallerySource.File:
                var file = _resolved.Path;
                return Task.Run(() => ImageGallery.ReadFile(file), token);

            default:
                var request = new GalleryRequest(
                    _resolved.Path,
                    _config.Recursive,
                    _config.Shuffle,

                    // Re-rolled per scan, so a folder larger than the cap eventually shows all of it
                    // rather than the same two dozen pictures for ever.
                    Environment.TickCount,
                    _config.EffectiveMaxImages);

                return Task.Run(() => ImageGallery.Read(request, token), token);
        }
    }

    private async Task ScanAsync(Task<GalleryScan> reading, int generation, bool reroll, CancellationTokenSource cts)
    {
        try
        {
            var scan = await reading.ConfigureAwait(true);

            // A late reply from a source the widget has since left. Discarded rather than drawn.
            if (_detached || generation != _generation)
            {
                return;
            }

            var sameSet = scan.Status == _scan.Status
                && scan.Caption == _scan.Caption
                && scan.Paths.SequenceEqual(_scan.Paths, StringComparer.Ordinal);

            _scan = scan;

            if (_index >= scan.Paths.Count)
            {
                _index = 0;
            }

            if (!sameSet || reroll)
            {
                _seed = Environment.TickCount;
            }

            ApplySlideCadence();

            // A backstop rescan that found exactly what the last one found must not redraw: it
            // would deal the scatter again and re-decode the grid. "Refresh images" asked for that.
            if (!sameSet || reroll)
            {
                Lay();
            }
        }
        catch (OperationCanceledException)
        {
            // ignore: superseded by a newer scan, which will draw in this one's place.
        }
        finally
        {
            if (ReferenceEquals(_inFlight, cts))
            {
                _inFlight = null;
            }

            cts.Dispose();
        }
    }

    public void Detach()
    {
        if (_detached)
        {
            return;
        }

        _detached = true;

        if (_slideTimer is not null)
        {
            _slideTimer.Stop();
            _slideTimer.Tick -= OnSlideTick;
        }

        _subscription?.Dispose();
        _subscription = null;
        _subscribedInterval = TimeSpan.Zero;

        // Cancel but do not dispose: the in-flight scan owns its source and frees it on the way out.
        _inFlight?.Cancel();
        _inFlight = null;

        DetachParts();
        _loader.Clear();
        _ticker = null;
    }

    /// <summary>
    /// Tell the card what this widget is about. The catalogue can only say "Gallery"; the widget
    /// knows which folder or which picture — and on a desktop holding three of them, that is the
    /// only thing telling them apart.
    /// </summary>
    private void PublishTitle()
    {
        var title = _config.Source switch
        {
            GallerySource.Bing => Str.WidgetsB_Gallery_PhotoOfTheDay.Text(),
            GallerySource.File => FileTitle(_config.FilePath),
            _ => FolderPath.DisplayName(_config.Path),
        };

        _surface?.SetHeaderTitle(string.IsNullOrWhiteSpace(title) ? null : title);
    }

    private static string FileTitle(string path)
    {
        try
        {
            return System.IO.Path.GetFileNameWithoutExtension(path.Trim());
        }
        catch (ArgumentException)
        {
            return string.Empty; // ignore: an unusable path has no name; the empty state says why.
        }
    }
}
