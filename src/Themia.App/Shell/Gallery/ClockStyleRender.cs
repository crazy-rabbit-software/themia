#if DEBUG
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.Core.Diagnostics;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Sdk;
using Windows.Graphics;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-6016] Debug only: <c>Themia.exe --clock-style-check &lt;outDir&gt;</c>. One live clock card,
/// off-screen, handed a run of configs in turn through the card's own settings path (what a pick on
/// the properties menu does): every style after every other, both ways, with seconds and the date
/// on and off, the seconds toggled on the flip face, and the card resized between some of them. A
/// card that turns into its error card stops the run; the steps go to <c>steps.txt</c>, the last
/// face to <c>last.png</c>, and the process exits with 1 on a fault, 0 when every step held.
/// </summary>
internal static class ClockStyleRender
{
    private const string Scope = "clock-style-check";
    private const string Flag = "--clock-style-check";

    private static readonly string[] Styles = ["digital", "analog", "stacked", "flip", "word", "minimal", "date"];

    public static bool TryRunFromCommandLine(Application app, string[] args)
    {
        var at = Array.IndexOf(args, Flag);
        if (at < 0)
        {
            return false;
        }

        if (at + 1 >= args.Length || DispatcherQueue.GetForCurrentThread() is not { } dispatcher)
        {
            Log.Error(Scope, $"usage: {Flag} <outDir>");
            app.Exit();
            return true;
        }

        var outDir = Path.GetFullPath(args[at + 1]);

        _ = dispatcher.TryEnqueue(async () =>
        {
            Window? window = null;
            Ticker? ticker = null;
            WidgetCard? card = null;
            var lines = new List<string>();
            var faulted = false;

            try
            {
                Directory.CreateDirectory(outDir);

                var screen = new Grid { Width = 900, Height = 600, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
                window = new Window { Content = new Grid { Children = { screen } }, Title = "Themia clock style check" };

                // Off-screen and never activated: it must not appear on the user's desktop.
                window.AppWindow.Move(new PointInt32(-32000, -32000));
                window.AppWindow.Resize(new SizeInt32(1000, 700));
                window.AppWindow.Show(activateWindow: false);

                ticker = new Ticker(dispatcher);
                var host = new NoticeRender.RenderCardHost(ticker);
                card = new WidgetCard(host, WidgetInstance.Create(new WidgetId("clock"), new FracRect(0, 0, 0.3, 0.3)))
                {
                    Width = 360,
                    Height = 220,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                };

                screen.Children.Add(card);
                await Task.Delay(1000).ConfigureAwait(true);

                var step = 0;
                foreach (var (fragment, size) in Steps())
                {
                    step++;
                    if (size is { } wh)
                    {
                        (card.Width, card.Height) = wh;
                    }

                    card.Model.Config = StateJson.Merged(card.Model.Config, fragment);
                    card.ApplyConfig();

                    // A layout pass, so a resize reaches the view's ApplySize before the next config.
                    await Task.Delay(40).ConfigureAwait(true);

                    var line = $"{step,3} {card.Width}x{card.Height} {fragment}";
                    if (card.IsFaulted)
                    {
                        faulted = true;
                        lines.Add(line + "  FAULT (the error card)");
                        break;
                    }

                    lines.Add(line + "  ok");
                }

                lines.Add(faulted ? "clock-style-check: FAULT" : $"clock-style-check: {step} steps, no fault");
                await RenderPng.SaveAsync(screen, Path.Combine(outDir, "last.png")).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                faulted = true;
                Log.Error(Scope, "the clock style check failed", ex);
                lines.Add($"clock-style-check: failed: {ex.Message}");
            }
            finally
            {
                card?.Detach();
                ticker?.Dispose();
                window?.Close();

                foreach (var line in lines)
                {
                    Console.WriteLine(line);
                }

                await File.WriteAllLinesAsync(Path.Combine(outDir, "steps.txt"), lines).ConfigureAwait(true);
                Environment.ExitCode = faulted ? 1 : 0;
                app.Exit();
            }
        });

        return true;
    }

    /// <summary>The configs in turn: each a fragment over the last, and now and then a new size.</summary>
    private static IEnumerable<(string Fragment, (double, double)? Size)> Steps()
    {
        (double, double)[] sizes = [(360, 220), (180, 300), (640, 160), (120, 120)];

        foreach (var seconds in new[] { false, true })
        {
            foreach (var date in new[] { true, false })
            {
                var flags = $"\"showSeconds\":{(seconds ? "true" : "false")},\"showDate\":{(date ? "true" : "false")}";

                // Forward, backward, and every style to and from flip.
                foreach (var style in Styles.Concat(Styles.Reverse()))
                {
                    yield return ($$"""{"style":"{{style}}",{{flags}}}""", null);
                }

                foreach (var other in Styles)
                {
                    yield return ($$"""{"style":"flip",{{flags}}}""", null);
                    yield return ($$"""{"style":"{{other}}",{{flags}}}""", null);
                }
            }
        }

        // The flip face with the seconds toggled on and off, at each size.
        foreach (var size in sizes)
        {
            yield return ("""{"style":"flip","showSeconds":true}""", size);
            yield return ("""{"style":"flip","showSeconds":false}""", null);
            yield return ("""{"style":"digital"}""", null);
            yield return ("""{"style":"flip","showSeconds":true}""", null);
        }
    }
}
#endif
