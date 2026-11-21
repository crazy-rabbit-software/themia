#if DEBUG
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.Core.Diagnostics;
using Themia.Core.Shell;
using Themia.Core.Widgets;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-11712] Debug only: draw the add page open under the real edit bar to a PNG, off-screen, like the
/// other debug renders. <c>Themia.exe --render picker &lt;out.png&gt; [--tab Id] [--search text] [--width w]
/// [--height h] [--hover key] [--open family:Id] [--theme light|dark] [--lang xx]</c>: <c>--tab</c> chooses a rail item by its <see cref="AddFolderId"/>
/// (Start, Time, … QuickSettings), <c>--search</c> types into the search box, <c>--width</c> and
/// <c>--height</c> size the stand-in display (1920 × 1032 DIPs: a 1080p display at 100 % with its taskbar);
/// <c>--hover</c> rests the pointer on the tile or family head of that key (<c>kind:clock</c>, <c>group:thisPc</c>,
/// <c>family:Processor</c>), its preview shown ([S-13602], [S-13614]); <c>--open</c> opens that family in place ([S-13612]).
/// Every picture the page asks for is drawn before the shot. One path with <c>--render editbar</c>'s add shots.
/// </summary>
internal static class PickerRender
{
    public static RenderCommand Command { get; } = new(
        RenderVerb.Render,
        "picker",
        "<out.png> [--tab Id] [--search text] [--width w] [--height h] [--hover key] [--open family:Id] [--theme light|dark]",
        """
        the add page open under the edit bar on a stand-in display (1920 × 1032 DIPs) [S-11712]: --tab a rail item by its id
        (Start, Time, … QuickSettings), --search typed into its box, --width/--height the display; --hover rests the pointer on
        a tile or family head (kind:clock, group:thisPc, family:Processor), its preview shown [S-13602] [S-13614];
        --open opens that family in place [S-13612]. Every picture the page asks for is drawn before the shot
        """,
        RunAsync);

    private static async Task<int> RunAsync(RenderRun run)
    {
        var outPath = run.PathArg(0);
        var search = run.Option("--search");
        AddFolderId? tab = null;
        if (run.Option("--tab") is { } name)
        {
            if (Enum.TryParse<AddFolderId>(name, ignoreCase: true, out var id) && Enum.IsDefined(id))
            {
                tab = id;
            }
            else
            {
                Console.WriteLine($"picker: no tab '{name}' (has: {string.Join(", ", Enum.GetNames<AddFolderId>())})");
            }
        }

        // What went wrong while drawing (a picture that could not be drawn) is said with the picture.
        Log.AddSink(static record =>
        {
            if (record.Level >= LogLevel.Warn || record.Scope == "add-pictures")
            {
                Console.WriteLine($"picker: {record.Level} {record.Scope}: {record.Message} {record.Detail}");
            }
        });

        var width = Number(run, "--width") ?? 1920;
        var height = Number(run, "--height") ?? 1032;

        using var harness = RenderHarness.Open(run);
        harness.PutAppThemeInForce();
        var screen = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Background = DesignApplier.Brush("SolidBackgroundFillColorTertiaryBrush", Colors.DimGray),
        };

        var window = harness.MakeWindow("Themia add page render", new Grid { Children = { screen } });
        UiLanguage.Apply(screen);
        window.Show(width, height);

        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(outPath)!);
        await EditBarRender.AddPageShot(screen, outPath, width, height, tab, search, run.Option("--hover"), run.Option("--open")).ConfigureAwait(true);
        Console.WriteLine($"picker: wrote {outPath}");
        return RenderCommandLine.Passed;
    }

    private static double? Number(RenderRun run, string name)
        => run.Option(name) is { } text && double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value) && value >= 320
            ? value
            : null;
}
#endif
