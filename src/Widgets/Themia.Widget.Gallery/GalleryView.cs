using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Widget.Gallery;

namespace Themia.App.Widgets;

/// <summary>
/// Pictures from a folder on this PC, one picture file, or Bing's photo of the day. [S-300]
/// <para>
/// A thin subscriber ([S-3410]): the path is validated in Core before anything opens a handle, and
/// then the host's one gallery service (<see cref="IWidgetHost.Gallery"/>) does the rest. It reads the source (on the pool, once
/// however many widgets show it, kept current by a folder watcher and the refresh interval, never
/// because this view was built again), decodes the pictures at the size they are drawn and keeps
/// them ([S-3411]), and remembers this widget's slide, deal and pause by widget id ([S-3412]). So a
/// view switch and back draws the same picture in the same pass, and the slideshow continues.
/// </para>
/// <para>
/// This file is the lifecycle: config, the source and the slideshow's timer. The ladder and the
/// drawing are in <c>GalleryView.Layout.cs</c>; the hover bar, menus and commands in
/// <c>GalleryView.Commands.cs</c>.
/// </para>
/// </summary>
internal sealed partial class GalleryView : Grid, IWidgetView, IWidgetHeaderContent, IWidgetSurfaceAware,
    IWidgetSizeAware, IWidgetContextActions, IWidgetKeys
{
    private const string Scope = "gallery-view";

    private readonly DispatcherQueueTimer? _slideTimer;

    private GalleryConfig _config = new();

    /// <summary>
    /// [S-13a] A copy of the settings this view last acted on, which is what a change is measured
    /// against. Not <c>_config</c>: the properties menu edits the live config object in place.
    /// </summary>
    private GalleryConfig _applied = new();

    /// <summary>The card this view draws on, which owns the header it names.</summary>
    private IWidgetSurface? _surface;

    /// <summary>The validated path of whichever local source is configured (the folder or the file).</summary>
    private FolderPathResult _resolved;

    /// <summary>The shared source this widget shows, or null for a configuration with none to read.</summary>
    private GallerySourceKey? _key;

    private IGalleryWatch? _watch;

    /// <summary>What the source read: every picture found, or the state with none.</summary>
    private GalleryScan _scan = GalleryScan.Of(GalleryStatus.Loading);

    /// <summary>[S-3412] This widget's selection from <see cref="_scan"/>: shuffled or not, capped.</summary>
    private IReadOnlyList<string> _pictures = [];

    /// <summary>The slide on show.</summary>
    private int _index;

    /// <summary>
    /// [S-764]/[S-765]/[S-3412] The seed the selection, the scatter and the mosaic are dealt from.
    /// Kept by widget id across view switches; dealt again only by "Refresh images" and, for a
    /// shuffled folder larger than the widget shows, the refresh interval. Never on a resize.
    /// </summary>
    private int _seed = Environment.TickCount;

    /// <summary>[S-769] The user paused the slideshow. Kept for the session by widget id, never persisted.</summary>
    private bool _paused;

    private bool _attached;
    private bool _everApplied;
    private bool _detached;

    /// <summary>[S-6913] What the view asks of the host: the launcher, ⚙ › Accounts.</summary>
    private readonly IWidgetHost _widgetHost;

    public GalleryView(IWidgetHost host)
    {
        _widgetHost = host;
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

    private IGalleries? Service => _widgetHost.Gallery;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        // [S-1881] Subscribed in Attach, not here: a timer's Tick roots the view from the native
        // side, and a view parked under the Set up button is built but never attached or detached.
        if (_slideTimer is not null)
        {
            _slideTimer.Tick -= OnSlideTick;
            _slideTimer.Tick += OnSlideTick;
        }

        _attached = true;
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
        _config = next;
        _applied = (GalleryConfig)next.Clone();
        PublishTitle();

        if (!_attached || _detached)
        {
            return;
        }

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

        GallerySourceKey? key = next.Source == GallerySource.Bing || _resolved.IsValid
            ? GallerySourceKey.For(next.Source, _resolved.Path, next.Recursive)
            : null;

        var first = !_everApplied;
        _everApplied = true;

        // A configuration with nothing to read says which nothing it is, again on every change.
        if (first || key is null || key.Id != _key?.Id)
        {
            Watch(key, restore: first);
        }
        else if (_watch is not null && _watch.Refresh != next.TickInterval)
        {
            // A new cadence for the same source: a new hold, never a new read.
            _watch.Dispose();
            _watch = Service?.Watch(key!, next.TickInterval, OnSourceUpdated);
        }

        // Shuffle and the count are this widget's own: a new selection from the list in hand, no I/O.
        if (previous.Shuffle != next.Shuffle || previous.EffectiveMaxImages != next.EffectiveMaxImages)
        {
            Select(keepPath: CurrentPath);
        }

        ApplySlideCadence();
        Lay();
    }

    /// <summary>
    /// Show a source: let go of the one before (it lingers for whoever else shows it, [S-3413]) and
    /// hold this one. <paramref name="restore"/> is the widget's first attach: it comes back to the
    /// slide, deal and pause it had when it was last built ([S-3412]).
    /// </summary>
    private void Watch(GallerySourceKey? key, bool restore)
    {
        _watch?.Dispose();
        _watch = null;
        _key = key;
        _index = 0;

        var remembered = restore ? Service?.Positions.Recall(_surface?.WidgetId) : null;

        if (remembered is not null)
        {
            _seed = remembered.Seed;
            _paused = remembered.Paused;
        }

        if (key is null)
        {
            var empty = _resolved.Problem != FolderPathProblem.Empty ? GalleryStatus.InvalidPath
                : _config.Source == GallerySource.File ? GalleryStatus.NoFile
                : GalleryStatus.NoFolder;

            _scan = GalleryScan.Of(empty);
            _pictures = [];
            return;
        }

        if (Service is not { } service)
        {
            Log.Warn(Scope, "no gallery service; the widget has nothing to read through");
            _scan = GalleryScan.Of(GalleryStatus.Loading);
            _pictures = [];
            return;
        }

        _watch = service.Watch(key, _config.TickInterval, OnSourceUpdated);
        _scan = _watch.Scan;
        var same = remembered is not null && remembered.SourceId == key.Id;
        Select(keepPath: same ? remembered!.Path : null, index: same ? remembered!.Index : 0);
    }

    /// <summary>
    /// [S-3412] This widget's selection from the source's list, and the slide kept on the picture it
    /// showed (wherever that is now), else at the same position.
    /// </summary>
    private void Select(string? keepPath, int? index = null)
    {
        _pictures = _scan.Status == GalleryStatus.Ok
            ? (_config.IsSingleSource ? _scan.Paths : ImageFiles.Choose(_scan.Paths, _config.Shuffle, _seed, _config.EffectiveMaxImages))
            : [];
        _index = GalleryDeal.Resume(_pictures, keepPath, index ?? _index);
    }

    private string? CurrentPath => _index >= 0 && _index < _pictures.Count ? _pictures[_index] : null;

    /// <summary>The source read again: deal again only when the rule says so, and redraw only what changed.</summary>
    private void OnSourceUpdated(GalleryRefresh reason)
    {
        if (_detached || _watch is null)
        {
            return;
        }

        var scan = _watch.Scan;
        var reroll = GalleryDeal.ShouldReroll(reason, _config.Shuffle, scan.Paths.Count, _config.EffectiveMaxImages);
        var before = _pictures;
        var beforeScan = _scan;

        if (reroll)
        {
            _seed = Environment.TickCount;
        }

        _scan = scan;
        Select(keepPath: CurrentPath);
        ApplySlideCadence();

        // A read that left this widget's selection as it was must not redraw: it would deal the
        // scatter again. "Refresh images" asked for that.
        if (reroll
            || scan.Status != beforeScan.Status
            || scan.Credit != beforeScan.Credit
            || !before.SequenceEqual(_pictures, StringComparer.Ordinal))
        {
            Lay();
        }
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
        && _config.Style == GalleryStyle.Slideshow
        && _scan.Status == GalleryStatus.Ok
        && _pictures.Count > 1;

    private void OnSlideTick(DispatcherQueueTimer sender, object args)
    {
        if (_detached || _pictures.Count == 0)
        {
            return;
        }

        _index = (_index + 1) % _pictures.Count;
        Lay();
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

        // [S-3412] Where this widget was, so it comes back there when it is built again.
        if (_key is not null && _surface?.WidgetId is { Length: > 0 } id)
        {
            Service?.Positions.Remember(id, new GalleryPosition(_key.Id, CurrentPath, _index, _seed, _paused));
        }

        // [S-3413] The source lingers for a view switch back; the pictures this view pinned are let go.
        _watch?.Dispose();
        _watch = null;

        DetachParts();
    }

    /// <summary>
    /// Tell the card what this widget is about. The catalogue can only say "Gallery"; the widget
    /// knows which folder — and on a desktop holding three of them, that is the only thing telling
    /// them apart. [S-2760] A single file keeps the catalogue label: the picture's own name is
    /// never shown.
    /// </summary>
    private void PublishTitle()
    {
        var title = _config.Source switch
        {
            GallerySource.Bing => GalleryStr.WidgetsB_Gallery_PhotoOfTheDay.Text(),
            GallerySource.File => null,
            _ => FolderPath.DisplayName(_config.Path),
        };

        _surface?.SetHeaderTitle(string.IsNullOrWhiteSpace(title) ? null : title);
    }
}
