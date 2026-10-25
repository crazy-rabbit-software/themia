#if DEBUG
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.App.Interop;
using Themia.App.Services;
using Themia.App.Shell.Config;
using Themia.Core.Diagnostics;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Sdk;
using Windows.Graphics;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// The debug-only settings render:
/// <c>Themia.exe --settings-png &lt;Page&gt; &lt;out.png&gt; [waitMs] [--theme light|dark] [--search text] [--land text]
/// [--flyout row-title] [--width dips]</c>
/// draws the real settings window — title bar, navigation, breadcrumb and page — off-screen to a
/// PNG, then exits. [S-499], [S-1548]
/// <para>
/// The settings window drawn off-screen, so a page can be looked at without running
/// the app — no state file is read, no single-instance hand-off happens, and nothing is written:
/// the host is built over a demo state and a <see cref="AppStateStore.Detached"/> store, so a page
/// that saves as it is built still never touches the user's real state.
/// </para>
/// <para>
/// The window is made as tall as the page, so rows below the fold are in the picture too. Mica
/// cannot be captured, so the render paints the colour Mica falls back to. <c>--theme</c> picks
/// light or dark (the app's own theme is set from it at start-up, see <c>App</c>);
/// <c>--search</c> types into "Find a setting" and also writes the open suggestion list to
/// <c>&lt;out&gt;-search.png</c>; <c>--land</c> goes to the best match and rings it, as choosing a
/// suggestion does.
/// </para>
/// </summary>
internal static class SettingsRender
{
    private const string Scope = "settings-render";

    /// <summary>The window's width: wide enough that the navigation pane stays open, as by default.</summary>
    private const double WindowWidth = 1100;

    /// <summary>
    /// The <c>--theme</c> a render asked for, read before the app's resources load, because the
    /// application theme can only be set then. Null when not rendering or not asked.
    /// </summary>
    public static ApplicationTheme? RequestedTheme(string[] args)
    {
        if (Array.IndexOf(args, "--settings-png") < 0)
        {
            return null;
        }

        return Option(args, "--theme")?.ToLowerInvariant() switch
        {
            "light" => ApplicationTheme.Light,
            "dark" => ApplicationTheme.Dark,
            _ => null,
        };
    }

    /// <summary>
    /// Handle <c>--settings-png</c>. Returns false when the arguments are not for it, so the caller
    /// starts the app normally.
    /// </summary>
    public static bool TryRunFromCommandLine(Application app, string[] args)
    {
        var at = Array.IndexOf(args, "--settings-png");
        if (at < 0)
        {
            return false;
        }

        if (at + 2 >= args.Length)
        {
            Log.Error(Scope, "usage: --settings-png <Page> <out.png> [waitMs] [--theme light|dark] [--search text] [--land text] [--flyout row-title] [--width dips]");
            app.Exit();
            return true;
        }

        var name = args[at + 1];
        var outPath = Path.GetFullPath(args[at + 2]);
        var wait = at + 3 < args.Length && int.TryParse(args[at + 3], out var ms) ? ms : 2000;

        // A widget has no page here: its options are on its properties menu (--editbar-png draws it).
        if (Resolve(name) is not { } page)
        {
            Log.Error(Scope, $"unknown settings page '{name}'");
            Console.Error.WriteLine($"settings: unknown page '{name}'");
            app.Exit();
            return true;
        }

        if (DispatcherQueue.GetForCurrentThread() is not { } dispatcher)
        {
            Log.Error(Scope, "no dispatcher on this thread; cannot render");
            app.Exit();
            return true;
        }

        var request = new Request(page, outPath, wait, Option(args, "--search"), Option(args, "--land"),
            RequestedTheme(args) switch
            {
                ApplicationTheme.Light => AppTheme.Light,
                ApplicationTheme.Dark => AppTheme.Dark,
                _ => AppTheme.System,
            },
            Option(args, "--flyout"),
            double.TryParse(Option(args, "--width"), System.Globalization.CultureInfo.InvariantCulture, out var width) && width >= 320 ? width : WindowWidth);

        _ = RenderAsync(app, dispatcher, request);
        return true;
    }

    private sealed record Request(
        ConfigTarget Page,
        string OutPath,
        int WaitMs,
        string? Search,
        string? Land,
        AppTheme Theme,
        string? Flyout,
        double Width);

    private static string? Option(string[] args, string name)
    {
        var at = Array.IndexOf(args, name);
        return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
    }

    /// <summary>
    /// A page name to its target. The app pages are the <see cref="AppPage"/> members by name, so a
    /// page added there renders without a change here. [S-3584] None is left: the licence's states
    /// are drawn by <c>--editbar-png</c> (⚙ › Themia Pro).
    /// </summary>
    private static ConfigTarget? Resolve(string name)
        => Enum.TryParse<AppPage>(name, ignoreCase: true, out var page) && Enum.IsDefined(page)
            ? new ConfigTarget.App(page)
            : null;

