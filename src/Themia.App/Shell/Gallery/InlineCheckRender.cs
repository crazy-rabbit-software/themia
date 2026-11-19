#if DEBUG
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.App.Interop;
using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI.Kit;
using Windows.Graphics;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-13950] Debug only: the inline check. <c>Themia.exe --inline-check &lt;out.txt&gt;</c> draws every widget's inline box
/// open over the state it opens on (the add row, a row's rename, typed words longer than the row, a fence's title, a
/// folder item's name, a search box taking the keys), one card at a time at the kind's min, natural and max, off-screen
/// as <c>--widget-png</c> does; measures every text, glyph, picture, row and list viewport in the card at rest and open
/// (<see cref="Collect"/>), and judges them by <see cref="InlineFit.Judge"/>: the box is the box of the words it stands
/// in for and nothing else moves, grows or goes. Writes one line per finding (none: "ok") and exits 1 on any.
/// </summary>
internal static class InlineCheckRender
{
    private const string Scope = "inline-check";
    public const string Flag = "--inline-check";

    /// <summary>
    /// Every inline input a widget has, by the render state that opens it over its rest state. Not here: a desktop icon's
    /// rename box, which is Explorer's own (wider than the name and over the icons below, [S-5607]: it moves nothing, but
    /// it is not the name's box), and a search opened from a tool line or the card menu (a row of its own, the list's
    /// sizing, [S-1326], [S-1275]).
    /// </summary>
    private static readonly (string Kind, string? Style, string AtRest, string Open)[] Cases =
    [
        ("tasks", null, "tasks", "adding"),
        ("tasks", null, "tasks", "typing"),
        ("tasks", null, "tasks", "renaming"),
        ("habitTracker", null, "sample", "adding"),
        ("habitTracker", null, "sample", "typing"),
        ("habitTracker", null, "sample", "renaming"),
        ("folder", null, "documents", "renaming"),
        ("folder", "details", "documents", "renaming"),
        ("folder", "grid", "documents", "renaming"),
        ("fence", null, "sample", "title"),
        ("contacts", null, "sample", "search"),
    ];

    public static bool TryRunFromCommandLine(Application app, string[] args)
    {
        var at = Array.IndexOf(args, Flag);
        if (at < 0)
        {
            return false;
        }

        if (at + 1 >= args.Length || DispatcherQueue.GetForCurrentThread() is not { } dispatcher)
        {
            Console.WriteLine($"usage: {Flag} <out.txt>");
            app.Exit();
            return true;
        }

        var outPath = System.IO.Path.GetFullPath(args[at + 1]);
        _ = dispatcher.TryEnqueue(async () =>
        {
            Window? window = null;
            Ticker? ticker = null;
            var lines = new List<string>();
            var failed = 0;
            try
            {
                var design = new DesignState();
                DesignApplier.Apply(design);
                const double Width = 1300, Height = 1100;
                var page = new Canvas { Width = Width, Height = Height };
                window = new Window { Content = new Grid { Children = { page } }, Title = "Themia inline check" };
                UiLanguage.ApplyLanguageOnly(page);
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                var dpi = Win32.GetDpiForWindow(hwnd);
                var scale = dpi > 0 ? dpi / 96.0 : 1.0;

                // Off-screen and never activated: it must not appear on the user's desktop.
                window.AppWindow.Move(new PointInt32(-32000, -32000));
                window.AppWindow.Resize(new SizeInt32((int)((Width + 40) * scale), (int)((Height + 80) * scale)));
                window.AppWindow.Show(activateWindow: false);

                ticker = new Ticker(dispatcher);
                ConfirmFlyout.RenderInWindow = true;
                AppServices.System = new Services.SystemData.SystemHub(ticker, dispatcher);
                AppServices.Gallery = new Services.GalleryService(ticker, dispatcher);
                var host = new NoticeRender.RenderCardHost(ticker) { Design = design };

                foreach (var (name, styleName, rest, open) in Cases)
                {
                    if (!ModuleCatalog.TryFind(name, out var picked))
                    {
                        lines.Add($"{name}: no such kind");
                        failed++;
                        continue;
                    }

                    var kind = picked.Id;
                    int? style = styleName is not null && WidgetStyles.Find(kind, styleName) is { } found ? found.Value : null;
                    foreach (var tier in SizeFit.CheckedAt(SizeFit.Of(kind, style)))
                    {
                        var label = $"{name}{(styleName is null ? string.Empty : "/" + styleName)} {open} {tier.Name}";
                        var before = await DrawAsync(page, host, kind, style, tier, rest).ConfigureAwait(true);
                        var after = await DrawAsync(page, host, kind, style, tier, open).ConfigureAwait(true);
                        var findings = after.Box is null
                            ? ["no box is open"]
                            : InlineFit.Judge(before.Card, after.Card, after.Box, before.Parts, after.Parts);
                        failed += findings.Count;
                        lines.Add(findings.Count == 0 ? $"{label}: ok" : $"{label}: {string.Join("; ", findings)}");
                        Console.WriteLine($"inline-check: {lines[^1]}");
                    }
                }

                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(outPath)!);
                await File.WriteAllLinesAsync(outPath, lines).ConfigureAwait(true);
                Console.WriteLine($"inline-check: {failed} findings; wrote {outPath}");
            }
            catch (Exception ex)
            {
                Log.Error(Scope, "the inline check failed", ex);
                Console.WriteLine($"inline-check: failed: {ex.Message}");
                failed++;
            }
            finally
            {
                AppServices.System?.Dispose();
                AppServices.System = null;
                AppServices.Gallery?.Dispose();
                AppServices.Gallery = null;
                ticker?.Dispose();
                window?.Close();
                Environment.ExitCode = failed == 0 ? 0 : 1;
                app.Exit();
            }
        });

