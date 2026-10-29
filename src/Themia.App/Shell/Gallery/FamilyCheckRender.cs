#if DEBUG
using System.Text.Json;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Interop;
using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Windows.Graphics;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-6731] Debug only: the family's render walk. <c>Themia.exe --family-check &lt;out.json&gt;
/// [--theme light|dark] [--kinds a,b]</c> draws every kind (or those named) at the seven tiers, in its
/// default state, each of its preview states (not the interaction ones: a hover, a press, a lasso)
/// and each of its styles (at its first data state), off-screen as <c>--widget-png</c> does, walks
/// what each card's header, view and Set up row draw (<see cref="FamilyWalk"/>) and judges every
/// element by <see cref="FamilyCheck"/>. Writes the findings per kind (each with its count and the
/// first variant it was seen in) and prints one line per kind; <c>scripts/family-check.ps1</c> runs
/// it for both themes and holds the result to its baseline. The theme, as for every render, is
/// chosen before the resources load (<see cref="RequestedTheme"/>).
/// </summary>
internal static class FamilyCheckRender
{
    private const string Scope = "family-check";
    private const string Flag = "--family-check";

    /// <summary>Interaction states: they say nothing about the family's look.</summary>
    private static readonly HashSet<string> Skip = new(StringComparer.Ordinal)
    {
        "hover", "pressed", "focus", "hovered", "selected", "lasso", "inactive", "fullname", "packaged", "unknown",
        "confirm", "editing", "open", "linked", "sticker", "file",
    };

    /// <summary>
    /// The one-readout widgets (DECISIONS 2, [S-6702]): the only ones a Hero step is allowed in. The
    /// quick toggles and power buttons by their shared config; the six timers and the clock by id.
    /// </summary>
    private static readonly HashSet<string> OneReadoutIds = new(StringComparer.Ordinal)
    {
        "clock", "alarm", "countdown", "stopwatch", "pomodoro", "focusSession",
    };

    public static ApplicationTheme? RequestedTheme(string[] args)
    {
        if (Array.IndexOf(args, Flag) < 0)
        {
            return null;
        }

        var at = Array.IndexOf(args, "--theme");
        return at >= 0 && at + 1 < args.Length && args[at + 1].Equals("light", StringComparison.OrdinalIgnoreCase)
            ? ApplicationTheme.Light
            : ApplicationTheme.Dark;
    }

    public static bool TryRunFromCommandLine(Application app, string[] args)
    {
        var at = Array.IndexOf(args, Flag);
        if (at < 0)
        {
            return false;
        }

        if (at + 1 >= args.Length || DispatcherQueue.GetForCurrentThread() is not { } dispatcher)
        {
            Console.WriteLine($"usage: {Flag} <out.json> [--theme light|dark] [--kinds a,b]");
            app.Exit();
            return true;
        }

        var outPath = System.IO.Path.GetFullPath(args[at + 1]);
        var theme = RequestedTheme(args) == ApplicationTheme.Light ? AppTheme.Light : AppTheme.Dark;
        var only = Option(args, "--kinds")?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.Ordinal);
        var kinds = ModuleCatalog.Widgets.Select(d => d.Id).Where(id => only is null || only.Contains(id.Value)).ToList();

        _ = dispatcher.TryEnqueue(async () =>
        {
            Window? window = null;
            Ticker? ticker = null;
            var results = new SortedDictionary<string, SortedDictionary<string, (int Count, string First)>>(StringComparer.Ordinal);

            try
            {
                var design = new DesignState { Theme = theme };
                DesignApplier.Apply(design);
                List<WidgetTier> tiers = [.. WidgetTiers.All];
                var (width, height) = WidgetRender.PageSize(tiers);
                var page = new Canvas { Width = width, Height = height };
                window = new Window { Content = new Grid { Children = { page } }, Title = "Themia family check" };
                UiLanguage.ApplyLanguageOnly(page);
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                var dpi = Win32.GetDpiForWindow(hwnd);
                var scale = dpi > 0 ? dpi / 96.0 : 1.0;

                // Off-screen and never activated: it must not appear on the user's desktop.
                window.AppWindow.Move(new PointInt32(-32000, -32000));
                window.AppWindow.Resize(new SizeInt32((int)((width + 40) * scale), (int)((height + 80) * scale)));
                window.AppWindow.Show(activateWindow: false);

                ticker = new Ticker(dispatcher);
                ConfirmFlyout.RenderInWindow = true;
                AppServices.System = new Services.SystemData.SystemHub(ticker, dispatcher);
                AppServices.Gallery = new Services.GalleryService(ticker, dispatcher);
                var host = new NoticeRender.RenderCardHost(ticker) { Design = design };

                foreach (var kind in kinds)
                {
                    var found = new SortedDictionary<string, (int Count, string First)>(StringComparer.Ordinal);
                    results[kind.Value] = found;
                    foreach (var (state, style) in await VariantsAsync(page, host, kind).ConfigureAwait(true))
                    {
                        var name = (state ?? "default") + (style is null ? string.Empty : "/" + style.Value.Name);
                        await CheckAsync(page, host, kind, state, style?.Value, tiers, name, found).ConfigureAwait(true);
                    }

                    Console.WriteLine($"family-check: {kind.Value} {found.Count} findings ({found.Values.Sum(f => f.Count)} elements)");
                }

                Write(outPath, theme, results);
                Console.WriteLine($"family-check: wrote {outPath}");
            }
            catch (Exception ex)
            {
                Log.Error(Scope, "the family check failed", ex);
                Console.WriteLine($"family-check: failed: {ex.Message}");
            }
            finally
            {
                AppServices.System?.Dispose();
                AppServices.System = null;
                AppServices.Gallery?.Dispose();
                AppServices.Gallery = null;
                FixtureServices.State = null;
                ticker?.Dispose();
                window?.Close();
                app.Exit();
            }
        });

