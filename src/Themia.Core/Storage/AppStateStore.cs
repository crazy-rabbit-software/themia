using System.Text.Json;
using System.Text.Json.Nodes;
using Themia.Core.Diagnostics;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.Core.Storage;

/// <summary>How a load ended. "Absent" and "corrupt" are deliberately different outcomes.</summary>
public enum LoadOutcome
{
    /// <summary>No file yet — a fresh profile. Seeded defaults, nothing was lost.</summary>
    Fresh,

    /// <summary>Read and validated.</summary>
    Loaded,

    /// <summary>
    /// A file existed but could not be trusted. Defaults are returned and the original is moved
    /// aside (never deleted) so the user can recover it.
    /// </summary>
    Recovered,
}

public readonly record struct LoadResult(AppState State, LoadOutcome Outcome, string? Detail);

/// <summary>
/// A document read through <see cref="AppStateStore.Parse"/>: a validated state with the repairs it
/// needed, or no state and the reason it was refused. Never a half-built state.
/// </summary>
public readonly record struct ParseResult(
    AppState? State,
    ParseRefusal Refusal,
    string? Problem,
    Exception? Error,
    IReadOnlyList<string> Repairs)
{
    public static ParseResult Refused(ParseRefusal refusal, string problem, Exception? error = null)
        => new(null, refusal, problem, error, []);
}

/// <summary>Why a document was refused. A closed set, so a caller can say it in its own words.</summary>
public enum ParseRefusal
{
    /// <summary>Not refused.</summary>
    None = 0,

    /// <summary>Empty or whitespace — corrupt, not absent.</summary>
    Empty = 1,

    /// <summary>Not JSON at all.</summary>
    NotJson = 2,

    /// <summary>JSON, but not the shape of a state document.</summary>
    NotState = 3,

    /// <summary>Written by a newer build than this one.</summary>
    NewerSchema = 4,

    /// <summary>The file itself could not be read.</summary>
    Unreadable = 5,
}

/// <summary>
/// Single-writer, atomic persistence for <see cref="AppState"/>.
/// <para>
/// Writes go to a sibling temp file and are then moved over the target, so a crash mid-write can
/// never leave a truncated document. Saves are serialised through one gate and coalesced, so a
/// drag that mutates geometry sixty times a second still produces one write.
/// </para>
/// </summary>
public sealed class AppStateStore : IAsyncDisposable
{
    private const string Scope = "store";

    private readonly string _path;
    private readonly TimeSpan _debounce;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly Lock _pendingGate = new();

    private AppState? _pending;
    private CancellationTokenSource? _debounceCts;
    private bool _disposed;

    /// <summary>False for a <see cref="Detached"/> store, which accepts saves and writes nothing.</summary>
    private readonly bool _persists;

    public AppStateStore(string path, TimeSpan? debounce = null)
        : this(path, debounce, persists: true)
    {
    }

