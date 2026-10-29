#if DEBUG
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Interop;
using Themia.Core.Diagnostics;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Windows.Graphics;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-6732] Debug only: the family's seven blocks drawn alone, every variant at every tier, to PNGs
/// with their visual trees. <c>Themia.exe --family-blocks-png &lt;outDir&gt; [--theme light|dark]
/// [--only name,…]</c> writes <c>&lt;outDir&gt;/&lt;theme&gt;/&lt;variant&gt;.png</c> and
/// <c>.png.tree.json</c>: one page per variant, the seven tiers laid out as <c>--widget-png</c> lays
/// them, each on a plain card (the card's fill and stroke, radius 4). The same variants over the same
/// sample data are drawn by the mockups (TestResults/family/mockups/blocks.py), so the two can be laid
/// side by side and their boxes compared.
/// </summary>
internal static class FamilyBlocksRender
{
    private const string Scope = "family-blocks";
    private const string Flag = "--family-blocks-png";
    private const double Gap = 24;
    private const double LabelHeight = 22;

    public static ApplicationTheme? RequestedTheme(string[] args)
    {
        if (Array.IndexOf(args, Flag) < 0)
        {
            return null;
        }

        var at = Array.IndexOf(args, "--theme");
        return at >= 0 && at + 1 < args.Length && args[at + 1].Equals("dark", StringComparison.OrdinalIgnoreCase)
            ? ApplicationTheme.Dark
            : ApplicationTheme.Light;
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
            Console.WriteLine($"usage: {Flag} <outDir> [--theme light|dark] [--only name,..]");
            app.Exit();
            return true;
        }

        var dark = RequestedTheme(args) == ApplicationTheme.Dark;
        var outDir = System.IO.Path.Combine(System.IO.Path.GetFullPath(args[at + 1]), dark ? "dark" : "light");
        var onlyAt = Array.IndexOf(args, "--only");
        var only = onlyAt >= 0 && onlyAt + 1 < args.Length ? args[onlyAt + 1].Split(',').ToHashSet(StringComparer.Ordinal) : null;

        _ = dispatcher.TryEnqueue(async () =>
        {
            Window? window = null;
            try
            {
                DesignApplier.Apply(new Themia.Core.Model.DesignState { Theme = dark ? Themia.Core.Model.AppTheme.Dark : Themia.Core.Model.AppTheme.Light });
                List<WidgetTier> tiers = [.. WidgetTiers.All];
                var (width, height) = WidgetRender.PageSize(tiers);
                var page = new Canvas { Width = width, Height = height, Background = ThemeBrush.Of("SolidBackgroundFillColorSecondaryBrush", Colors.DimGray) };
                window = new Window { Content = new Grid { Children = { page } }, Title = "Themia family blocks" };
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                var dpi = Win32.GetDpiForWindow(hwnd);
                var scale = dpi > 0 ? dpi / 96.0 : 1.0;

                // Off-screen and never activated: it must not appear on the user's desktop.
                window.AppWindow.Move(new PointInt32(-32000, -32000));
                window.AppWindow.Resize(new SizeInt32((int)((width + 40) * scale), (int)((height + 80) * scale)));
                window.AppWindow.Show(activateWindow: false);
                System.IO.Directory.CreateDirectory(outDir);

                foreach (var (name, build) in FamilyBlockSamples.Variants)
                {
                    if (only is not null && !only.Contains(name))
                    {
                        continue;
                    }

                    page.Children.Clear();
                    var cards = Place(page, tiers, build);
                    await Task.Delay(500).ConfigureAwait(true);
                    var path = System.IO.Path.Combine(outDir, name + ".png");
                    await RenderPng.SaveAsync(page, path).ConfigureAwait(true);
                    TreeDump.Write([.. cards.Select((c, i) => (tiers[i].Name, (FrameworkElement)c, (DependencyObject?)c))], path + ".tree.json");
                    Console.WriteLine($"family-blocks: wrote {path}");
                }
            }
            catch (Exception ex)
            {
                Log.Error(Scope, "drawing the blocks failed", ex);
                Console.WriteLine($"family-blocks: failed: {ex.Message}");
            }
            finally
            {
                window?.Close();
                app.Exit();
            }
        });

        return true;
    }

    /// <summary>One plain card per tier, at the places <c>--widget-png</c> puts them, each holding what <paramref name="build"/> draws for its size.</summary>
    private static List<Grid> Place(Canvas page, List<WidgetTier> tiers, Func<double, double, FrameworkElement> build)
    {
        int[] rowOf = [0, 0, 0, 0, 1, 1, 2];
        var x = new double[3];
        var top = new double[3];
        for (var row = 0; row < 3; row++)
        {
            x[row] = Gap;
            top[row] = row == 0 ? Gap : top[row - 1] + LabelHeight + tiers.Where((_, i) => rowOf[i] == row - 1).Max(t => t.Height) + Gap;
        }

        var cards = new List<Grid>();
        for (var i = 0; i < tiers.Count; i++)
        {
            var tier = tiers[i];
            var row = rowOf[i];
            var label = Family.Text(FamilyType.Caption, ColourRole.TextSecondary, $"{tier.Name}  {tier.Width:0} x {tier.Height:0}");
            Canvas.SetLeft(label, x[row]);
            Canvas.SetTop(label, top[row]);
            page.Children.Add(label);

            // The card's base, its tint and its stroke, the stroke laid over the content so the content's inset is from the card's edge.
            var card = new Grid
            {
                Width = tier.Width,
                Height = tier.Height,
                CornerRadius = Family.Radius,
                Background = ThemeBrush.Of("SolidBackgroundFillColorBaseBrush", Colors.Gray),
            };
            card.Children.Add(new Border { Background = ThemeBrush.Of("CardBackgroundFillColorDefaultBrush", Colors.Transparent), IsHitTestVisible = false });
            card.Children.Add(build(tier.Width, tier.Height));
            card.Children.Add(new Border
            {
                BorderBrush = ThemeBrush.Of("CardStrokeColorDefaultBrush", Colors.Transparent),
                BorderThickness = new Thickness(FamilyShape.Hairline),
                CornerRadius = Family.Radius,
                IsHitTestVisible = false,
            });

            Canvas.SetLeft(card, x[row]);
            Canvas.SetTop(card, top[row] + LabelHeight);
            page.Children.Add(card);
            cards.Add(card);
            x[row] += tier.Width + Gap;
        }

        return cards;
    }
}
#endif
