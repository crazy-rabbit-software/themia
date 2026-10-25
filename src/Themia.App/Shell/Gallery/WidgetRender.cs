#if DEBUG
using Microsoft.UI;
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
using Windows.Graphics;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-1917] Debug only: draw one widget, in a real <see cref="WidgetCard"/> on a preview host, to a
/// PNG, off-screen. <c>Themia.exe --widget-png &lt;Kind&gt; &lt;out.png&gt; [w h] [--theme light|dark]
/// [--state name] [--progress 0..1] [--span s] [--style name] [--scale f] [--header top|bottom|left|right|hidden] [--align left|center|right] [--background fill] [--own-items fill] [--own-item-text #RRGGBB] [--transparent]</c> ([S-6006]: any styled kind, in one of its styles). Without a size the card is drawn at every tier of <see cref="WidgetTiers"/>
/// (XS S M Wide Tall L XL), labelled, on one page; with one, once at that size. The widget has its
/// kind's default config and header; the host is a preview, so nothing it does reaches the machine.
/// <c>--reference</c> adds, beside the first card, a stock WinUI card at the same size (a Border with
/// the framework's own CardBackgroundFillColorDefault / CardStrokeColorDefault over
/// SolidBackgroundFillColorBase, OverlayCornerRadius) to compare pixels against ([S-836]).
/// </summary>
internal static class WidgetRender
{
    private const string Scope = "widget-render";
    private const string Flag = "--widget-png";

    /// <summary>[S-6569] The cards' own background (<c>--background</c>), Default to follow the design.</summary>
    private static FillChoice _background = new();

    /// <summary>[S-6535] The cards' own content scale (<c>--scale 1.5</c>), as stored.</summary>
    private static double _scale = 1.0;

    /// <summary>[S-404] The cards' header placement (<c>--header left</c>), or the kind's own default.</summary>
    private static HeaderPlacement? _header;

    /// <summary>[S-6586] The cards' own alignment, or null for the kind's default.</summary>
    private static ContentAlign? _align;
    private static int? _span;

    /// <summary>[S-6547] The cards' own item colour and item text colour (<c>--own-items</c>, <c>--own-item-text</c>), empty to follow the design.</summary>
    private static FillChoice _ownItems = new();

    private static string _ownItemText = string.Empty;

    /// <summary>Space between frames and around the page, in DIPs.</summary>
    private const double Gap = 24;

    /// <summary>Height reserved above each frame for its size label.</summary>
    private const double LabelHeight = 22;

    /// <summary>Which row each tier sits on: four across the top, Tall and L under them, XL alone.</summary>
    private static readonly int[] RowOf = [0, 0, 0, 0, 1, 1, 2];

