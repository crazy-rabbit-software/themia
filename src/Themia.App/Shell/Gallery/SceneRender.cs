#if DEBUG
using System.Globalization;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Scenes;
using Themia.Sdk;
using Themia.Sdk.Samples;
using Themia.Sdk.WinUI;
using Windows.Foundation;
using Windows.Graphics;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-1074] Debug only: one scene of a store scenes file ([S-1075]), a whole stand-in desktop of real
/// widget cards, off-screen, to one PNG of exactly the display's size times <c>--scale</c>, whatever
/// this machine's DPI is. <c>Themia.exe --scene-png &lt;scenes.json&gt; &lt;scene&gt; &lt;out.png&gt;
/// [--lang &lt;code&gt;] [--scale &lt;n&gt;]</c>. Cards stand where the desktop would put them
/// ([S-1076]); a scene with <c>edit</c> is drawn in edit mode as the edit bar's pictures are
/// ([S-1077]); a scene with <c>taskbar</c> has Windows' taskbar stood in along the bottom ([S-2200]), a
/// <c>sample:</c> wallpaper is painted at the output's size ([S-2210]). One scene per process: its theme is the application's, chosen before the resources load.
/// Exit codes: 0 written; 2 the command line, the file, a scene, a kind, a style or the wallpaper
/// refused before anything is drawn (a sample wallpaper this build does not have too); 1 the drawing failed (a preview state or submenu a widget does
/// not have, a card that broke, a bitmap of the wrong size).
/// </summary>
internal static class SceneRender
{
    public const string Flag = "--scene-png";

    /// <summary>[S-2475] A ready-made group placed, drawn as a scene of its own: <c>--recipe-png &lt;id&gt; &lt;out.png&gt;</c>.</summary>
    public const string RecipeFlag = "--recipe-png";

    private const string RecipeUsage = $"usage: {RecipeFlag} <id> <out.png> [--states kind=state,..] [--theme light|dark] [--edit] [--lang <code>] [--scale <n>]";

    private const string Scope = "scene-render";
    private const string Usage = $"usage: {Flag} <scenes.json> <scene> <out.png> [--lang <code>] [--scale <n>]";

    private const int Drawn = 0;
    private const int Failed = 1;
    private const int Refused = 2;

    /// <summary>The output's pixels per DIP when <c>--scale</c> is not given.</summary>
    private const double DefaultScale = 2;

    /// <summary>The window the scene is laid out in, in physical pixels: smaller than most scenes on purpose (the scene is not clipped to it).</summary>
    private static readonly SizeInt32 WindowSize = new(1280, 800);

    /// <summary>The application theme can only be chosen before the resources load: the scene's, read by <c>App</c>; null when the run will refuse anyway.</summary>
    public static ApplicationTheme? RequestedTheme(string[] args)
    {
        if (Read(args, out _) is not { } job)
        {
            return null;
        }

        return job.Scene.Theme == AppTheme.Light ? ApplicationTheme.Light : ApplicationTheme.Dark;
    }

    public static bool TryRunFromCommandLine(Application app, string[] args)
    {
        if (Array.IndexOf(args, Flag) < 0 && Array.IndexOf(args, RecipeFlag) < 0)
        {
            return false;
        }

        if (Read(args, out var problem) is not { } job || DispatcherQueue.GetForCurrentThread() is not { } dispatcher)
        {
            Fail(app, Refused, problem ?? "no dispatcher on the launch thread");
            return true;
        }

        _ = dispatcher.TryEnqueue(async () =>
        {
            var code = Failed;
            Window? window = null;
            Ticker? ticker = null;
            IDisposable? machine = null;
            var cards = new List<WidgetCard>();
            try
            {
                window = new Window { Title = "Themia scene render" };
                ticker = new Ticker(dispatcher);

                // [S-1931] A store picture never shows this PC: the system widgets read the sample machine.
                machine = RenderMachine.Install(ticker, dispatcher);
                AppServices.Gallery = new Services.GalleryService(ticker, dispatcher);
                code = await DrawAsync(window, ticker, job, cards).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Log.Error(Scope, "rendering the scene failed", ex);
                Console.WriteLine($"scene: failed: {ex.Message}");
                code = Failed;
            }
            finally
            {
                foreach (var card in cards)
                {
                    card.Detach();
                }

                machine?.Dispose();
                AppServices.Gallery?.Dispose();
                AppServices.Gallery = null;
                ticker?.Dispose();
                window?.Close();
                Environment.ExitCode = code;
                app.Exit();
            }
        });

        return true;
    }

