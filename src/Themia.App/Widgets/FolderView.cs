using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Themia.App.Services;
using Themia.App.Services.Files;
using Themia.App.Shell;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// [S-1320] A folder on this PC as a small Windows 11 file browser: list, details, grid or icons;
/// navigation inside the widget or out to Explorer; Explorer's selection, keys, menus and file
/// operations; the real Windows file clipboard; drag out to any app and drop in from Explorer.
/// <para>
/// Split by concern: this file is the lifecycle, loading and navigation; <c>.Render</c> draws the
/// ladder ([S-1323]); <c>.Input</c> is pointer, keys, selection and drag and drop; <c>.Commands</c> is
/// the menus and file operations. Every rule with an edge case lives in Core (<see cref="FolderLayout"/>,
/// <see cref="FolderSelection"/>, <see cref="FolderGesture"/>, <see cref="FolderHistory"/>,
/// <see cref="FolderFilter"/>, <see cref="Themia.Core.Files.FileDrops"/>, …) and is tested there.
/// </para>
/// <para>
/// Nothing is ever deleted permanently: Delete asks first and sends to the Recycle Bin through the
/// shell ([S-1330]).
/// </para>
/// </summary>
internal sealed partial class FolderView : Grid, IWidgetView, IWidgetHeaderContent, IWidgetSurfaceAware,
    IWidgetSizeAware, IWidgetKeys, IWidgetContextActions
{
    private const string Scope = "folder";

    /// <summary>
    /// The most items one view draws. A folder of fifty thousand files would be fifty thousand rows
    /// of XAML; past this the footer row says how many more there are and opens Explorer.
    /// </summary>
    public const int RenderCap = 1000;

    private static readonly string ProfileFolder =
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    private readonly FolderSelection _selection = new();
    private readonly FolderGesture _gesture = new();
    private readonly FolderHistory _history = new(string.Empty);

    private Ticker? _ticker;
    private IDisposable? _subscription;
    private TimeSpan _subscribedInterval;
    private FolderConfig _config = new();

    /// <summary>
    /// A copy of the settings last acted on: the settings page edits the live object in place, so the
    /// previous values are only knowable from a copy.
    /// </summary>
    private FolderConfig _applied = new();

    private IWidgetSurface? _surface;
    private FolderPathResult _resolved;
    private FolderSnapshot _snapshot = FolderSnapshot.Of(FolderListingStatus.Loading);
    private string _snapshotPath = string.Empty;
    private string _watchedPath = string.Empty;
    private FolderWatch? _watch;
    private CancellationTokenSource? _inFlight;
    private int _generation;
    private bool _loading;
    private bool _dirty;
    private bool _everLoaded;
    private bool _detached;

    /// <summary>The scroll offset to restore once the place Back / Forward returned to has drawn ([S-1326]).</summary>
    private double? _restoreScroll;

    private GitRepoStatus? _git;

    public FolderView()
    {
        BuildChrome();

        // Event-driven: the clipboard changing (a copy here or in another folder widget) redraws the
        // cut dims and what Paste offers.
        FileClipboard.Changed += OnClipboardChanged;
    }

    // ---- lifecycle ------------------------------------------------------------------------------

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _ticker = ticker;

        // Warm the New menu's template list on the pool, so the first right-click has it.
        _ = ShellNewTemplates.GetAsync();
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
        var next = config as FolderConfig ?? new FolderConfig();
        var previous = _applied;

        _config = next;
        _applied = (FolderConfig)next.Clone();

        var resolved = FolderPath.Resolve(next.Path, ProfileFolder);
        var pathChanged = resolved.Path != _resolved.Path || resolved.Problem != _resolved.Problem;
        _resolved = resolved;

        if (pathChanged || !_everLoaded || previous.Navigation != next.Navigation)
        {
            // A new folder, or a change of mode: start over there, history gone.
            _history.Reset(resolved.IsValid ? resolved.Path : string.Empty);
            _selection.Clear();
            CloseSearch();
        }

        PublishTitle();
        Lay();

        if (Resubscribe())
        {
            return;
        }

        if (!_everLoaded || pathChanged || previous.Navigation != next.Navigation)
        {
            Reload(force: true);
        }
        else
        {
            // Order, filters, view, sizes, columns: all drawn from what is in hand.
            Render();
        }
    }

    /// <summary>Rebuild the backstop timer only when its cadence changed; says whether it did (a new subscription ticks at once).</summary>
    private bool Resubscribe()
    {
        // [S-1333] The watcher is the refresh. Without one (a share that sends no notifications) the
        // original's 5 s fallback poll; with one, the slow backstop.
        var interval = _watch is { Watching: false } ? TimeSpan.FromSeconds(5) : _config.TickInterval;

        if (_subscription is not null && interval == _subscribedInterval)
        {
            return false;
        }

        _subscription?.Dispose();
        _subscribedInterval = interval;
        _subscription = _ticker?.Subscribe(interval, () => Reload(force: false));
        return _subscription is not null;
    }

    public void Detach()
    {
        if (_detached)
        {
            return;
        }

        _detached = true;
        FileClipboard.Changed -= OnClipboardChanged;

        _subscription?.Dispose();
        _subscription = null;
        _watch?.Dispose();
        _watch = null;
        _watchedPath = string.Empty;

        // Cancel, not dispose: the in-flight load owns its source and frees it on the way out.
        _inFlight?.Cancel();
        _inFlight = null;

        DetachInput();
        ClearItems();
        _fade?.Dispose();
        _fade = null;
        _ticker = null;
    }

    // ---- where the widget is ---------------------------------------------------------------------

    /// <summary>The folder on screen: the history's place in navigate mode, the setting otherwise.</summary>
    private string CurrentPath => _config.Navigation == FolderNavigation.Navigate && _history.Current.Length > 0
        ? _history.Current
        : _resolved.Path;

    private bool Navigates => _config.Navigation == FolderNavigation.Navigate;

    /// <summary>[S-1326] Go to <paramref name="path"/> inside the widget (navigate mode).</summary>
    private void NavigateTo(string path)
    {
        if (!_history.Go(path, _scroll.VerticalOffset))
        {
            return;
        }

        Arrived(null);
    }

    private void GoBack()
    {
        if (Navigates && _history.Back(_scroll.VerticalOffset) is { } place)
        {
            Arrived(place.Scroll);
        }
    }

    private void GoForward()
    {
        if (Navigates && _history.Forward(_scroll.VerticalOffset) is { } place)
        {
            Arrived(place.Scroll);
        }
    }

    private void GoUp()
    {
        if (Navigates && _history.UpTarget() is { } parent)
        {
            NavigateTo(parent);
        }
    }

    private void GoHome()
    {
        if (Navigates && !_history.AtHome)
        {
            NavigateTo(_history.Home);
        }
    }

    /// <summary>A new place: drop the old listing, selection and search, and read the new one.</summary>
    private void Arrived(double? scroll)
    {
        _restoreScroll = scroll;
        _selection.Clear();
        CloseSearch();
        _snapshot = FolderSnapshot.Of(FolderListingStatus.Loading);
        _git = null;
        PublishTitle();
        Lay();
        Reload(force: true);
    }

    // ---- loading ---------------------------------------------------------------------------------

    /// <summary>
    /// Read the folder on the thread pool. A read already running for the same folder is not
    /// restarted: a burst of change notices marks it dirty and one more read follows it ([S-1333]).
    /// <paramref name="force"/> (a new folder) abandons the running read instead.
    /// </summary>
    private void Reload(bool force)
    {
        if (_detached)
        {
            return;
        }

        _everLoaded = true;
        var path = CurrentPath;

        if (!_resolved.IsValid || path.Length == 0)
        {
            _snapshot = FolderSnapshot.Of(FolderListingStatus.InvalidPath);
            Render();
            return;
        }

        if (_loading && !force && string.Equals(path, _snapshotPath, StringComparison.OrdinalIgnoreCase))
        {
            _dirty = true;
            return;
        }

        if (!string.Equals(path, _watchedPath, StringComparison.OrdinalIgnoreCase))
        {
            Watch(path);
        }

        _inFlight?.Cancel();
        var cts = new CancellationTokenSource();
        _inFlight = cts;
        var generation = ++_generation;
        _loading = true;
        _dirty = false;

        if (!string.Equals(path, _snapshotPath, StringComparison.OrdinalIgnoreCase))
        {
            _snapshot = FolderSnapshot.Of(FolderListingStatus.Loading);
            _snapshotPath = path;
            Render();
        }

        _ = LoadAsync(path, generation, cts);
    }

    private async Task LoadAsync(string path, int generation, CancellationTokenSource cts)
    {
        try
        {
            SetBusy(true);
            var snapshot = await Task.Run(() => FolderListing.Read(path, cts.Token), cts.Token).ConfigureAwait(true);

            if (_detached || generation != _generation)
            {
                return;
            }

            var unchanged = snapshot.Status == _snapshot.Status && FolderRules.SameContent(snapshot.Entries, _snapshot.Entries);
            _snapshot = snapshot;

            if (!unchanged)
            {
                Render();
            }

            RefreshGit(path, generation);
        }
        catch (OperationCanceledException)
        {
            // ignore: superseded by a newer read, which draws in this one's place.
        }
        finally
        {
            if (ReferenceEquals(_inFlight, cts))
            {
                _inFlight = null;
                _loading = false;
                SetBusy(false);

                if (_dirty && !_detached)
                {
                    Reload(force: false);
                }
            }

            cts.Dispose();
        }
    }

    /// <summary>[S-1325] The git badge and column, when the folder is in a repository and either is shown.</summary>
    private async void RefreshGit(string path, int generation)
    {
        try
        {
            // Asked whether the badge would have room if there were a repository, not whether it is
            // shown now: it is not shown until the status exists.
            var wanted = (_area.IsMeasured && FolderLayout.Header(_area.Width, _config.View, Navigates, hasGit: true).Git)
                || (_config.View == FolderViewMode.Details && _config.Columns.Contains(FolderColumn.Git));

            var status = wanted ? await GitStatus.GetAsync(path).ConfigureAwait(true) : null;

            if (_detached || generation != _generation)
            {
                return;
            }

            var changed = !Equals(status?.Summary.Branch, _git?.Summary.Branch) || status?.Summary.Counts() != _git?.Summary.Counts()
                || (status is null) != (_git is null) || !ReferenceEquals(status?.Summary.Files, _git?.Summary.Files);
            _git = status;

            if (changed)
            {
                ApplyHeader();
                RefreshGitCells();
            }
        }
        catch (Exception ex)
        {
            // async void: nothing may escape to the dispatcher.
            Themia.Core.Diagnostics.Log.Error(Scope, "the git badge could not be refreshed", ex);
        }
    }

    /// <summary>[S-1333] Watch the folder on screen; the old watcher goes exactly once.</summary>
    private void Watch(string path)
    {
        // Recorded first: a tick that fires while this runs must not start a second watcher.
        _watchedPath = path;
        var wasWatching = _watch?.Watching ?? true;
        _watch?.Dispose();
        _watch = null;

        try
        {
            _watch = FolderWatch.Start(path, () => Reload(force: false));
        }
        catch (InvalidOperationException ex)
        {
            Themia.Core.Diagnostics.Log.Warn(Scope, "no dispatcher to watch the folder on; the timer refreshes it", ex);
        }

        // The timer's cadence follows whether notifications arrive. Posted, not called: a new
        // subscription ticks at once, and that tick must not run inside the load that is starting.
        if ((_watch?.Watching ?? false) != wasWatching)
        {
            DispatcherQueue?.TryEnqueue(() =>
            {
                if (!_detached)
                {
                    Resubscribe();
                }
            });
        }
    }

    private void SetBusy(bool busy)
    {
        if (_detached)
        {
            return;
        }

        _busy.IsActive = busy;
        _busy.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnClipboardChanged()
    {
        if (!_detached)
        {
            RefreshDims();
        }
    }

    /// <summary>
    /// The card's title names the folder on screen (in navigate mode, where the widget has gone);
    /// the user's own label still wins (the card decides).
    /// </summary>
    private void PublishTitle()
    {
        var title = Navigates && _history.Current.Length > 0
            ? FolderPath.DisplayName(_history.Current)
            : FolderPath.DisplayName(_config.Path);

        _surface?.SetHeaderTitle(string.IsNullOrWhiteSpace(title) ? null : title);
    }

    /// <summary>Change the config from the widget itself (a menu, a header button) and save it.</summary>
    private void Edit(Action<FolderConfig> change)
    {
        change(_config);
        _applied = (FolderConfig)_config.Clone();
        _surface?.RequestSave();
        Lay();
        Render();
    }

    /// <summary>The window this widget is drawn in, for shell UI that wants an owner.</summary>
    private nint Window()
    {
        if (XamlRoot?.ContentIslandEnvironment is { } environment)
        {
            return Microsoft.UI.Win32Interop.GetWindowFromWindowId(environment.AppWindowId);
        }

        return 0;
    }

    /// <summary>Whether Shift is held now (for Shift+arrows, which the lease delivers as plain arrows).</summary>
    private static bool ShiftDown()
        => Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

    private static bool CtrlDown()
        => Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

    /// <inheritdoc />
    public FrameworkElement? HeaderTrailing => _trailing;

    /// <summary>A view of nothing but chrome taps (no keys of its own) still clears its own handlers on Detach.</summary>
    private void DetachInput()
    {
        _scrollContent.PointerPressed -= OnPointerPressed;
        _scrollContent.PointerMoved -= OnPointerMoved;
        _scrollContent.PointerReleased -= OnPointerReleased;
        _scrollContent.PointerCanceled -= OnPointerCanceled;
        _scrollContent.PointerCaptureLost -= OnPointerCanceled;
        _scrollContent.RightTapped -= OnBackgroundRightTapped;
        _scrollContent.DoubleTapped -= OnDoubleTapped;
        _scrollContent.PointerEntered -= OnPointerEntered;
    }

    private void OnPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        // A cut made somewhere else may have ended since the dims were drawn (another app copied).
        RefreshDims();
    }
}
