#if DEBUG
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Interop;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Sdk.Kit;
using Windows.Graphics;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// Debug only: draw the open icon picker ([S-1803], [S-13605]) to a PNG, off-screen.
/// <c>Themia.exe --iconpicker-png &lt;out.png&gt; [--search text] [--theme light|dark] [--initials]</c>.
/// <para>
/// The picker stands inline in its card on a page; it is drawn here in a stand-in for that card's opened
/// body (the page's 580 flyout less its gutters, from the card's name column), the default chosen, so its
/// cross is ringed. <c>--initials</c> names the default as a view's initials, as the Views page does.
/// </para>
/// </summary>
internal static class IconPickerRender
{
    private const string Flag = "--iconpicker-png";
    private const string Scope = "iconpicker-render";

    /// <summary>The <c>--theme</c> of this render, read before the app's resources load.</summary>
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

        if (at + 1 >= args.Length || DispatcherQueue.GetForCurrentThread() is not { } dispatcher)
        {
            Log.Error(Scope, $"usage: {Flag} <out.png> [--search text] [--theme light|dark] [--initials]");
            app.Exit();
            return true;
        }

        var outPath = System.IO.Path.GetFullPath(args[at + 1]);
        var search = Option(args, "--search");
        var initials = args.Contains("--initials");

        _ = dispatcher.TryEnqueue(async () =>
        {
            Window? window = null;

            try
            {
                EditBarRender.ApplyDesignTheme(app);

                // The default chosen: its cross ringed.
                var picker = initials
                    ? new IconPicker(null, Str.Shell_View_Initials.Text(), "WK", _ => { })
                    : new IconPicker(null, null, null, _ => { });

                // A stand-in for the card's opened body on a page: its fill and rim, the picker from the name column to the end.
                var presenter = new Border
                {
                    Child = picker,
                    Width = ShellMetrics.Flyout.Cards,
                    Padding = new Thickness(ShellMetrics.Page.NameAt, ShellMetrics.Inline.Pad, ShellMetrics.Page.EndInset, ShellMetrics.Inline.Pad),
                    CornerRadius = new CornerRadius(ShellMetrics.Page.CardRadius),
                    BorderThickness = new Thickness(1),
                    BorderBrush = DesignApplier.Brush("CardStrokeColorDefaultBrush", Colors.Gray),
                    Background = DesignApplier.Brush("CardBackgroundFillColorDefaultBrush", Colors.Black),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                };

                var root = new Grid
                {
                    Padding = new Thickness(Fluent.Space4),
                    Background = DesignApplier.Brush("SolidBackgroundFillColorBaseBrush", Colors.Black),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    Children = { presenter },
                };

                window = new Window { Content = root, Title = "Themia icon picker render" };
                UiLanguage.Apply(root);
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                var dpi = Win32.GetDpiForWindow(hwnd);
                var scale = dpi > 0 ? dpi / 96.0 : 1.0;

                // Off-screen and never activated: it must not appear on the user's desktop.
                window.AppWindow.Move(new PointInt32(-32000, -32000));
                window.AppWindow.Resize(new SizeInt32((int)(700 * scale), (int)(640 * scale)));
                window.AppWindow.Show(activateWindow: false);

                // A render, not app code: the box takes text once it is loaded, and the grid
                // realises its rows on the next layout passes.
                await Task.Delay(500).ConfigureAwait(true);
                if (search is not null)
                {
                    picker.SetQuery(search);
                    await Task.Delay(500).ConfigureAwait(true);
                }

                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(outPath)!);
                await RenderPng.SaveAsync(root, outPath).ConfigureAwait(true);
                Console.WriteLine($"iconpicker: wrote {outPath}");
            }
            catch (Exception ex)
            {
                Log.Error(Scope, "rendering the icon picker failed", ex);
            }
            finally
            {
                window?.Close();
                app.Exit();
            }
        });

        return true;
    }

    private static string? Option(string[] args, string name)
    {
        var at = Array.IndexOf(args, name);
        return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
    }
}
#endif