    /// <summary>What a run draws, every part checked before a window opens.</summary>
    /// <param name="SampleWhereOffered">[S-2475] A widget given no state shows its view's "sample" state where it has one (a ready-made group's picture).</param>
    private sealed record Job(SceneFile File, Scene Scene, IReadOnlyList<(SceneWidget Spec, WidgetInstance Model)> Widgets, string? Wallpaper, string OutPath, double Scale, bool SampleWhereOffered = false)
    {
        /// <summary>[S-2210] The sample picture painted as the wallpaper, or null.</summary>
        public string? SampleWallpaper => Scene.SampleWallpaper;
    }

    /// <summary>The command line, the file, the scene and its widgets, read; null and why when any is refused.</summary>
    private static Job? Read(string[] args, out string? problem)
    {
        problem = null;
        if (Array.IndexOf(args, RecipeFlag) >= 0)
        {
            return ReadRecipe(args, out problem);
        }

        var at = Array.IndexOf(args, Flag);
        if (at < 0 || at + 3 >= args.Length || args[(at + 1)..(at + 4)].Any(a => a.StartsWith("--", StringComparison.Ordinal)))
        {
            problem = Usage;
            return null;
        }

        var scale = DefaultScale;
        if (Option(args, "--scale") is { } scaleText
            && !(double.TryParse(scaleText, NumberStyles.Float, CultureInfo.InvariantCulture, out scale) && double.IsFinite(scale) && scale is >= 0.5 and <= 4))
        {
            problem = $"--scale {scaleText}: a number of pixels per DIP from 0.5 to 4";
            return null;
        }

        var path = Path.GetFullPath(args[at + 1]);
        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            problem = $"{path}: cannot be read ({ex.Message})";
            return null;
        }

        if (SceneParser.Parse(json, out var refusal) is not { } file)
        {
            problem = $"{path}: {refusal}";
            return null;
        }

        if (file.Find(args[at + 2]) is not { } scene)
        {
            problem = $"{path} has no scene \"{args[at + 2]}\" (has: {string.Join(", ", file.Scenes.Select(s => s.Name))})";
            return null;
        }

