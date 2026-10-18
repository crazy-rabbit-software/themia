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
/// [--flyout row-title] [--width dips] [--open first|name]</c>
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
/// suggestion does. <c>--open</c> opens one group of the Displays or Views page, as a link to one
/// display or view does: <c>first</c>, or the view's or display's name ([S-1835]).
/// </para>
/// </summary>
internal static class SettingsRender
{
    private const string Scope = "settings-render";

    /// <summary>The window's width: wide enough that the navigation pane stays open, as by default.</summary>
    private const double WindowWidth = 1100;

    /// <summary>Set while a render runs, so nothing destructive (uninstall) can happen in one.</summary>
    public static bool IsRendering { get; private set; }

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

        IsRendering = true;

        if (at + 2 >= args.Length)
        {
            Log.Error(Scope, "usage: --settings-png <General|Appearance|Accounts|LanguageRegion|Displays|Views|Widgets|License|About|Widget:<Kind>> <out.png> [waitMs] [--theme light|dark] [--search text] [--land text] [--flyout row-title] [--width dips] [--open first|name] [--style name] [--config json]");
            app.Exit();
            return true;
        }

        var name = args[at + 1];
        var outPath = Path.GetFullPath(args[at + 2]);
        var wait = at + 3 < args.Length && int.TryParse(args[at + 3], out var ms) ? ms : 2000;

        // "Widget:<Kind>" is that kind's settings page, for the demo profile's widget of that kind.
        var widgetKind = name.StartsWith("Widget:", StringComparison.OrdinalIgnoreCase)
            && ModuleCatalog.TryFind(name["Widget:".Length..], out var widget)
                ? widget.Id
                : (WidgetId?)null;

        var page = widgetKind is not null ? PageKind.Widget : PageKind.App;

        if (page == PageKind.App && Resolve(name) is null)
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

        _licenseFixture = Option(args, "--license");

        var request = new Request(page, Resolve(name), widgetKind, outPath, wait, Option(args, "--search"), Option(args, "--land"),
            RequestedTheme(args) switch
            {
                ApplicationTheme.Light => AppTheme.Light,
                ApplicationTheme.Dark => AppTheme.Dark,
                _ => AppTheme.System,
            },
            Option(args, "--flyout"),
            double.TryParse(Option(args, "--width"), System.Globalization.CultureInfo.InvariantCulture, out var width) && width >= 320 ? width : WindowWidth,
            Option(args, "--open"),
            Option(args, "--style"),
            Option(args, "--config"));

