#if DEBUG
using Themia.App.Interop;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Geometry;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Storage;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-1814] Debug only: <c>Themia.exe --add-showcase-views [&lt;statePath&gt;]</c> puts one view per
/// widget kind into a state file, each holding the widget at every size tier
/// (<see cref="ShowcaseViews"/>), laid out for the primary display, and exits. No window, no desktop.
/// <para>
/// The state file is read with the app's own trust boundary (<see cref="AppStateStore.Parse"/>); a
/// file that does not parse is left untouched and the command fails, rather than starting over from
/// defaults. Writing the app's own file while a copy is running is refused: that copy owns it and
/// would overwrite the change with its next save.
/// </para>
/// <para>
/// Exit codes: 0 written; 2 usage or no display; 3 another copy is running; 4 the file is unreadable
/// or not a state document; 5 the write did not land.
/// </para>
/// </summary>
internal static class ShowcaseCommand
{
    public const string Flag = "--add-showcase-views";

    private const string Scope = "showcase";

    /// <summary>MONITOR_DEFAULTTOPRIMARY.</summary>
    private const uint DefaultToPrimary = 1;

    /// <summary>MDT_EFFECTIVE_DPI.</summary>
    private const int EffectiveDpi = 0;

    /// <summary>Run the command if <paramref name="args"/> asks for it. Returns the exit code, or null when not asked.</summary>
    public static int? TryRun(string[] args)
    {
        var at = Array.IndexOf(args, Flag);
        if (at < 0)
        {
            return null;
        }

        try
        {
            return Run(at + 1 < args.Length && !args[at + 1].StartsWith("--", StringComparison.Ordinal) ? args[at + 1] : null);
        }
        catch (Exception ex)
        {
            Say($"failed: {ex.Message}");
            Log.Error(Scope, "adding the showcase views failed", ex);
            return 5;
        }
    }

    private static int Run(string? requested)
    {
        var path = Path.GetFullPath(requested ?? AppStateStore.DefaultPath());
        var isAppsOwn = string.Equals(path, Path.GetFullPath(AppStateStore.DefaultPath()), StringComparison.OrdinalIgnoreCase);

        if (isAppsOwn && CommandWindow.IsAnotherInstanceRunning())
        {
            Say($"refused: Themia is running and owns {path}. Quit it first, or pass another state file.");
            return 3;
        }

        AppState state;
        if (File.Exists(path))
        {
            var parsed = AppStateStore.Parse(File.ReadAllText(path));
            if (parsed.State is not { } loaded)
            {
                Say($"refused: {path} is not a state document ({parsed.Refusal}: {parsed.Problem}); left untouched.");
                return 4;
            }

            if (parsed.Repairs.Count > 0)
            {
                Say($"note: the file needed {parsed.Repairs.Count} repair(s): {string.Join("; ", parsed.Repairs)}");
            }

            state = loaded;
        }
        else
        {
            Say($"no state at {path}; starting from the default layout");
            state = AppState.CreateDefault();
        }

        // The views are named in the language the app would show them in.
        Strings.SetLocale(LanguageResolver.Resolve(state.Preferences.Language, SystemLanguages.Preferred()));

        if (PrimaryScreen() is not { } primary)
        {
            Say("failed: no display found");
            return 2;
        }

        var (screen, monitorId) = primary;
        var displayScale = state.MonitorScales.TryGetValue(monitorId, out var own) && double.IsFinite(own) && own > 0 ? own : 1.0;
        var uiScale = double.IsFinite(state.Design.UiScale) && state.Design.UiScale > 0 ? state.Design.UiScale : 1.0;

        var kinds = WidgetCatalog.All
            .Select(d => new ShowcaseKind(d.Kind, d.Label, d.Glyph))
            .ToList();
        var views = ShowcaseViews.Build(kinds, screen, state.Design.WidgetGapPx, displayScale * uiScale);
        var removed = ShowcaseViews.Replace(state, views);

        Write(path, state);

        // The store logs a failed write instead of throwing, so read the file back to know it landed.
        var check = AppStateStore.Parse(File.ReadAllText(path));
        if (check.State is not { } written || views.Any(v => written.FindView(v.Id) is null))
        {
            Say($"failed: {path} does not hold the new views after saving (see the log)");
            return 5;
        }

        Say($"wrote {views.Count} showcase view(s) for {kinds.Count} widget kinds to {path} " +
            $"(replaced {removed}; primary display {screen.Width:0.#} x {screen.Height:0.#} DIPs, content scale {displayScale * uiScale:0.##})");
        return 0;
    }

    /// <summary>Atomic, through the app's own store: a temp file renamed over the target.</summary>
    private static void Write(string path, AppState state)
    {
        var store = new AppStateStore(path, TimeSpan.Zero);
        try
        {
            store.SaveSoon(state);
            store.FlushAsync().GetAwaiter().GetResult();
        }
        finally
        {
            store.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    /// <summary>The primary display in DIPs, with its work area, and its stable id.</summary>
    private static (ScreenMetrics Screen, string Id)? PrimaryScreen()
    {
        var monitors = Monitors.All();
        if (monitors.Count == 0)
        {
            return null;
        }

        var primary = monitors[0];
        var handle = Win32.MonitorFromPoint(new Win32.Point { X = primary.Bounds.Left, Y = primary.Bounds.Top }, DefaultToPrimary);
        var scale = handle != 0 && Win32.GetDpiForMonitor(handle, EffectiveDpi, out var dpi, out _) == 0 && dpi > 0 ? dpi / 96.0 : 1.0;

        var info = primary.Info;
        var screen = new ScreenMetrics(primary.Bounds.Width / scale, primary.Bounds.Height / scale)
        {
            Allowed = GridGeometry.AllowedArea(info.Bounds, info.WorkArea),
        };

        return (screen, primary.Id);
    }

    private static void Say(string message)
    {
        Console.WriteLine($"{Flag}: {message}");
        Log.Info(Scope, message);
    }
}
#endif