        return Finish(file, scene, path, args[at + 3], scale, sampleWhereOffered: false, out problem);
    }

    /// <summary>
    /// [S-2475] <c>--recipe-png</c>: the ready-made group as it lands, a scene of its own (ReadyMadeScene):
    /// on a sample wallpaper, one panel, no edit chrome; <c>--edit</c> draws it in edit mode, the new group
    /// selected under the bar with its page. <c>--states</c> gives a kind a preview state; a kind given none
    /// shows its "sample" state where it has one.
    /// </summary>
    private static Job? ReadRecipe(string[] args, out string? problem)
    {
        problem = null;
        var at = Array.IndexOf(args, RecipeFlag);
        if (at + 2 >= args.Length || args[(at + 1)..(at + 3)].Any(a => a.StartsWith("--", StringComparison.Ordinal)))
        {
            problem = RecipeUsage;
            return null;
        }

        if (ReadyMadeGroups.Find(args[at + 1]) is not { } recipe)
        {
            problem = $"no ready-made group \"{args[at + 1]}\" (has: {string.Join(", ", ReadyMadeGroups.All.Select(r => r.Id))})";
            return null;
        }

        var scale = DefaultScale;
        if (Option(args, "--scale") is { } scaleText
            && !(double.TryParse(scaleText, NumberStyles.Float, CultureInfo.InvariantCulture, out scale) && double.IsFinite(scale) && scale is >= 0.5 and <= 4))
        {
            problem = $"--scale {scaleText}: a number of pixels per DIP from 0.5 to 4";
            return null;
        }

        var states = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in (Option(args, "--states") ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = pair.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0)
            {
                problem = $"--states {pair}: kind=state, as tasks=tasks";
                return null;
            }

            states[parts[0]] = parts[1];
        }

        // [S-2475] The group as it stands on the desktop; --edit draws it selected under the bar, its page open.
        var theme = string.Equals(Option(args, "--theme"), "light", StringComparison.OrdinalIgnoreCase) ? AppTheme.Light : AppTheme.Dark;
        var file = ReadyMadeScene.Of(recipe, states, edit: Array.IndexOf(args, "--edit") >= 0, theme);
        return Finish(file, file.Scenes[0], Path.Combine(Environment.CurrentDirectory, "recipe.json"), args[at + 2], scale, sampleWhereOffered: true, out problem);
    }

    /// <summary>The scene's widgets built and its wallpaper found: the job, or null and why.</summary>
    private static Job? Finish(SceneFile file, Scene scene, string path, string outPath, double scale, bool sampleWhereOffered, out string? problem)
    {
        problem = null;
        var widgets = new List<(SceneWidget, WidgetInstance)>();
        foreach (var spec in scene.Widgets)
        {
            if (SceneWidgets.Build(spec, file.Screen, out var refused) is not { } model)
            {
                problem = $"{path}: {refused}";
                return null;
            }

            widgets.Add((spec, model));
        }

        // [S-2210] What knows the sample pictures refuses a name it does not have, before drawing.
        if (scene.SampleWallpaper is { } sample && !SamplePictures.WallpaperSlugs.Contains(sample, StringComparer.Ordinal))
        {
            problem = $"{path}: scene \"{scene.Name}\" names the wallpaper {Scene.SamplePrefix}{sample}, and there is no such sample picture (has: {string.Join(", ", SamplePictures.WallpaperSlugs.Select(s => Scene.SamplePrefix + s))})";
            return null;
        }

        string? wallpaper = null;
        if (scene.WallpaperFile is { } relative)
        {
            wallpaper = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, relative));
            if (!File.Exists(wallpaper))
            {
                problem = $"{path}: scene \"{scene.Name}\" names the wallpaper {relative}, and there is no {wallpaper}";
                return null;
            }
        }

        return new Job(file, scene, widgets, wallpaper, Path.GetFullPath(outPath), scale, sampleWhereOffered);
    }

    private static async Task<int> DrawAsync(Window window, Ticker ticker, Job job, List<WidgetCard> cards)
    {
        var (width, height) = (job.File.Screen.Width, job.File.Screen.Height);
        var edit = job.Scene.Edit;

        // [S-1076] The design the cards are drawn with: the scene's text, its theme the scene's
        // (the application theme is too), so the themed brushes and the design agree.
        var design = new DesignState();
        _ = DesignText.Apply(design, job.Scene.Design);
        design.Theme = job.Scene.Theme;
        DesignApplier.Apply(design);

        // The display: the wallpaper, the snap grid, the cards and their marks, the bar and its menus.
        var screen = new Grid
        {
            Width = width,
            Height = height,
            RequestedTheme = DesignApplier.Theme,
            Clip = new RectangleGeometry { Rect = new Rect(0, 0, width, height) },

            // Drawn, though it shows nothing: a bitmap covers only what its element draws, so a
            // display of no background would start at its first card. The PNG keeps the cards' alpha.
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            UseLayoutRounding = false,
        };
        UiLanguage.ApplyLanguageOnly(screen);

        // [S-1074] The output is exactly width × scale by height × scale pixels at any DPI: the
        // display is laid out in DIPs at the window's rasterisation scale and magnified by
        // scale / dpi inside a frame sized so the bitmap comes out exact. In a canvas, so a scene
        // larger than the window is neither measured down nor clipped.
        var frame = new Canvas { Children = { screen } };
        window.Content = new Canvas { Children = { frame } };

        // Off-screen and never activated: it must not appear on the user's desktop.
        window.AppWindow.Move(new PointInt32(-32000, -32000));
        window.AppWindow.Resize(WindowSize);
        window.AppWindow.Show(activateWindow: false);
        await LoadedAsync(screen).ConfigureAwait(true);

        var dpi = screen.XamlRoot?.RasterizationScale is > 0 and var raster ? raster : 1.0;
        var magnify = job.Scale / dpi;
        var place = new CompositeTransform { ScaleX = magnify, ScaleY = magnify };
        screen.RenderTransform = place;
        var step = TileStep(dpi);
        Show((0, 0, Math.Min(step, (int)Math.Round(width * job.Scale)), Math.Min(step, (int)Math.Round(height * job.Scale))));
        Console.WriteLine($"scene: {job.Scene.Name}: {width:0} x {height:0} DIPs at {job.Scale:0.##} px/DIP (window {dpi:0.##})");

        // One tile of the output: the frame that many output pixels, the display moved under it and
        // clipped to the tile in its own DIPs. The bitmap is as large as what its element's subtree
        // draws (a clip on the frame does not shrink that), so the display itself is cut to the tile.
        void Show((int X, int Y, int Width, int Height) tile)
        {
            frame.Width = tile.Width / dpi;
            frame.Height = tile.Height / dpi;
            screen.Clip = new RectangleGeometry { Rect = new Rect(tile.X / job.Scale, tile.Y / job.Scale, tile.Width / job.Scale, tile.Height / job.Scale) };
            place.TranslateX = -tile.X / dpi;
            place.TranslateY = -tile.Y / dpi;
        }

        // [S-2210] A sample wallpaper is painted at the output's pixel size into a file of this run's
        // own, used as a file wallpaper is and deleted when the run ends (after the backdrop is let go).
        var (pixelWidth, pixelHeight) = ((int)Math.Round(width * job.Scale), (int)Math.Round(height * job.Scale));
        using var sample = job.SampleWallpaper is { } slug ? await SampleWallpaperFile.WriteAsync(slug, pixelWidth, pixelHeight).ConfigureAwait(true) : null;
        var wallpaperPath = job.Wallpaper ?? sample?.FullPath;

        // [S-2119] The scene's wallpaper is the display's: the cards frost over it as on a desktop ([S-2110]).
        // In use before a card is built, until the picture is written.
        using var backdrop = wallpaperPath is { } behind ? RenderWallpaper.Use(behind, new ScreenMetrics(width, height)) : null;
        if (wallpaperPath is { } wallpaper)
        {
            screen.Children.Add(await WallpaperAsync(wallpaper, width, height).ConfigureAwait(true));
        }

        // [S-06a] The snap grid in edit mode, its lines on whole pixels of the output.
        var metrics = new ScreenMetrics(width, height);
        if (edit is { Grid: true })
        {
            var grid = new SnapGridOverlay();
            grid.Draw(metrics, job.Scale);
            screen.Children.Add(grid);
        }

        var canvas = new Canvas { Width = width, Height = height };
        screen.Children.Add(canvas);

        var view = ViewState.Create("scene");
        view.Widgets.AddRange(job.Widgets.Select(w => w.Model));

        // [S-2458] The scene's groups and the selection its edit block draws: a group whole, or one member entered.
        view.Groups.AddRange(SceneWidgets.Groups(job.Scene));
        var selection = SceneWidgets.Selection(job.Scene, view);
        var selected = selection.Ids.ToHashSet(StringComparer.Ordinal);
        var links = new Themia.Core.Links.LinkBoard();
        var markers = new EditMarkerLayer();

        // [S-1076] Each card where the desktop puts it: its whole cell, the design's gap inside it,
        // its own content scale; each on a host of its own so it replays its own preview state.
        foreach (var (spec, model) in job.Widgets)
        {
            var host = new NoticeRender.RenderCardHost(ticker)
            {
                Design = design,
                Metrics = metrics,
                EditMode = edit is not null,
                Selected = selected,
                Entered = selection.Entered,
                Links = links,
                FixtureState = spec.State,
            };
            // [S-2446] A widget is painted in its rect fitted into its sizing, as on the desktop.
            var contentScale = WidgetScale.Effective(job.Scene.Scale, model.Scale);
            var shown = SizeFit.Shown(model.Rect, SizeFit.Limits(model, metrics, contentScale, design.WidgetGapPx), metrics);
            var cell = GridGeometry.ToPixels(shown, metrics);
            var card = new WidgetCard(host, model) { Width = cell.Width, Height = cell.Height };
            card.SetLiveRect(shown);
            card.ApplyEditMode(edit is not null);
            card.ApplySelection();
            // [S-1080] The scene's display scale times the widget's own, as the desktop applies them.
            card.ApplyContentScale(contentScale);
            card.ApplyGap(design.WidgetGapPx);
            Canvas.SetLeft(card, cell.X);
            Canvas.SetTop(card, cell.Y);
            canvas.Children.Add(card);
            cards.Add(card);
            if (edit is not null)
            {
                markers.Add(card);
            }
        }

        // [S-2240] Cards that touch join, by the desktop's own path; nothing is dragged in a scene.
        WidgetCard.Join(cards, metrics, design.WidgetGapPx, job.Scale, edit is not null, _ => 0);

        if (edit is not null)
        {
            canvas.Children.Add(markers.Halos);
            canvas.Children.Add(markers.Inks);

            // [S-2453] The groups' frames, as the desktop draws them over the markers.
            var frames = new GroupFrameLayer();
            frames.Draw(GroupFrameLayer.Frames(view, cards, metrics, design.WidgetGapPx, editMode: true, selection), job.Scale);
            canvas.Children.Add(frames.Halos);
            canvas.Children.Add(frames.Inks);
        }

        // [S-1500] A confirmation a state opens is drawn inside the window, as on --widget-png.
        ConfirmFlyout.RenderInWindow = true;
        await Task.Delay(800).ConfigureAwait(true);

        if (!await ShowStatesAsync(job, cards).ConfigureAwait(true))
        {
            return Failed;
        }

        if (edit is not null && !await ComposeEditAsync(canvas, edit, view, job, metrics, design).ConfigureAwait(true))
        {
            return Failed;
        }

        await Task.Delay(800).ConfigureAwait(true);
        await RenderWallpaper.WhenLoaded().ConfigureAwait(true);

        // [S-2200] Windows' taskbar over the desktop layer, over the same frosted wallpaper as the cards.
        if (job.Scene.Taskbar)
        {
            screen.Children.Add(await TaskbarStandIn.BuildAsync(job.Scene, width, height, job.Scale, RenderWallpaper.Backdrop?.Picture).ConfigureAwait(true));
            await Task.Delay(300).ConfigureAwait(true);
        }

        if (cards.FirstOrDefault(c => c.IsFaulted) is { } broken)
        {
            Console.WriteLine($"scene: {job.Widgets[cards.IndexOf(broken)].Spec.Path}: the card broke (its error card shows); see the log");
            return Failed;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(job.OutPath)!);
        await RenderPng.SaveTiledAsync(frame, job.OutPath, pixelWidth, pixelHeight, step, async tile =>
        {
            Show(tile);
            frame.UpdateLayout();

            // A frame for the moved display to be drawn (the first tile is already showing).
            await Task.Delay(tile is (0, 0, _, _) ? 0 : 150).ConfigureAwait(true);
        }).ConfigureAwait(true);
        Console.WriteLine($"scene: wrote {job.OutPath} ({pixelWidth} x {pixelHeight})");
        return Drawn;
    }

    /// <summary>
    /// [S-1076] Each widget's preview state, as <c>--widget-png --state</c> applies it: a widget
    /// waiting for setup runs for the preview first. A state the view does not have, or a view with
    /// none, fails the run rather than drawing something half-right.
    /// </summary>
    private static async Task<bool> ShowStatesAsync(Job job, List<WidgetCard> cards)
    {
        for (var i = 0; i < cards.Count; i++)
        {
            var (spec, _) = job.Widgets[i];
            var card = cards[i];

            // [S-2475] A ready-made group's picture: a widget given no state shows its sample where it has one.
            var sample = job.SampleWhereOffered && card.View is IWidgetPreviewState offered && offered.PreviewStates.Contains("sample", StringComparer.Ordinal)
                ? "sample"
                : null;
            if ((spec.State ?? sample) is not { } state)
            {
                continue;
            }

            if (card.View is not IWidgetPreviewState preview)
            {
                Console.WriteLine($"scene: {spec.Path}.state: {spec.Kind} has no preview states");
                return false;
            }

            if (card.ShowsSetup)
            {
                card.RunForPreview();
                await Task.Delay(200).ConfigureAwait(true);
            }

            if (!preview.PreviewStates.Contains(state, StringComparer.Ordinal))
            {
                Console.WriteLine($"scene: {spec.Path}.state: {spec.Kind} has no state \"{state}\" (has: {string.Join(", ", preview.PreviewStates)})");
                return false;
            }

            await preview.ShowPreviewStateAsync(state).ConfigureAwait(true);
        }

        await Task.Delay(400).ConfigureAwait(true);
        return true;
    }

    /// <summary>
    /// [S-1077] Edit mode as the edit bar's pictures compose it: the real bar at the top centre, the
    /// selection's entry on it; the add menu hung under it; the selection's page hung from its entry, a
    /// row (by its key) opened in it.
    /// </summary>
    private static async Task<bool> ComposeEditAsync(Canvas canvas, SceneEdit edit, ViewState view, Job job, ScreenMetrics metrics, DesignState design)
    {
        var width = job.File.Screen.Width;
        var selected = job.Widgets.Where(w => job.Scene.Selected.Contains(w.Spec)).ToList();

        // [S-2458] [S-11754] The selection as the desktop holds it: a group whole, or one member entered.
        var selection = SceneWidgets.Selection(job.Scene, view);
        var barRect = new PixelRect(0, 0, 0, 0);
        EditBar? bar = null;
        if (edit.Bar)
        {
            bar = EditBarRender.SelectionBar(selected.Count > 0 ? EditBarRender.SelectionEntry(view, selection) : null, open: selected.Count > 0);
            barRect = EditBarRender.PlaceBar(canvas, bar, width);
        }

        if (edit.Add)
        {
            if (bar is null)
            {
                Console.WriteLine($"scene: {job.Scene.Name}: \"edit.add\" hangs the add menu under the bar, and \"edit.bar\" is false");
                return false;
            }

            EditBarRender.HangAddMenu(canvas, EditBarRender.AddMenu(), bar, barRect.X, barRect.Y, width);
        }

        if (selected.Count == 0)
        {
            return true;
        }

        // [S-2202] The flyout's room is the work area: above the taskbar, as on a desktop. [S-2343] The selection's page hangs from its entry.
        var bottom = job.Scene.WorkBottom(job.File.Screen);
        if (bar is null)
        {
            Console.WriteLine($"scene: {job.Scene.Name}: a selection's page hangs from the bar, and \"edit.bar\" is false");
            return false;
        }

        var page = EditBarRender.SelectionPageOf(view, selection);
        var flyout = EditBarRender.HangFlyout(canvas, page, bar, barRect, null, new PixelRect(0, 0, metrics.Width, bottom));
        page.Show();
        if (edit.Open is { } named && MemberKey(named, job) is var open && !page.DebugClick(open))
        {
            Console.WriteLine($"scene: {job.Scene.Name}: the selection's page has no row \"{open}\"");
            return false;
        }

        canvas.UpdateLayout();
        flyout.Place(FlyoutPlacement.Place(bar.EntryBox(null, canvas) ?? barRect, barRect, new PixelRect(0, 0, metrics.Width, bottom), flyout.ContentHeight));
        await Task.Delay(400).ConfigureAwait(true);

        return true;
    }

    /// <summary>
    /// [S-11755] A row key that opens a group member's own page names the member by its scene id
    /// (<c>member/facts</c>): the page names it by the widget's, which a scene does not know.
    /// </summary>
    private static string MemberKey(string open, Job job)
    {
        const string Member = "member/";
        if (!open.StartsWith(Member, StringComparison.Ordinal))
        {
            return open;
        }

        var rest = open[Member.Length..];
        var cut = rest.IndexOf('/', StringComparison.Ordinal);
        var name = cut < 0 ? rest : rest[..cut];
        return job.Widgets.FirstOrDefault(w => string.Equals(w.Spec.Id, name, StringComparison.Ordinal)) is { } widget
            ? Member + widget.Model.Id + (cut < 0 ? string.Empty : rest[cut..])
            : open;
    }

    /// <summary>
    /// The largest tile, at most <see cref="RenderPng.MaxSide"/> pixels, that is a whole number of the
    /// window's DIPs (Windows scales in quarters: 150 % is 3 pixels to 2 DIPs, so 4095), so each tile's
    /// edge falls on a pixel and comes out exactly its size.
    /// </summary>
    private static int TileStep(double dpi)
    {
        var quarters = Math.Max(1, (int)Math.Round(dpi * 4));
        var unit = quarters / (int)System.Numerics.BigInteger.GreatestCommonDivisor(quarters, 4);
        return RenderPng.MaxSide / unit * unit;
    }

    /// <summary>The wallpaper over the whole display, filled as Windows' Fill does (UniformToFill), decoded before the shot.</summary>
    private static async Task<Image> WallpaperAsync(string path, double width, double height)
    {
        var bitmap = new BitmapImage();
        using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            await bitmap.SetSourceAsync(file.AsRandomAccessStream());
        }

        return new Image { Source = bitmap, Width = width, Height = height, Stretch = Stretch.UniformToFill };
    }

    /// <summary>
    /// [S-2210] A sample wallpaper as a PNG of this run's own under the sample pictures' temp folder
    /// (<c>%TEMP%\themia-sample\wallpapers</c>), named for the process so parallel captures never share
    /// one; deleted when disposed. Nothing else there is touched.
    /// </summary>
    private sealed class SampleWallpaperFile : IDisposable
    {
        private SampleWallpaperFile(string fullPath) => FullPath = fullPath;

        public string FullPath { get; }

        public static async Task<SampleWallpaperFile> WriteAsync(string slug, int width, int height)
        {
            var png = await SamplePictureFiles.WallpaperPngAsync(slug, width, height).ConfigureAwait(true)
                ?? throw new InvalidOperationException($"no sample picture \"{slug}\"");
            var folder = Path.Combine(Path.GetTempPath(), "themia-sample", "wallpapers");
            Directory.CreateDirectory(folder);
            var file = Path.Combine(folder, $"{slug}-{width}x{height}-{Environment.ProcessId}.png");
            await File.WriteAllBytesAsync(file, png).ConfigureAwait(true);
            return new SampleWallpaperFile(file);
        }

        public void Dispose()
        {
            try
            {
                File.Delete(FullPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log.Warn(Scope, $"the sample wallpaper {FullPath} could not be deleted", ex);
            }
        }
    }

    /// <summary>Done when <paramref name="element"/> is in the live tree (it has a XamlRoot and a scale to read).</summary>
    private static Task LoadedAsync(FrameworkElement element)
    {
        if (element.IsLoaded)
        {
            return Task.CompletedTask;
        }

        var loaded = new TaskCompletionSource();
        void OnLoaded(object sender, RoutedEventArgs e)
        {
            element.Loaded -= OnLoaded;
            loaded.TrySetResult();
        }

        element.Loaded += OnLoaded;
        return loaded.Task;
    }

    private static void Fail(Application app, int code, string message)
    {
        Log.Error(Scope, message);
        Console.WriteLine($"scene: {message}");
        Environment.ExitCode = code;
        app.Exit();
    }

    private static string? Option(string[] args, string name)
    {
        var at = Array.IndexOf(args, name);
        return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
    }
}
#endif