        return true;
    }

    /// <summary>One card of <paramref name="kind"/> at <paramref name="tier"/> in <paramref name="state"/>: its size, its parts and its open box.</summary>
    private static async Task<((double Width, double Height) Card, List<InlinePart> Parts, InlinePart? Box)> DrawAsync(
        Canvas page, NoticeRender.RenderCardHost host, WidgetId kind, int? style, CheckedSize tier, string state)
    {
        var cards = new List<WidgetCard>();
        host.FixtureState = state;
        try
        {
            WidgetRender.Place(page, host, kind, style, [tier], cards);
            await Task.Delay(700).ConfigureAwait(true);
            var card = cards[0];
            if (card.ShowsSetup)
            {
                card.RunForPreview();
            }

            if (card.View is IWidgetPreviewState preview)
            {
                await preview.ShowPreviewStateAsync(state).ConfigureAwait(true);
            }

            await Task.Delay(400).ConfigureAwait(true);
            card.UpdateLayout();
            var (parts, box) = Collect(card);
            return ((card.ActualWidth, card.ActualHeight), parts, box);
        }
        finally
        {
            foreach (var card in cards)
            {
                card.Detach();
            }

            page.Children.Clear();
        }
    }

    /// <summary>
    /// Every text, glyph, picture, row and list viewport drawn in the card's view, by what it is and says (numbered where that repeats),
    /// with its box in the card; and the open text or search box, whose own parts are its and not counted.
    /// </summary>
    private static (List<InlinePart> Parts, InlinePart? Box) Collect(WidgetCard card)
    {
        var parts = new List<InlinePart>();
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        InlinePart? box = null;
        if (card.View is DependencyObject view)
        {
            Walk(card, view, parts, seen, ref box);
        }

        return (parts, box);
    }

    private static void Walk(WidgetCard card, DependencyObject node, List<InlinePart> parts, Dictionary<string, int> seen, ref InlinePart? box)
    {
        if (node is UIElement { Visibility: not Visibility.Visible })
        {
            return;
        }

        if (node is TextBox or AutoSuggestBox)
        {
            box ??= Place(card, (FrameworkElement)node, "box");
            return;
        }

        if (node is FrameworkElement { ActualWidth: > 0, ActualHeight: > 0 } element && KeyOf(element) is { } key)
        {
            var n = seen.GetValueOrDefault(key);
            seen[key] = n + 1;
            parts.Add(Place(card, element, n == 0 ? key : $"{key} #{n + 1}"));
        }

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
        {
            Walk(card, VisualTreeHelper.GetChild(node, i), parts, seen, ref box);
        }
    }

    private static string? KeyOf(FrameworkElement element) => element switch
    {
        TextBlock { Text.Length: > 0 } text => $"text {text.Text}",
        FontIcon { Glyph.Length: > 0 } glyph => $"glyph {(int)glyph.Glyph[0]:X4}",
        Image => "picture",
        RowBlock => "row",

        // A list's viewport: a list cut shorter (its last row no longer shown) is a shift too.
        ScrollViewer => "scroll",
        _ => null,
    };

    private static InlinePart Place(WidgetCard card, FrameworkElement element, string key)
    {
        var at = element.TransformToVisual(card).TransformPoint(new Windows.Foundation.Point(0, 0));
        return new InlinePart(key, at.X, at.Y, element.ActualWidth, element.ActualHeight);
    }
}
#endif
