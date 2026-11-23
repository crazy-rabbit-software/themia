#if DEBUG
using Themia.Core.Geometry;
using Themia.Core.Shell;
using Themia.Core.Widgets;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-17224] Debug only: a gallery tab's layout file given every widget's place. <c>Themia.exe --render gallery-positions
/// &lt;tab.json&gt; [&lt;tab.json&gt; ..]</c> reads each file and writes it back (as the editor saves, <see cref="CuratedLayouts.Write"/>)
/// with every widget's <c>x</c> and <c>y</c> and its size as the gallery draws it on a 1920 by 1080 display
/// (<see cref="CuratedLayouts.Positioned"/>): a place a widget has is kept, one it lacks (a file saved before places were
/// stored, packed in reading order) the spot the packer gave it, so the tab looks as it did. A file whose every widget has its
/// place and size already is written unchanged.
/// </summary>
internal static class GalleryPositionsCommand
{
    public static RenderCommand Command { get; } = new(
        RenderVerb.Render,
        "gallery-positions",
        "<tab.json> [<tab.json> ..]",
        """
        a gallery tab's layout file (src/Themia.Core/Widgets/GalleryLayouts/<tab>.json) written back with every widget's place
        (x, y) and size as the gallery draws it at 1920 by 1080: a file of before places were stored looks as it did [S-17224]
        """,
        run =>
        {
            var files = run.Args.Where(a => !a.StartsWith("--", StringComparison.Ordinal)).Select(Path.GetFullPath).ToList();
            if (files.Count == 0)
            {
                throw run.Usage("no layout file given");
            }

            // The reference display the shipped tabs were laid on: 1920 by 1080 DIPs, its taskbar 48 at the foot, scale 1.
            var screen = new ScreenMetrics(1920, 1080) { Allowed = new FracRect(0, 0, 1, (1080 - 48) / 1080.0) };
            var (cols, rows) = GridGeometry.GridDims(screen);
            var work = new DipRect(0, 0, 1920, 1080 - 48);
            var view = WidgetGallery.Frame(work, 56, work, 1920 / (double)cols, 1080 / (double)rows).View.W;
            var sizes = new GallerySizes(screen, 1, view);
            foreach (var file in files)
            {
                var before = File.ReadAllText(file);
                var after = CuratedLayouts.Write(CuratedLayouts.Positioned(CuratedLayouts.Read(before), sizes.Curated, sizes.TitleRows));
                var temp = file + ".tmp";
                File.WriteAllText(temp, after);
                File.Move(temp, file, overwrite: true);
                Console.WriteLine($"gallery-positions: {file} {(string.Equals(before.ReplaceLineEndings("\n"), after, StringComparison.Ordinal) ? "unchanged" : "written with every widget's place")}");
            }

            return Task.FromResult(RenderCommandLine.Passed);
        });
}
#endif