    private AppStateStore(string path, TimeSpan? debounce, bool persists)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
        _debounce = debounce ?? TimeSpan.FromMilliseconds(400);
        _persists = persists;
    }

    /// <summary>
    /// A store that names <paramref name="path"/> but never writes to it: saves are accepted and
    /// dropped.
    /// <para>
    /// For a host built only to be looked at — the debug settings render — where the pages must be
    /// able to call the ordinary save path without the render ever touching the user's real state
    /// file. A flag on the store rather than a second host, so the pages run exactly the code they
    /// run in the app.
    /// </para>
    /// </summary>
    public static AppStateStore Detached(string path) => new(path, TimeSpan.Zero, persists: false);

    public string Path => _path;

    /// <summary>False for a <see cref="Detached"/> store: whatever is built over it must not touch the disk either ([S-2601]).</summary>
    public bool Persists => _persists;

    /// <summary>
    /// The folder name the app keeps its files under, in both %APPDATA% (settings, accounts) and
    /// %LOCALAPPDATA% (caches, the browser profile). One name for every place, so they cannot drift
    /// apart again.
    /// </summary>
    public const string FolderName = "Themia";

    /// <summary>The default on-disk location: %APPDATA%\Themia\state.json.</summary>
    public static string DefaultPath()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return System.IO.Path.Combine(root, FolderName, "state.json");
    }

    /// <summary>
    /// Read and validate. Never throws for bad data: a corrupt document is set aside and defaults
    /// are returned, loudly. The caller can tell that apart from a fresh profile via the outcome.
    /// </summary>
    public LoadResult Load()
    {
        string json;
        try
        {
            if (!File.Exists(_path))
            {
                Log.Info(Scope, $"no state at {_path}; seeding defaults");
                return new LoadResult(AppState.CreateDefault(), LoadOutcome.Fresh, null);
            }

            json = File.ReadAllText(_path);
        }
        catch (IOException ex)
        {
            return Recover("state file could not be read", ex, setAside: false);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Recover("state file could not be read", ex, setAside: false);
        }

        var parsed = Parse(json);
        if (parsed.State is not { } state)
        {
            return Recover(parsed.Problem ?? "state file could not be read", parsed.Error, setAside: true);
        }

        if (parsed.Repairs.Count > 0)
        {
            Log.Warn(Scope, $"state loaded with {parsed.Repairs.Count} repair(s): {string.Join("; ", parsed.Repairs)}");
        }

        return new LoadResult(state, LoadOutcome.Loaded, parsed.Repairs.Count > 0 ? string.Join("; ", parsed.Repairs) : null);
    }

    /// <summary>
    /// Turn a document into a validated state, or say why it cannot be one. The one trust boundary
    /// for a state document, whether it is the app's own file at startup or a file the user picked
    /// to import: both are read the same way, so an import can never accept something a restart
    /// would then refuse.
    /// <para>
    /// Empty, unparseable, null and newer-schema documents are refused with a reason. Anything else
    /// is repaired back inside its invariants (<see cref="Validate"/>) and the repairs are listed.
    /// </para>
    /// </summary>
    public static ParseResult Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            // A zero-byte file is the classic result of a crash during a non-atomic write. It is
            // corrupt, not absent.
            return ParseResult.Refused(ParseRefusal.Empty, "state file is empty");
        }

        JsonNode? document;
        try
        {
            document = JsonNode.Parse(json);
        }
        catch (JsonException ex)
        {
            return ParseResult.Refused(ParseRefusal.NotJson, "state file is not valid JSON", ex);
        }

        var dropped = DropUnknownKinds(document);

        AppState? parsed;
        try
        {
            parsed = document is null ? null : document.Deserialize(AppStateJsonContext.Default.AppState);
        }
        catch (JsonException ex)
        {
            return ParseResult.Refused(ParseRefusal.NotJson, "state file is not valid JSON", ex);
        }
        catch (NotSupportedException ex)
        {
            return ParseResult.Refused(ParseRefusal.NotState, "state file holds an unsupported shape", ex);
        }

        if (parsed is null)
        {
            return ParseResult.Refused(ParseRefusal.NotState, "state file deserialised to null");
        }

        if (parsed.SchemaVersion > AppState.CurrentSchemaVersion)
        {
            return ParseResult.Refused(
                ParseRefusal.NewerSchema,
                $"state schema v{parsed.SchemaVersion} is newer than this build (v{AppState.CurrentSchemaVersion})");
        }

        // Only the current schema is read: there is no upgrade path, so an older number is a file
        // this build cannot vouch for, refused like any other that is not a state document.
        if (parsed.SchemaVersion < AppState.CurrentSchemaVersion)
        {
            return ParseResult.Refused(
                ParseRefusal.NotState,
                $"state schema v{parsed.SchemaVersion} is not supported (this build reads v{AppState.CurrentSchemaVersion})");
        }

        dropped.AddRange(Validate(parsed));
        return new ParseResult(parsed, ParseRefusal.None, null, null, dropped);
    }

    /// <summary>
    /// [S-1900] Take out, before the typed read, every widget whose kind this build does not have —
    /// a widget that was removed from the app, or a hand edit — and say which. The typed read would
    /// otherwise refuse the whole document over one widget it cannot name, and the user would lose
    /// every other widget with it. What such a widget held goes with it; nothing else is touched.
    /// </summary>
    private static List<string> DropUnknownKinds(JsonNode? document)
    {
        var dropped = new List<string>();
        if (document is not JsonObject root || root["views"] is not JsonArray views)
        {
            return dropped;
        }

        foreach (var view in views)
        {
            if (view is not JsonObject { } entry || entry["widgets"] is not JsonArray widgets)
            {
                continue;
            }

            var unknown = new List<JsonNode>();
            foreach (var widget in widgets)
            {
                // Only a kind given by name can be unknown here; a number is checked after the read.
                if (widget is JsonObject fields
                    && fields["kind"] is JsonValue kind
                    && kind.TryGetValue<string>(out var name)
                    && !IsKindName(name))
                {
                    unknown.Add(widget);
                    var id = fields["id"] is JsonValue idValue && idValue.TryGetValue<string>(out var text) ? text : "?";
                    dropped.Add($"dropped widget '{id}' of unknown kind '{name}'");
                }
            }

            foreach (var widget in unknown)
            {
                widgets.Remove(widget);
            }
        }

        return dropped;
    }

    private static bool IsKindName(string name)
        => Array.Exists(Enum.GetNames<WidgetKind>(), known => string.Equals(known, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Bring a deserialised document back inside its invariants, reporting what had to change.
    /// Pure and total: it repairs rather than rejects, because a half-valid layout is still the
    /// user's layout.
    /// </summary>
    public static List<string> Validate(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var repairs = new List<string>();

        state.Design ??= new DesignState();
        state.Views ??= [];
        state.MonitorViews ??= [];

        var design = state.Design;
        design.WidgetGapPx = Math.Clamp(Sanitise(design.WidgetGapPx, 8), 0, 64);
        design.CornerRadiusPx = Math.Clamp(Sanitise(design.CornerRadiusPx, DesignState.DefaultCornerRadiusPx), 0, 32);
        design.UiScale = Math.Clamp(Sanitise(design.UiScale, 1), 0.5, 3.0);
        design.FontScale = Math.Clamp(Sanitise(design.FontScale, 1), 0.5, 2.0);
        design.BorderWidthPx = Math.Clamp(Sanitise(design.BorderWidthPx, 1), 0, 8);
        design.BorderOpacity = Math.Clamp(Sanitise(design.BorderOpacity, 1), 0, 1);
        design.IntroAnimationMs = Motion.IntroMotion.ClampMs(Sanitise(design.IntroAnimationMs, Motion.IntroMotion.DefaultMs));

        // A malformed colour becomes empty, which means "use the theme's own" - the one value that
        // is always renderable. Keeping the bad string would leave the widget with no fill at all.
        design.PrimaryColor = Widgets.HexColor.Normalise(design.PrimaryColor);
        design.BorderColor = Widgets.HexColor.Normalise(design.BorderColor);

        // [S-6561] The two fill choices: an unknown kind or a colour that is not one is the default,
        // the opacity 0..1, loudly. A missing one is the default, silently.
        design.Background ??= new FillChoice();
        repairs.AddRange(design.Background.Repair("widget background"));
        design.ItemBackground ??= new FillChoice();
        repairs.AddRange(design.ItemBackground.Repair("item background"));

        // [S-6543] The item text colour: a colour or empty (the theme's text colours), loudly.
        var (itemText, itemTextRepair) = Widgets.ItemLook.RepairTextColour(design.ItemTextColor);
        design.ItemTextColor = itemText;
        if (itemTextRepair is not null)
        {
            repairs.Add(itemTextRepair);
        }

        if (string.IsNullOrWhiteSpace(design.FontFamily))
        {
            design.FontFamily = "system";
        }

        if (!Enum.IsDefined(design.BorderStyle))
        {
            design.BorderStyle = Model.WidgetBorderStyle.Solid;
        }

        if (!Enum.IsDefined(design.IntroAnimation))
        {
            design.IntroAnimation = Model.IntroAnimation.Scale;
        }

        if (!Enum.IsDefined(design.Theme))
        {
            design.Theme = Model.AppTheme.System;
        }

        // [S-704] Header fill and the transition/unlock animations, on the same terms as the rest.
        design.HeaderBackgroundColor = Widgets.HexColor.Normalise(design.HeaderBackgroundColor);
        design.HeaderOpacity = Math.Clamp(Sanitise(design.HeaderOpacity, 1), 0, 1);
        design.TransitionAnimationMs = Motion.IntroMotion.ClampMs(Sanitise(design.TransitionAnimationMs, Motion.IntroMotion.DefaultMs));
        design.UnlockAnimationMs = Motion.IntroMotion.ClampMs(Sanitise(design.UnlockAnimationMs, Motion.IntroMotion.DefaultMs));

        if (!Enum.IsDefined(design.TransitionAnimation))
        {
            design.TransitionAnimation = Model.IntroAnimation.Scale;
        }

        if (!Enum.IsDefined(design.UnlockAnimation))
        {
            design.UnlockAnimation = Model.IntroAnimation.Scale;
        }

        repairs.AddRange(ValidatePreferences(state));

        if (state.Views.Count == 0)
        {
            state.Views.Add(ViewState.Create("Desktop"));
            repairs.Add("no views; seeded one");
        }

        var seenViewIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var view in state.Views)
        {
            if (string.IsNullOrWhiteSpace(view.Id) || !seenViewIds.Add(view.Id))
            {
                view.Id = Guid.NewGuid().ToString("n");
                seenViewIds.Add(view.Id);
                repairs.Add("view had a missing or duplicate id");
            }

            if (string.IsNullOrWhiteSpace(view.Name))
            {
                view.Name = "View";
                repairs.Add("view had no name");
            }

            if (view.Icon is not null && !IsGlyph(view.Icon))
            {
                // [S-703] Fall back to initials rather than draw whatever text got onto disk.
                view.Icon = null;
                repairs.Add($"view '{view.Id}' had an icon that is not a single glyph");
            }

            // [S-3811] A colour that does not parse draws the icon in the theme's colour, loudly.
            if (ViewOps.CleanColor(view.Color) is var color && color != view.Color)
            {
                if (color is null && !string.IsNullOrWhiteSpace(view.Color))
                {
                    repairs.Add($"view '{view.Id}' had a colour that is not #RRGGBB; it uses the theme's");
                }

                view.Color = color;
            }

            view.Widgets ??= [];
            repairs.AddRange(ValidateWidgets(view));
        }

        if (state.FindView(state.ActiveViewId) is null)
        {
            state.ActiveViewId = state.Views[0].Id;
            repairs.Add("active view id was dangling");
        }

        foreach (var monitorId in state.MonitorViews.Keys.ToList())
        {
            if (state.FindView(state.MonitorViews[monitorId]) is null)
            {
                state.MonitorViews.Remove(monitorId);
                repairs.Add($"monitor '{monitorId}' pointed at a deleted view");
            }
        }

        repairs.AddRange(ValidateDisplays(state));

        state.SchemaVersion = AppState.CurrentSchemaVersion;
        return repairs;
    }

    /// <summary>[S-702] The behaviour switches: the language, the regional formats and the update cadences can be out of range.</summary>
    private static List<string> ValidatePreferences(AppState state)
    {
        var repairs = new List<string>();

        if (state.Preferences is null)
        {
            // A hand-edited null: repair to the defaults, loudly.
            state.Preferences = new AppPreferences();
            repairs.Add("preferences were null; reset to defaults");
            return repairs;
        }

        if (!Enum.IsDefined(state.Preferences.Language))
        {
            state.Preferences.Language = AppLanguage.System;
            repairs.Add("unknown language; following Windows");
        }

        // [S-3772] [S-3781] The shared formats and cadences: a hand-edited null is the defaults.
        if (state.Preferences.Region is null)
        {
            state.Preferences.Region = new RegionPreferences();
            repairs.Add("region formats were null; following Windows");
        }

        if (state.Preferences.Refresh is null)
        {
            state.Preferences.Refresh = new RefreshPreferences();
            repairs.Add("refresh cadences were null; reset to defaults");
        }

        repairs.AddRange(state.Preferences.Region.Repair());
        repairs.AddRange(Sync.UpdateCadence.Repair(state.Preferences.Refresh));
        return repairs;
    }

    /// <summary>
    /// [S-705] The per-display settings. Runs after the views are repaired, because the default view
    /// for new displays has to name one that still exists.
    /// </summary>
    private static List<string> ValidateDisplays(AppState state)
    {
        var repairs = new List<string>();

        if (state.DefaultViewForNewDisplays is not null && state.FindView(state.DefaultViewForNewDisplays) is null)
        {
            state.DefaultViewForNewDisplays = null;
            repairs.Add("the default view for new displays was deleted; new displays show the active view");
        }

        state.DisabledMonitors ??= [];
        var disabled = state.DisabledMonitors
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (disabled.Count != state.DisabledMonitors.Count)
        {
            repairs.Add("the disabled displays held blank or repeated ids");
        }

        state.DisabledMonitors = disabled;

        state.MonitorScales ??= [];
        foreach (var monitorId in state.MonitorScales.Keys.ToList())
        {
            var scale = state.MonitorScales[monitorId];
            if (string.IsNullOrWhiteSpace(monitorId) || !double.IsFinite(scale))
            {
                state.MonitorScales.Remove(monitorId);
                repairs.Add("dropped an unreadable display scale");
                continue;
            }

            var clamped = Math.Clamp(scale, MinMonitorScale, MaxMonitorScale);
            if (clamped != scale)
            {
                state.MonitorScales[monitorId] = clamped;
                repairs.Add($"display scale for '{monitorId}' was outside {MinMonitorScale:0.#}–{MaxMonitorScale:0.#}");
            }
        }

        repairs.AddRange(ValidateViewBars(state));
        return repairs;
    }

    /// <summary>
    /// [S-906] The per-display view bars: unreadable entries are dropped (the display falls back to
    /// the defaults), unknown anchors, sizes and styles reset, and each whitelist keeps only views that
    /// exist, once each. A whitelist that ends up empty means "all views", never an empty bar.
    /// </summary>
    private static List<string> ValidateViewBars(AppState state)
    {
        var repairs = new List<string>();
        state.ViewBars ??= [];

        foreach (var monitorId in state.ViewBars.Keys.ToList())
        {
            var bar = state.ViewBars[monitorId];
            if (string.IsNullOrWhiteSpace(monitorId) || bar is null)
            {
                state.ViewBars.Remove(monitorId);
                repairs.Add("dropped an unreadable view bar entry");
                continue;
            }

            if (!Enum.IsDefined(bar.Anchor))
            {
                bar.Anchor = ViewBarAnchor.BottomLeft;
                repairs.Add($"view bar on '{monitorId}' had an unknown position");
            }

            if (!Enum.IsDefined(bar.Size))
            {
                bar.Size = ViewBarSize.Medium;
                repairs.Add($"view bar on '{monitorId}' had an unknown size");
            }

            if (!Enum.IsDefined(bar.Style))
            {
                // [S-5400] Chips, the bar as it always was.
                bar.Style = ViewBarStyle.Chips;
                repairs.Add($"view bar on '{monitorId}' had an unknown style");
            }

            var visible = (bar.VisibleViews ?? [])
                .Where(id => state.FindView(id) is not null)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (bar.VisibleViews is null || visible.Count != bar.VisibleViews.Count)
            {
                repairs.Add($"view bar on '{monitorId}' listed missing or repeated views");
            }

            bar.VisibleViews = visible;
        }

        return repairs;
    }

    /// <summary>The per-display scale range, as in the original's display settings (50 % to 200 %).</summary>
    public const double MinMonitorScale = 0.5;

    public const double MaxMonitorScale = 2.0;

    /// <summary>One visible glyph: a single UTF-16 unit, or one surrogate pair.</summary>
    private static bool IsGlyph(string text)
        => text.Length == 1 ? !char.IsWhiteSpace(text[0]) && !char.IsControl(text[0])
            : text.Length == 2 && char.IsSurrogatePair(text[0], text[1]);

    private static List<string> ValidateWidgets(ViewState view)
    {
        var repairs = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        // Geometry is validated against a unit screen: the stored model is fractional, so the
        // invariants (on-screen, non-inverted, finite) hold independently of any real monitor.
        var unit = new ScreenMetrics(1920, 1080);

        for (var i = view.Widgets.Count - 1; i >= 0; i--)
        {
            var widget = view.Widgets[i];

            if (!Enum.IsDefined(widget.Kind))
            {
                // An unknown kind is data this build cannot render at all. Dropping the instance is
                // the only honest option, and it is reported rather than silently swallowed.
                view.Widgets.RemoveAt(i);
                repairs.Add($"dropped widget of unknown kind '{(int)widget.Kind}'");
                continue;
            }

            if (string.IsNullOrWhiteSpace(widget.Id) || !seen.Add(widget.Id))
            {
                widget.Id = Guid.NewGuid().ToString("n");
                seen.Add(widget.Id);
                repairs.Add("widget had a missing or duplicate id");
            }

            if (widget.Config is null || widget.Config.Kind != widget.Kind)
            {
                widget.Config = WidgetConfig.CreateDefault(widget.Kind);
                repairs.Add($"widget '{widget.Id}' had a missing or mismatched config");
            }

            // The config's own invariants: ranges and values a hand edit made unusable.
            foreach (var repair in widget.Config.Repair())
            {
                repairs.Add($"widget '{widget.Id}': {repair}");
            }

            // [S-6004] A style the kind does not have: its first style. One rule for every styled kind.
            if (WidgetStyles.Repair(widget.Config) is { } style)
            {
                repairs.Add($"widget '{widget.Id}': {style}");
            }

            // [S-6561] A widget's own fill choices: repaired like the app-wide ones; absent follows them.
            widget.Background ??= new FillChoice();
            widget.ItemBackground ??= new FillChoice();
            foreach (var repair in widget.Background.Repair("background").Concat(widget.ItemBackground.Repair("item background")))
            {
                repairs.Add($"widget '{widget.Id}': {repair}");
            }

            // [S-6543] A widget's own item text colour: a colour, or empty to follow the app-wide one.
            var (ownText, ownTextRepair) = ItemLook.RepairTextColour(widget.ItemTextColor);
            widget.ItemTextColor = ownText;
            if (ownTextRepair is not null)
            {
                repairs.Add($"widget '{widget.Id}': {ownTextRepair}");
            }

            // [S-6533] A scale outside 50–200 % or off the 5 % grain: the nearest one it may hold.
            if (WidgetScale.Repair(widget) is { } scale)
            {
                repairs.Add($"widget '{widget.Id}': {scale}");
            }

            // [S-6585] An alignment outside the set, or on a kind with nothing to align: the kind's default.
            if (ContentAlignment.Repair(widget) is { } align)
            {
                repairs.Add($"widget '{widget.Id}': {align}");
            }

            if (!Enum.IsDefined(widget.Header))
            {
                widget.Header = HeaderPlacement.Top;
                repairs.Add($"widget '{widget.Id}' had an unknown header placement");
            }

            // [S-3711] A kind that can never have a header is stored without one.
            if (!WidgetInstance.CanHaveHeader(widget.Kind) && widget.Header != HeaderPlacement.Hidden)
            {
                widget.Header = HeaderPlacement.Hidden;
                repairs.Add($"widget '{widget.Id}' ({widget.Kind}) cannot have a header");
            }

            var settled = GridGeometry.Clamp(widget.Rect, unit);
            if (settled != widget.Rect)
            {
                widget.Rect = settled;
                repairs.Add($"widget '{widget.Id}' was off-screen or below the size floor");
            }
        }

        return repairs;
    }

    private static double Sanitise(double value, double fallback)
        => double.IsFinite(value) ? value : fallback;

    private LoadResult Recover(string reason, Exception? error, bool setAside)
    {
        Log.Error(Scope, $"{reason}; falling back to defaults", error);

        if (setAside)
        {
            // Never delete the user's data on a recoverable error — move it where they can find it.
            var aside = $"{_path}.corrupt-{DateTime.Now:yyyyMMdd-HHmmss}";
            try
            {
                File.Move(_path, aside, overwrite: true);
                Log.Warn(Scope, $"previous state preserved at {aside}");
            }
            catch (IOException ex)
            {
                Log.Error(Scope, "could not set aside the corrupt state file", ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                Log.Error(Scope, "could not set aside the corrupt state file", ex);
            }
        }

        return new LoadResult(AppState.CreateDefault(), LoadOutcome.Recovered, reason);
    }

    /// <summary>
    /// Queue a save. Repeated calls inside the debounce window collapse into one write of the
    /// latest snapshot, so a live drag does not hammer the disk.
    /// </summary>
    public void SaveSoon(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        ObjectDisposedException.ThrowIf(_disposed, this);

        CancellationTokenSource cts;
        lock (_pendingGate)
        {
            _pending = state;
            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            _debounceCts = new CancellationTokenSource();
            cts = _debounceCts;
        }

        _ = DebouncedSaveAsync(cts.Token);
    }

    private async Task DebouncedSaveAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(_debounce, token).ConfigureAwait(false);
        }
        catch (TaskCanceledException)
        {
            return; // superseded by a newer SaveSoon; that call owns the write
        }

        try
        {
            await FlushAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // This runs detached from any caller, so an escaping exception would be lost entirely.
            // WriteAtomicAsync already handles the expected I/O failures; anything reaching here is
            // unexpected and must still be visible rather than vanish into an unobserved task.
            Log.Error(Scope, "background save failed", ex);
        }
    }

    /// <summary>
    /// Write any queued snapshot now and wait for it. Safe to call when nothing is pending.
    /// <para>
    /// The write gate is taken <i>before</i> the pending snapshot is read, so a flush that finds
    /// nothing queued still waits for an in-flight write to land. Reading the snapshot first would
    /// let this return while a debounced save was mid-write, i.e. "flushed" without durability —
    /// exactly the window a shutdown flush exists to close.
    /// </para>
    /// </summary>
    public async Task FlushAsync()
    {
        await _writeGate.WaitAsync().ConfigureAwait(false);
        try
        {
            AppState? snapshot;
            lock (_pendingGate)
            {
                snapshot = _pending;
                _pending = null;
            }

            if (snapshot is null || !_persists)
            {
                return;
            }

            await WriteAtomicAsync(snapshot).ConfigureAwait(false);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    private async Task WriteAtomicAsync(AppState state)
    {
        var temp = $"{_path}.tmp";
        try
        {
            var dir = System.IO.Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(state, AppStateJsonContext.Default.AppState);
            await File.WriteAllTextAsync(temp, json).ConfigureAwait(false);

            // Atomic swap: readers see either the old document or the new one, never a partial write.
            AtomicFile.Replace(temp, _path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            Log.Error(Scope, $"could not save state to {_path}", ex);

            try
            {
                if (File.Exists(temp))
                {
                    File.Delete(temp);
                }
            }
            catch (IOException cleanupEx)
            {
                Log.Warn(Scope, "could not remove the temp state file", cleanupEx);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        lock (_pendingGate)
        {
            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            _debounceCts = null;
        }

        await FlushAsync().ConfigureAwait(false);
        _writeGate.Dispose();
    }
}
