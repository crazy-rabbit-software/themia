#if DEBUG
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Shell;
using Themia.Core.Storage;
using Themia.Sdk;
using Windows.Graphics;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-6016] Debug only: <c>Themia.exe --check clock-style &lt;outDir&gt;</c>. One live clock card,
/// off-screen, handed a run of configs in turn through the card's own settings path (what a pick on
/// the selection's page does): every style after every other, both ways, with seconds on and off, the seconds toggled on the flip face, and the card resized between some of them. A
/// card that turns into its error card fails the step and stops the run; the steps go to <c>steps.txt</c>, the last
/// face to <c>last.png</c>, PASS or FAIL.
/// </summary>
internal static class ClockStyleRender
{
    private const string Scope = "clock-style-check";

    private static readonly string[] Styles = ["digital", "analog", "stacked", "flip", "word", "minimal"];

    public static RenderCommand Command { get; } = new(
        RenderVerb.Check,
        "clock-style",
        "<outDir>",
        """
        one live clock card handed every style after every other, both ways, seconds on and off, resized between some,
        through the card's own settings path: a step that turns it into its error card fails, PASS or FAIL [S-6016]
        writes steps.txt and the last face, last.png
        """,
        run => RunAsync(run, run.PathArg(0)));

    private static async Task<int> RunAsync(RenderRun run, string outDir)
    {
        var report = new CheckReport(Scope, Path.Combine(outDir, "steps.txt"));
        try
        {
            Directory.CreateDirectory(outDir);
            var screen = new Grid { Width = 900, Height = 600, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            using var harness = RenderHarness.Open(run, "Themia clock style check", new Grid { Children = { screen } }, screen.Width, screen.Height);
            harness.Window.Window.AppWindow.Resize(new SizeInt32(1000, 700));
            var host = new NoticeRender.RenderCardHost(harness.Ticker);
            var card = harness.Track(new WidgetCard(host, WidgetInstance.Create(new WidgetId("clock"), new FracRect(0, 0, 0.3, 0.3)))
            {
                Width = 360,
                Height = 220,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            });

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
                if (!report.Expect(!card.IsFaulted, $"{step,3} {card.Width}x{card.Height} {fragment}" + (card.IsFaulted ? "  (the error card)" : string.Empty)))
                {
                    break;
                }
            }

            report.Say($"clock-style-check: {step} steps");
            await RenderHarness.SaveAsync(screen, Path.Combine(outDir, "last.png"), Scope).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            report.Crash(ex);
        }

        return await report.FinishAsync().ConfigureAwait(true);
    }

    /// <summary>The configs in turn: each a fragment over the last, and now and then a new size.</summary>
    private static IEnumerable<(string Fragment, (double, double)? Size)> Steps()
    {
        (double, double)[] sizes = [(360, 220), (180, 300), (640, 160), (120, 120)];

        foreach (var seconds in new[] { false, true })
        {
            var flags = $"\"showSeconds\":{(seconds ? "true" : "false")}";

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