        _ = RenderAsync(app, dispatcher, request);
        return true;
    }

    private enum PageKind
    {
        App,
        Widget,
    }

    private sealed record Request(
        PageKind Page,
        ConfigTarget? App,
        WidgetId? Kind,
        string OutPath,
        int WaitMs,
        string? Search,
        string? Land,
        AppTheme Theme,
        string? Flyout,
        double Width,
        string? Open,
        string? Style,
        string? Config);

    private static string? Option(string[] args, string name)
    {
        var at = Array.IndexOf(args, name);
        return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
    }

    /// <summary>
    /// A page name to its target. The app pages are the <see cref="AppPage"/> members by name, so a
    /// page added there — Accounts included — renders without a change here.
    /// </summary>
    /// <summary>[S-1068] <c>--license free|refused|pro|grace|offline</c>: the License page as a packaged copy would show it.</summary>
    private static string? _licenseFixture;

    private static void ApplyLicenseFixture(AppHost host)
    {
        var now = DateTimeOffset.UtcNow;
        Themia.Core.Licensing.LicenseStatus? status = _licenseFixture?.ToLowerInvariant() switch
        {
            "free" or "refused" => new(Themia.Core.Licensing.LicenseTier.Free, Themia.Core.Licensing.LicenseBasis.StoreFree),
            "pro" => new(Themia.Core.Licensing.LicenseTier.Pro, Themia.Core.Licensing.LicenseBasis.StorePro),
            "grace" => new(Themia.Core.Licensing.LicenseTier.Pro, Themia.Core.Licensing.LicenseBasis.OfflineGrace, now.AddDays(5)),
            "offline" => new(Themia.Core.Licensing.LicenseTier.Free, Themia.Core.Licensing.LicenseBasis.StoreUnavailable),
            _ => null,
        };

        if (status is null)
        {
            return;
        }

        host.License.OverrideForRender(status);
        if (_licenseFixture!.Equals("refused", StringComparison.OrdinalIgnoreCase))
        {
            host.LastRefusal = Themia.Core.Licensing.LicenseText.Refusal(
                Themia.Core.Licensing.LicenseUsage.Of(host.State), 0, 1);
        }
    }

    private static ConfigTarget? Resolve(string name)
        => Enum.TryParse<AppPage>(name, ignoreCase: true, out var page) && Enum.IsDefined(page)
            ? new ConfigTarget.App(page)
            : null;

    /// <summary>
    /// [S-1835] <c>--open first|name</c> on the Displays or Views page: the page as a link to one
    /// display or view opens it. Any other page, or no match, renders the page as it opens from the
    /// navigation (logged when a name matched nothing).
    /// </summary>
    private static ConfigTarget WithOpenGroup(AppHost host, ConfigTarget target, string? open)
    {
        if (open is null || target is not ConfigTarget.App app || ConfigTarget.ListOf(app.Page) is not { } list)
        {
            return target;
        }

        var items = list == Themia.Core.Settings.SettingsList.Views
            ? host.State.Views.Select(v => (v.Id, v.Name)).ToList()
            : host.Displays.Select(d => (d.Id, Name: d.ShortName)).ToList();

        var match = open.Equals("first", StringComparison.OrdinalIgnoreCase)
            ? items.FirstOrDefault()
            : items.FirstOrDefault(i => i.Name.Equals(open, StringComparison.OrdinalIgnoreCase));

        if (match.Id is null)
        {
            Log.Warn(Scope, $"--open '{open}' matched nothing on {app.Page}");
            return target;
        }

        return app with { Item = match.Id };
    }

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
            AddManyViews(state, int.TryParse(Option(Environment.GetCommandLineArgs(), "--views"), out var extra) ? extra : 0);
            state.Design.Theme = request.Theme;
            // [S-1109] --lang: the host applies the state's language, so the demo state carries it.
            if (RenderLanguage.Setting(Environment.GetCommandLineArgs()) is { } language)
            {
                state.Preferences.Language = language;
            }

            host = new AppHost(dispatcher, AppStateStore.Detached(statePath), state);
            ApplyLicenseFixture(host);
            DemoAccounts(host, Array.IndexOf(Environment.GetCommandLineArgs(), "--demo-accounts") >= 0);

            // [S-853] The Displays page lists the connected displays. Reading the list touches no
            // window; no surface is built, so the page renders exactly as Settings shows it.
            host.RefreshDisplayList();

            var target = WithOpenGroup(host, request.Page switch
            {
                PageKind.Widget => new ConfigTarget.Widget(DemoWidget(host, request.Kind!.Value, request.Style, request.Config).Id),
                _ => request.App!,
            }, request.Open);

            // [S-2847] --place-chosen / --place-query / --place-reply: the location row's states.
            PlaceRender.Prepare(host, request.Kind);

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

            PlaceRender.Type();

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

            if (request.Search is not null || request.Flyout is not null || PlaceRender.Query is not null)
            {
                // The suggestion list is a popup, outside the window's tree: drawn on its own.
                foreach (var popup in VisualTreeHelper.GetOpenPopupsForXamlRoot(root.XamlRoot))
                {
                    if (popup.Child is FrameworkElement child)
                    {
                        var path = Path.ChangeExtension(request.OutPath, null)
                            + (request.Search is not null ? "-search.png" : PlaceRender.Query is not null ? "-place.png" : "-flyout.png");
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

    /// <summary>
    /// [S-3824] <c>--demo-accounts</c>: two stand-in accounts on the Accounts page, one healthy with
    /// numbers for every kind, one that needs signing in again with mail only. The numbers go into
    /// the render host's own ledger; no account is stored and nothing reaches a provider.
    /// </summary>
    private static void DemoAccounts(AppHost host, bool on)
    {
        AccountsPage.DebugAccounts = null;
        if (!on)
        {
            return;
        }

        var now = DateTimeOffset.Now;
        var microsoft = new Services.Accounts.AccountInfo("demo-ms", Themia.Core.Accounts.HostProviders.Microsoft,
            "alex@contoso.com", "Alex Wilber", null, null, null);
        var imap = new Services.Accounts.AccountInfo("demo-imap", Themia.Core.Accounts.HostProviders.Imap,
            "alex@fabrikam.example", "alex@fabrikam.example", "Fabrikam mail", null,
            "The server refused the password. Sign in again to keep this account syncing.");

        AccountsPage.DebugAccounts = [microsoft, imap];

        host.Sync.Report(microsoft.Id, new(Themia.Core.Sync.SyncKind.Mail, 1240, 3, now.AddMinutes(-4), 2));
        host.Sync.Report(microsoft.Id, new(Themia.Core.Sync.SyncKind.Calendar, 386, 2, now.AddMinutes(-12), 0));
        host.Sync.Report(microsoft.Id, new(Themia.Core.Sync.SyncKind.Contacts, 214, 0, now.AddHours(-3), 0));
        host.Sync.Report(imap.Id, new(Themia.Core.Sync.SyncKind.Mail, 50, 1, now.AddDays(-2), 0));
    }

    /// <summary>
    /// The demo profile's widget of <paramref name="kind"/>; <c>--config</c> writes the given fields
    /// over its config (<c>{"shuffle":true}</c>, read and repaired as the state file is), and
    /// <c>--style</c> then sets its style ([S-6070]: the page shows
    /// the rows that style and state offer).
    /// </summary>
    private static WidgetInstance DemoWidget(AppHost host, WidgetId kind, string? style, string? config)
    {
        var demo = host.State.Views.SelectMany(v => v.Widgets).FirstOrDefault(w => w.Kind == kind);
        if (demo is null)
        {
            demo = WidgetInstance.Create(kind, new FracRect(0.6, 0.6, 0.2, 0.2));
            host.State.Views[0].Widgets.Add(demo);
        }

        if (config is not null && WithFields(demo, config) is { } read)
        {
            demo.Config = read;
        }

        if (style is not null)
        {
            if (WidgetStyles.Find(kind, style) is { } found)
            {
                WidgetStyles.Set(demo.Config, found.Value);
            }
            else
            {
                Console.WriteLine($"settings: {kind} has no style '{style}' (has: {string.Join(", ", WidgetStyles.For(kind).Select(s => s.Name))})");
            }
        }

        return demo;
    }

    /// <summary>
    /// <c>--config</c>: <paramref name="fields"/> (a JSON object) written over the widget's config in
    /// a state document, read back through the state file's own parse and repair, so a render sees
    /// exactly what a hand-edited file would give. Null, said on the console, when it does not read.
    /// </summary>
    private static WidgetConfig? WithFields(WidgetInstance widget, string fields)
    {
        try
        {
            if (System.Text.Json.Nodes.JsonNode.Parse(fields) is not System.Text.Json.Nodes.JsonObject wanted)
            {
                Console.WriteLine("settings: --config is not a JSON object");
                return null;
            }

            var state = AppState.CreateDefault();
            state.Views[0].Widgets.Add(widget);
            var document = System.Text.Json.Nodes.JsonNode.Parse(SettingsTransfer.Serialize(state))!;
            var node = document["views"]![0]!["widgets"]!.AsArray().First(w => w!["id"]!.GetValue<string>() == widget.Id)!;
            var target = node["config"]!.AsObject();

            foreach (var (name, value) in wanted)
            {
                target[name] = value?.DeepClone();
            }

            var parsed = AppStateStore.Parse(document.ToJsonString());
            if (parsed.State?.Views[0].Widgets.FirstOrDefault(w => w.Id == widget.Id) is { } read)
            {
                return read.Config;
            }

            Console.WriteLine("settings: --config was refused by the state file's parse");
            return null;
        }
        catch (System.Text.Json.JsonException ex)
        {
            Console.WriteLine($"settings: --config could not be read: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// <c>--views n</c>: the demo profile grown to n views, long and short names, every third with
    /// an icon, so the list pages and the "Views on the bar" choice can be looked at crowded.
    /// </summary>
    private static void AddManyViews(AppState state, int total)
    {
        string[] names =
        [
            "Focus", "Gaming", "Morning routine and news", "Travel", "Home office", "Weekend",
            "Studio", "Deep work: no distractions at all", "Reading", "Family", "Music", "Finance",
            "Fitness", "Side project", "Calendar overview", "Night", "Kids", "Photos", "Build status",
            "Stocks", "Shopping", "Notes", "Inbox zero", "Presentation mode", "Q",
        ];
        string[] icons = [Glyphs.Calendar, Glyphs.Mail, Glyphs.Sun, Glyphs.Moon, Glyphs.Play];

        // [S-3810] Some views coloured, so the coloured icons can be looked at on every list.
        string[] colours = ["#E74856", "#0099BC", "#FFB900", "#8764B8", "#10893E"];

        for (var i = state.Views.Count; i < Math.Min(total, 60); i++)
        {
            var view = ViewState.Create(names[i % names.Length]);
            if (i % 3 == 0)
            {
                view.Icon = icons[i / 3 % icons.Length];
            }

            if (i % 2 == 0)
            {
                view.Color = colours[i / 2 % colours.Length];
            }

            for (var w = 0; w < i % 4; w++)
            {
                view.Widgets.Add(WidgetInstance.Create(new WidgetId("clock"), new FracRect(0.05 + 0.2 * w, 0.1, 0.15, 0.1)));
            }

            state.Views.Add(view);
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
        work.Widgets.Add(WidgetInstance.Create(CalendarWidgetConfig.Descriptor.Id, new FracRect(0.05, 0.08, 0.20, 0.30)));
        work.Widgets.Add(WidgetInstance.Create(EmailConfig.Descriptor.Id, new FracRect(0.30, 0.08, 0.25, 0.30)));

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