    /// <summary>The application theme can only be chosen before the resources load: read by <c>App</c>.</summary>
    public static ApplicationTheme? RequestedTheme(string[] args)
    {
        if (Array.IndexOf(args, Flag) < 0)
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

    public static bool TryRunFromCommandLine(Application app, string[] args)
    {
        var at = Array.IndexOf(args, Flag);
        if (at < 0)
        {
            return false;
        }

        if (at + 2 >= args.Length
            || !ModuleCatalog.TryFind(args[at + 1], out var picked)
            || DispatcherQueue.GetForCurrentThread() is not { } dispatcher)
        {
            Log.Error(Scope, $"usage: {Flag} <Kind> <out.png> [w h] [--theme light|dark] [--state name] [--progress 0..1] [--span s] [--style name] [--scale f] [--header top|bottom|left|right|hidden] [--align left|center|right] [--background fill] [--own-items fill] [--own-item-text #RRGGBB] [--sizes WxH,..|tiers] [--keys k1,k2|text:abc] [--design k=v,...]");
            Console.WriteLine($"usage: {Flag} <Kind> <out.png> [w h] [--theme light|dark] [--state name] [--progress 0..1] [--span s] [--style name] [--scale f] [--header top|bottom|left|right|hidden] [--align left|center|right] [--background fill] [--own-items fill] [--own-item-text #RRGGBB] [--sizes WxH,..|tiers] [--keys k1,k2|text:abc] [--design k=v,...]");
            app.Exit();
            return true;
        }

        var kind = picked.Id;
        var outPath = System.IO.Path.GetFullPath(args[at + 2]);
        var state = Option(args, "--state");

        // [S-1826] --progress 0..1: every history chart drawn that far through its slide, still.
        SparkBand.PreviewProgress = Option(args, "--progress") is { } progressText
            && double.TryParse(progressText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var progress)
                ? Math.Clamp(progress, 0, 1)
                : null;

        // [S-6328] --tween 0..1: every readout's glide drawn that far between two readings, still.
        GlideMotion.PreviewProgress = Option(args, "--tween") is { } tweenText
            && double.TryParse(tweenText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var tween)
                ? Math.Clamp(tween, 0, 1)
                : null;

        // [S-6569] --background default|transparent|#RRGGBB[@percent]: the cards' own background.
        _background = DesignArgs.Fill(Option(args, "--background")) ?? new FillChoice();

        // [S-6535] --scale 0.75|1.5: the widget's own content scale, through the same path as on the desktop.
        _scale = Option(args, "--scale") is { } scaleText
            && double.TryParse(scaleText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var scaleValue)
                ? WidgetScale.Normalise(scaleValue)
                : 1.0;

        // [S-404] --header top|bottom|left|right|hidden: where the cards draw their header.
        _header = Option(args, "--header") is { } headerText && Enum.TryParse<HeaderPlacement>(headerText, ignoreCase: true, out var headerValue)
            && Enum.IsDefined(headerValue)
                ? headerValue
                : null;

        // [S-6586] --align left|center|right: where the cards draw their content (a kind or style
        // that does not align draws its default, as on the desktop).
        _align = Option(args, "--align") is { } alignText && Enum.TryParse<ContentAlign>(alignText, ignoreCase: true, out var alignValue)
            && Enum.IsDefined(alignValue)
                ? alignValue
                : null;

        // [S-6547] The widget's own item layer, over the app-wide one --design sets: the per-widget override.
        _ownItems = DesignArgs.Fill(Option(args, "--own-items")) ?? new FillChoice();
        _ownItemText = HexColor.Normalise(Option(args, "--own-item-text"));

        // [S-6006] Any styled kind in one of its styles; a wrong name prints the kind's styles.
        int? style = null;
        if (Option(args, "--style") is { } styleName)
        {
            if (WidgetStyles.Find(kind, styleName) is { } found)
            {
                style = found.Value;
            }
            else
            {
                Console.WriteLine($"widget: {kind} has no style '{styleName}' (has: {string.Join(", ", WidgetStyles.For(kind).Select(s => s.Name))})");
            }
        }
        // [S-1829] --span seconds: a meter's chart span, to see a folded chart (an hour on a tile).
        _span = Option(args, "--span") is { } spanText && int.TryParse(spanText, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var spanValue)
            ? spanValue
            : null;

        List<WidgetTier> tiers = Size(args, at + 3) is { } size
            ? [new WidgetTier($"{size.Width:0}x{size.Height:0}", size.Width, size.Height)]
            : Sizes(Option(args, "--sizes")) is { Count: > 0 } sizes ? sizes : [.. WidgetTiers.All];

        _ = dispatcher.TryEnqueue(async () =>
        {
            Window? window = null;
            Ticker? ticker = null;
            var cards = new List<WidgetCard>();

            try
            {
                // The themed brushes follow the design's theme, not the application's: set both.
                // [S-6480] --design sets the look the cards are drawn with (a border, a radius, a fill).
                var design = DesignArgs.From(args, RequestedTheme(args) switch
                {
                    ApplicationTheme.Light => AppTheme.Light,
                    ApplicationTheme.Dark => AppTheme.Dark,
                    _ => AppTheme.System,
                });
                DesignApplier.Apply(design);

                var reference = Array.IndexOf(args, "--reference") >= 0;
                var (width, height) = PageSize(tiers);
                if (reference)
                {
                    width += tiers[0].Width + Gap;
                }
                // [S-1917] --transparent: the page draws nothing, so the PNG keeps the cards' own alpha
                // (a transparent card, the corners) for compositing a render onto a picture.
                var page = new Canvas
                {
                    Width = width,
                    Height = height,
                    Background = Array.IndexOf(args, "--transparent") >= 0
                        ? null
                        : DesignApplier.Brush("SolidBackgroundFillColorSecondaryBrush", Colors.DimGray),
                };

                window = new Window { Content = new Grid { Children = { page } }, Title = "Themia widget render" };

                // As on the desktop: the page is screen geometry, each card flows with the language itself.
                UiLanguage.ApplyLanguageOnly(page);
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                var dpi = Win32.GetDpiForWindow(hwnd);
                var scale = dpi > 0 ? dpi / 96.0 : 1.0;

                // Off-screen and never activated: it must not appear on the user's desktop.
                window.AppWindow.Move(new PointInt32(-32000, -32000));
                window.AppWindow.Resize(new SizeInt32((int)((width + 40) * scale), (int)((height + 80) * scale)));
                window.AppWindow.Show(activateWindow: false);

                ticker = new Ticker(dispatcher);

                // [S-1500] A confirmation a state opens is drawn inside this window, as a popup of
                // its own window does not render to a bitmap. Set before any view builds one.
                ConfirmFlyout.RenderInWindow = true;

                // [S-2500] The render's own hub: the system widgets read through it as on the desktop.
                AppServices.System = new Services.SystemData.SystemHub(ticker, dispatcher);
                AppServices.Gallery = new Services.GalleryService(ticker, dispatcher);
                // [S-9929] A kit service widget's preview states are its provider's recordings.
                FixtureServices.State = state;
                var host = new NoticeRender.RenderCardHost(ticker) { Design = design };
                Place(page, host, kind, style, tiers, cards);
                if (reference)
                {
                    PlaceReference(page, tiers[0], width - Gap - tiers[0].Width);
                }

                await Task.Delay(800).ConfigureAwait(true);

                if (state is not null)
                {
                    foreach (var card in cards)
                    {
                        if (card.View is IWidgetPreviewState preview)
                        {
                            // [S-2711] A preview state draws over fixture data: no account is needed.
                            if (card.ShowsSetup)
                            {
                                card.RunForPreview();
                                await Task.Delay(200).ConfigureAwait(true);
                            }

                            if (!preview.PreviewStates.Contains(state, StringComparer.Ordinal))
                            {
                                Console.WriteLine($"widget: {kind} has no state '{state}' (has: {string.Join(", ", preview.PreviewStates)})");
                            }

                            await preview.ShowPreviewStateAsync(state).ConfigureAwait(true);
                        }
                        else
                        {
                            Console.WriteLine($"widget: {kind} has no preview states");
                        }
                    }

                    await Task.Delay(400).ConfigureAwait(true);
                }

                // [S-1980] Keys handed to every card before the shot, to look at a widget's keyboard
                // focus ("--keys Down,Down" or "--keys text:be" for type-ahead) without the lease.
                if (Option(args, "--keys") is { } keys)
                {
                    foreach (var card in cards)
                    {
                        SendKeys(card, keys);
                    }

                    await Task.Delay(400).ConfigureAwait(true);
                }

                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(outPath)!);
                await RenderPng.SaveAsync(page, outPath).ConfigureAwait(true);
                Console.WriteLine($"widget: wrote {outPath}");

                // A state that opens a popup (a confirmation) is not part of the page's bitmap: each
                // open one is written beside it as <name>-popup<n>.png.
                var popups = VisualTreeHelper.GetOpenPopupsForXamlRoot(page.XamlRoot);
                for (var i = 0; i < popups.Count; i++)
                {
                    if (popups[i].Child is not UIElement content)
                    {
                        continue;
                    }

                    var target = System.IO.Path.ChangeExtension(outPath, null) + $"-popup{i}.png";
                    try
                    {
                        // A flyout's presenter carries a system backdrop, which a bitmap cannot draw:
                        // draw what it presents.
                        var drawn = content is ContentControl { Content: UIElement presented } ? presented : content;
                        await RenderPng.SaveAsync(drawn, target).ConfigureAwait(true);
                        Console.WriteLine($"widget: wrote {target}");
                    }
                    catch (Exception ex)
                    {
                        // A popup with nothing laid out (a closing tooltip) cannot be drawn; the page already was.
                        Log.Warn(Scope, $"popup {i} could not be drawn", ex);
                        Console.WriteLine($"widget: popup {i} could not be drawn: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(Scope, "rendering the widget failed", ex);
                Console.WriteLine($"widget: failed: {ex.Message}");
            }
            finally
            {
                foreach (var card in cards)
                {
                    card.Detach();
                }

                AppServices.System?.Dispose();
                AppServices.System = null;
                AppServices.Gallery?.Dispose();
                AppServices.Gallery = null;
                ticker?.Dispose();
                window?.Close();
                app.Exit();
            }
        });

        return true;
    }

    private static void SendKeys(WidgetCard card, string keys)
    {
        foreach (var token in keys.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (token.StartsWith("text:", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var character in token[5..])
                {
                    card.SendText(character);
                }
            }
            else if (Enum.TryParse<WidgetKey>(token, ignoreCase: true, out var key) && Enum.IsDefined(key))
            {
                card.SendKey(key);
            }
            else
            {
                Console.WriteLine($"widget: no key '{token}'");
            }
        }
    }

    /// <summary>One labelled card per tier, laid out in <see cref="RowOf"/>'s rows (one row for a single size).</summary>
    private static void Place(Canvas page, IWidgetCardHost host, WidgetId kind, int? style, List<WidgetTier> tiers, List<WidgetCard> cards)
    {
        var rows = Rows(tiers);
        var x = new double[rows.Count];
        var top = new double[rows.Count];

        for (var row = 0; row < rows.Count; row++)
        {
            x[row] = Gap;
            top[row] = row == 0 ? Gap : top[row - 1] + LabelHeight + rows[row - 1].Max(t => t.Height) + Gap;
        }

        for (var row = 0; row < rows.Count; row++)
        {
            foreach (var tier in rows[row])
            {
                var label = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
                label.Text = $"{tier.Name}  {tier.Width:0} x {tier.Height:0}";
                Canvas.SetLeft(label, x[row]);
                Canvas.SetTop(label, top[row]);
                page.Children.Add(label);

                var model = WidgetInstance.Create(kind, new FracRect(0, 0, 0.1, 0.1));
                model.Background = _background.Clone();
                model.Scale = _scale;
                if (_header is { } header)
                {
                    model.Header = header;
                }
                model.ItemBackground = _ownItems.Clone();
                model.ItemTextColor = _ownItemText;
                model.Align = _align;
                if (style is { } chosen)
                {
                    WidgetStyles.Set(model.Config, chosen);
                }

                // [S-2848] --place-cleared: a widget about a place, with its place cleared.
                PlaceRender.Apply(model.Config);

                if (_span is { } seconds && model.Config is GaugeConfig gauge)
                {
                    gauge.GraphSpanSeconds = seconds;
                }

                var card = new WidgetCard(host, model)
                {
                    Width = tier.Width,
                    Height = tier.Height,
                };

                // The size is the painted card: there is no neighbour for the gap to separate.
                card.ApplyGap(0);
                card.ApplyContentScale(WidgetScale.Effective(1, 1, model.Scale));
                Canvas.SetLeft(card, x[row]);
                Canvas.SetTop(card, top[row] + LabelHeight);
                page.Children.Add(card);
                cards.Add(card);

                x[row] += tier.Width + Gap;
            }
        }
    }

    /// <summary>
    /// [S-836] A stock WinUI card, every brush the framework's own in the application theme (which a
    /// render sets from <c>--theme</c> before the resources load), and the colours it should composite
    /// to, printed for the pixel comparison.
    /// </summary>
    private static void PlaceReference(Canvas page, WidgetTier tier, double left)
    {
        var resources = Application.Current.Resources;
        var radius = resources["OverlayCornerRadius"] is CornerRadius r ? r : new CornerRadius(Fluent.OverlayRadius);

        var label = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
        label.Text = "WinUI reference";
        Canvas.SetLeft(label, left);
        Canvas.SetTop(label, Gap);
        page.Children.Add(label);

        var card = new Grid
        {
            Width = tier.Width,
            Height = tier.Height,
            CornerRadius = radius,
            Background = (Brush)resources["SolidBackgroundFillColorBaseBrush"],
            Children =
            {
                new Border
                {
                    Background = (Brush)resources["CardBackgroundFillColorDefaultBrush"],
                    BorderBrush = (Brush)resources["CardStrokeColorDefaultBrush"],
                    BorderThickness = new Thickness(1),
                    CornerRadius = radius,
                },
            },
        };

        Canvas.SetLeft(card, left);
        Canvas.SetTop(card, Gap + LabelHeight);
        page.Children.Add(card);

        if (resources["SolidBackgroundFillColorBase"] is Windows.UI.Color b
            && resources["CardBackgroundFillColorDefault"] is Windows.UI.Color fill
            && resources["CardStrokeColorDefault"] is Windows.UI.Color stroke)
        {
            var under = new RgbColor(b.R, b.G, b.B);
            var body = DesignLook.Over(fill.A, new RgbColor(fill.R, fill.G, fill.B), under);
            var edge = DesignLook.Over(stroke.A, new RgbColor(stroke.R, stroke.G, stroke.B), under);
            Console.WriteLine($"widget: reference base {HexColor.From(b.R, b.G, b.B)} card {HexColor.From(body.R, body.G, body.B)} edge {HexColor.From(edge.R, edge.G, edge.B)}");
        }
    }

    private static List<List<WidgetTier>> Rows(List<WidgetTier> tiers)
    {
        if (tiers.Count != RowOf.Length)
        {
            return [tiers];
        }

        var rows = new List<List<WidgetTier>>();
        for (var i = 0; i < tiers.Count; i++)
        {
            while (rows.Count <= RowOf[i])
            {
                rows.Add([]);
            }

            rows[RowOf[i]].Add(tiers[i]);
        }

        return rows;
    }

    private static (double Width, double Height) PageSize(List<WidgetTier> tiers)
    {
        var rows = Rows(tiers);
        var width = rows.Max(r => r.Sum(t => t.Width + Gap)) + Gap;
        var height = Gap + rows.Sum(r => LabelHeight + r.Max(t => t.Height) + Gap);
        return (width, height);
    }

    /// <summary>An optional "w h" pair right after the output path.</summary>
    private static (double Width, double Height)? Size(string[] args, int at)
    {
        if (at + 1 < args.Length
            && double.TryParse(args[at], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var w)
            && double.TryParse(args[at + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var h)
            && w >= 40 && h >= 40 && w <= 2000 && h <= 2000)
        {
            return (w, h);
        }

        return null;
    }

    /// <summary>[S-1917] <c>--sizes 100x100,160x100</c>: several in-between sizes on one page, beside or instead of the tiers.</summary>
    private static List<WidgetTier>? Sizes(string? list)
    {
        if (list is null)
        {
            return null;
        }

        var tiers = new List<WidgetTier>();
        foreach (var item in list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (item.Equals("tiers", StringComparison.OrdinalIgnoreCase))
            {
                tiers.AddRange(WidgetTiers.All);
                continue;
            }

            var parts = item.Split('x');
            if (parts.Length == 2 && Size(parts, 0) is { } size)
            {
                tiers.Add(new WidgetTier(item, size.Width, size.Height));
            }
            else
            {
                Console.WriteLine($"widget: '{item}' is not a size (WxH, 40 to 2000)");
            }
        }

        return tiers;
    }

    private static string? Option(string[] args, string name)
    {
        var at = Array.IndexOf(args, name);
        return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
    }
}
#endif