    private static async Task RenderAsync(Application app, DispatcherQueue dispatcher, Request request)
    {
        AppHost? host = null;
        ConfigWindow? settings = null;

        try
        {
            // A path of its own under %TEMP%, not the real one: the host keeps other files beside
            // the state file (accounts, screen time), and those must not be the user's either.
            var statePath = Path.Combine(Path.GetTempPath(), "themia-settings-render", "state.json");
            var state = DemoState();
            state.Design.Theme = request.Theme;
            // [S-1109] --lang: the host applies the state's language, so the demo state carries it.
            if (RenderLanguage.Setting(Environment.GetCommandLineArgs()) is { } language)
            {
                state.Preferences.Language = language;
            }

            host = new AppHost(dispatcher, AppStateStore.Detached(statePath), state);

            var target = request.Page;

            settings = new ConfigWindow(host);
            var window = settings.DebugWindow;

            // Off-screen and never activated, like every debug render: it must not take the
            // user's focus or appear on their desktop.
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            var dpi = Win32.GetDpiForWindow(hwnd);
            var scale = dpi > 0 ? dpi / 96.0 : 1.0;

            window.AppWindow.Move(new PointInt32(-32000, -32000));
            window.AppWindow.Resize(new SizeInt32((int)(request.Width * scale), (int)(800 * scale)));
            window.AppWindow.Show(activateWindow: false);

            var root = settings.DebugRoot;
            if (root is Panel panel)
            {
                // What Mica falls back to; the backdrop itself is not in a RenderTargetBitmap.
                panel.Background = ThemeBrush.Of("SolidBackgroundFillColorBaseBrush", Colors.Black);
            }

            settings.DebugShow(target);

            if (request.Land is { } land)
            {
                settings.DebugLand(land);
            }

            // Let the first layout happen, then make the window as tall as the page.
            await Task.Delay(400).ConfigureAwait(true);
            var pageHeight = settings.DebugPageHeight();
            var height = request.Land is null && request.Search is null && request.Flyout is null
                ? Math.Max(600, pageHeight + 180)
                : 800;

            window.AppWindow.Resize(new SizeInt32((int)(request.Width * scale), (int)(height * scale)));

            if (request.Search is { } query)
            {
                settings.DebugSearch(query);
            }

            if (request.Flyout is { } flyoutRow)
            {
                if (!settings.DebugOpenFlyout(flyoutRow))
                {
                    Log.Warn(Scope, $"no drop-down on a row titled '{flyoutRow}'");
                }
                else if (request.Search is null && request.Land is null)
                {
                    // The panel is drawn into the page: tall enough for the page as it is now.
                    await Task.Delay(400).ConfigureAwait(true);
                    var withPanel = Math.Max(800, settings.DebugPageHeight() + 180);
                    window.AppWindow.Resize(new SizeInt32((int)(request.Width * scale), (int)(withPanel * scale)));
                }
            }

            // Long enough for bitmaps to decode and the expander/scroll animations to settle.
            await Task.Delay(request.WaitMs).ConfigureAwait(true);

            Directory.CreateDirectory(Path.GetDirectoryName(request.OutPath)!);
            await RenderPng.SaveAsync(root, request.OutPath).ConfigureAwait(true);
            Console.WriteLine($"settings: wrote {request.OutPath}");

            if (request.Search is not null || request.Flyout is not null)
            {
                // The suggestion list is a popup, outside the window's tree: drawn on its own.
                foreach (var popup in VisualTreeHelper.GetOpenPopupsForXamlRoot(root.XamlRoot))
                {
                    if (popup.Child is FrameworkElement child)
                    {
                        var path = Path.ChangeExtension(request.OutPath, null)
                            + (request.Search is not null ? "-search.png" : "-flyout.png");
                        await RenderPng.SaveAsync(child, path).ConfigureAwait(true);
                        Console.WriteLine($"settings: wrote {path}");
                        break;
                    }
                }
            }

        }
        catch (Exception ex)
        {
            Log.Error(Scope, "rendering the settings page failed", ex);
        }
        finally
        {
            settings?.Close();
            host?.Dispose();
            app.Exit();
        }
    }

    /// <summary>A believable profile: three views with a few widgets, defaults everywhere else.</summary>
    private static AppState DemoState()
    {
        var desktop = ViewState.Create("Desktop");
        desktop.Widgets.Add(WidgetInstance.Create(new WidgetId("clock"), new FracRect(0.40, 0.08, 0.20, 0.12)));
        desktop.Widgets.Add(WidgetInstance.Create(new WidgetId("weatherNow"), new FracRect(0.05, 0.08, 0.15, 0.18)));

        // [S-903] One view with an icon of its own, so the pane shows a view glyph beside the shared colour icon.
        var work = ViewState.Create("Work");
        work.Icon = "";
        work.Color = "#0099BC";
        work.Widgets.Add(WidgetInstance.Create(new WidgetId("calendar"), new FracRect(0.05, 0.08, 0.20, 0.30)));
        work.Widgets.Add(WidgetInstance.Create(new WidgetId("email"), new FracRect(0.30, 0.08, 0.25, 0.30)));

        var evening = ViewState.Create("Evening");
        evening.Widgets.Add(WidgetInstance.Create(new WidgetId("music"), new FracRect(0.05, 0.70, 0.20, 0.12)));

        return new AppState
        {
            Views = [desktop, work, evening],
            ActiveViewId = desktop.Id,
        };
    }
}
#endif