        return true;
    }

    /// <summary>A kind's variants: its default, its preview states and its styles at its first data state.</summary>
    private static async Task<List<(string? State, (int Value, string Name)? Style)>> VariantsAsync(Canvas page, IWidgetCardHost host, WidgetId kind)
    {
        // One card tells the kind's states.
        var probe = new List<WidgetCard>();
        WidgetRender.Place(page, host, kind, null, [WidgetTiers.All[2]], probe);
        await Task.Delay(100).ConfigureAwait(true);
        var states = probe[0].View is IWidgetPreviewState preview ? preview.PreviewStates.Where(s => !Skip.Contains(s)).ToList() : [];
        Clear(page, probe);

        var variants = new List<(string?, (int, string)?)> { (null, null) };
        variants.AddRange(states.Select(s => ((string?)s, ((int, string)?)null)));
        var data = states.FirstOrDefault(s => s is not ("armed" or "failed" or "unavailable"));
        variants.AddRange(WidgetStyles.For(kind).Skip(1).Select(s => (data, ((int, string)?)(s.Value, s.Name))));
        return variants;
    }

    private static async Task CheckAsync(Canvas page, IWidgetCardHost host, WidgetId kind, string? state, int? style, List<WidgetTier> tiers, string variant, SortedDictionary<string, (int Count, string First)> found)
    {
        var cards = new List<WidgetCard>();
        FixtureServices.State = state;
        try
        {
            WidgetRender.Place(page, host, kind, style, tiers, cards);
            await Task.Delay(700).ConfigureAwait(true);

            if (state is not null)
            {
                foreach (var card in cards)
                {
                    if (card.ShowsSetup)
                    {
                        card.RunForPreview();
                    }

                    if (card.View is IWidgetPreviewState preview)
                    {
                        await preview.ShowPreviewStateAsync(state).ConfigureAwait(true);
                    }
                }

                await Task.Delay(300).ConfigureAwait(true);
            }

            var oneReadout = OneReadoutIds.Contains(kind.Value) || cards[0].Model.Config is QuickToggleConfig or PowerButtonConfig;
            var palette = FamilyWalk.Palette(cards[0]);
            var accent = FamilyWalk.Accent();
            for (var i = 0; i < cards.Count; i++)
            {
                foreach (var root in cards[i].FamilyRoots())
                {
                    FamilyWalk.Visit(root, fe =>
                    {
                        foreach (var fact in FamilyWalk.Facts(fe))
                        {
                            foreach (var finding in FamilyCheck.Findings(fact, oneReadout, palette, accent))
                            {
                                var key = $"{fact.Type} {finding}";
                                found[key] = found.TryGetValue(key, out var seen) ? (seen.Count + 1, seen.First) : (1, $"{variant} {tiers[i].Name}");
                            }
                        }
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(Scope, $"{kind.Value} {variant} could not be checked", ex);
            found[$"render failed: {ex.GetType().Name}"] = (1, variant);
        }
        finally
        {
            Clear(page, cards);
        }
    }

    private static void Clear(Canvas page, List<WidgetCard> cards)
    {
        foreach (var card in cards)
        {
            card.Detach();
        }

        page.Children.Clear();
    }

    private static void Write(string path, AppTheme theme, SortedDictionary<string, SortedDictionary<string, (int Count, string First)>> results)
    {
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        using var stream = File.Create(path);
        using var w = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        w.WriteStartObject();
        w.WriteString("theme", theme == AppTheme.Light ? "light" : "dark");
        w.WriteStartObject("kinds");
        foreach (var (kind, found) in results)
        {
            w.WriteStartArray(kind);
            foreach (var (finding, (count, first)) in found)
            {
                w.WriteStartObject();
                w.WriteString("finding", finding);
                w.WriteString("rule", FamilyCheck.Rule(finding));
                w.WriteNumber("count", count);
                w.WriteString("first", first);
                w.WriteEndObject();
            }

            w.WriteEndArray();
        }

        w.WriteEndObject();
        w.WriteEndObject();
    }

    private static string? Option(string[] args, string name)
    {
        var at = Array.IndexOf(args, name);
        return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
    }
}
#endif
