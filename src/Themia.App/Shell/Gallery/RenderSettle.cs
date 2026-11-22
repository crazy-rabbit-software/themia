#if DEBUG
using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using Themia.Core.Diagnostics;
using Themia.Core.Shell;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-15900] Debug only: the one settle of every render and check, reached through <see cref="RenderHarness.SettleAsync"/>
/// and <see cref="StandInDesktop.ReadyAsync"/>. A page is looked at once a frame (the compositor's own clock, one handler
/// for the run): laid out; while anything is still loading (a bitmap under it, the render's tracked work: a replayed
/// recording's answers and the host's loads, <see cref="Track(Task)"/>; what the caller says is still busy) that is
/// waited for; then its picture is drawn small into one bitmap each frame and copied into one of two buffers kept for the
/// run, until it is the same <see cref="RenderSettling.QuietFrames"/> frames in a row (an entrance, a glide, a fade has
/// ended; [S-15901] what turns forever on purpose, an indeterminate ring or a caret, is masked out). The tree is walked
/// seldom (as it starts, after <see cref="RewalkAfter"/> changed looks, once more before it is called settled). Core's
/// <see cref="RenderSettling"/> decides. All under one ceiling: past it, what was still busy is said on stdout and in the
/// log (<c>NOT SETTLED</c>) and the picture is taken as it stands. Never a fixed sleep.
/// </summary>
internal static class RenderSettle
{
    private const string Scope = "render-settle";

    /// <summary>The longest one frame is waited for: an off-screen window the compositor stopped drawing must not hang a render.</summary>
    private static readonly TimeSpan FrameCeiling = TimeSpan.FromSeconds(2);

    /// <summary>The long side of a look, in pixels: enough to see a page move, small enough to draw every frame.</summary>
    private const double LookSide = 1024;

    /// <summary>The longest one look (a bitmap of the page) is waited for.</summary>
    private static readonly TimeSpan LookCeiling = TimeSpan.FromSeconds(10);

    /// <summary>The render's own work still running (a recording's answers on their way), each a task.</summary>
    private static readonly HashSet<Task> Work = [];

    /// <summary>The waits for the next frame (UI thread), ended by <see cref="OnRendering"/>.</summary>
    private static readonly List<TaskCompletionSource> Waiting = [];

    /// <summary>Whether the run's one frame handler is in place.</summary>
    private static bool _listening;

    /// <summary>The bitmap every look draws into (UI thread).</summary>
    private static RenderTargetBitmap? _look;

    private static int _settles;
    private static int _late;
    private static long _ticks;

    /// <summary>The settles of this run so far, those that hit their ceiling and the time spent in them.</summary>
    public static (int Settles, int Late, TimeSpan Spent) Totals => (_settles, _late, TimeSpan.FromTicks(_ticks));

    /// <summary>The run's settles said once, at its end: how many, how long they took, and how many hit their ceiling.</summary>
    public static void Report()
    {
        if (_listening)
        {
            CompositionTarget.Rendering -= OnRendering;
            _listening = false;
        }

        var (settles, late, spent) = Totals;
        if (settles + late > 0)
        {
            Console.WriteLine($"settle: {settles} settles in {spent.TotalSeconds:0.0} s, {late} not settled or not seen by their ceiling");
        }
    }

    /// <summary>Count <paramref name="work"/> as the page's until it ends; returns it.</summary>
    public static Task<T> Track<T>(Task<T> work)
    {
        Track((Task)work);
        return work;
    }

    /// <summary>Count <paramref name="work"/> as the page's until it ends (the host's loads, <see cref="Services.LoadWatch"/>).</summary>
    public static void Track(Task work)
    {
        ArgumentNullException.ThrowIfNull(work);
        if (!work.IsCompleted)
        {
            lock (Work)
            {
                Work.Add(work);
            }

            _ = work.ContinueWith(
                done =>
                {
                    lock (Work)
                    {
                        Work.Remove(done);
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }

    /// <summary>
    /// Settle <paramref name="root"/> (on the UI thread): laid out, nothing under it loading and nothing
    /// <paramref name="busy"/> says (a reason, or null when idle), and its picture unchanged frame after frame; or the
    /// <paramref name="ceiling"/> (default <see cref="RenderSettling.DefaultCeiling"/>) passed, said loudly as
    /// <paramref name="what"/>'s. Returns whether it settled.
    /// </summary>
    public static async Task<bool> SettleAsync(UIElement root, string what, TimeSpan? ceiling = null, Func<string?>? busy = null, bool looks = true)
    {
        ArgumentNullException.ThrowIfNull(root);
        var clock = Stopwatch.StartNew();
        var rule = new RenderSettling(ceiling ?? RenderSettling.DefaultCeiling);
        Look? last = null;
        (PixelBox? Box, string? Size, double Scale)? moved = null;
        Survey? survey = null;
        var changed = 0;
        var frames = 0;
        try
        {
            while (true)
            {
                root.UpdateLayout();
                await FramesAsync(1).ConfigureAwait(true);
                frames++;

                if (survey is null || changed >= RewalkAfter)
                {
                    survey = SurveyOf(root);
                    changed = 0;
                }

                var pending = Pending(busy, survey);
                bool? same = null;
                if (pending.Count == 0 && !looks)
                {
                    // A check that draws nothing: laid out, a frame drawn and nothing loading is settled.
                    same = true;
                }
                else if (pending.Count == 0 && frames < FirstLookAfter)
                {
                    // A page just built is drawn a few frames before it is looked at: no look while it first draws.
                    same = null;
                }
                else if (pending.Count == 0)
                {
                    var now = await LookAsync(root, last, survey).ConfigureAwait(true);
                    // A page that draws nothing (no size, not in a window) cannot change in a picture: quiet.
                    same = now is not { } look ? true
                        : last is not { } before ? null
                        : RenderSettling.SamePicture(before.Pixels, look.Pixels, look.Width, 4, look.Masks);
                    if (same == false && last is { } was && now is { } other)
                    {
                        // Where it moved, kept for the ceiling's line (the two looks' buffers are drawn into again).
                        changed++;
                        moved = was.Width != other.Width || was.Pixels.Length != other.Pixels.Length
                            ? (null, $"{was.Width}x{was.Pixels.Length / Math.Max(1, was.Width * 4)} px, then {other.Width}x{other.Pixels.Length / Math.Max(1, other.Width * 4)} px", other.Scale)
                            : (RenderSettling.ChangedBox(was.Pixels, other.Pixels, other.Width, 4), null, other.Scale);
                    }

                    last = now;
                }
                else
                {
                    last = null;
                }

                switch (rule.Next(clock.Elapsed, pending.Count, same))
                {
                    case SettleStep.Settled:
                        // Once more over the whole page: a bitmap that started loading since the last walk is waited for.
                        survey = SurveyOf(root);
                        if (survey.Images.TrueForAll(image => image.Done.IsCompleted))
                        {
                            return true;
                        }

                        break;
                    case SettleStep.CeilingHit:
                        var line = RenderSettling.CeilingLine(what, rule.Ceiling, pending.Select(p => p.What), pending.Count == 0 ? Moving(root, moved) : null);
                        Log.Warn(Scope, line);
                        Console.WriteLine(line);
                        Interlocked.Increment(ref _late);
                        return false;
                    case SettleStep.AwaitWork:
                        var tasks = pending.Where(p => p.Done is not null).Select(p => p.Done!).ToArray();
                        if (tasks.Length == pending.Count)
                        {
                            var left = rule.Ceiling - clock.Elapsed;
                            await Task.WhenAny(Task.WhenAll(tasks), Task.Delay(left > TimeSpan.Zero ? left : TimeSpan.Zero)).ConfigureAwait(true);
                        }

                        // Otherwise something polled is busy: looked at again next frame.
                        break;
                    case SettleStep.NextFrame:
                        break;
                }
            }
        }
        finally
        {
            Interlocked.Increment(ref _settles);
            Interlocked.Add(ref _ticks, clock.Elapsed.Ticks);
        }
    }

    /// <summary>
    /// Frame by frame until <paramref name="done"/> holds (a popup opened, a surface at rest): true then, or false past
    /// <paramref name="ceiling"/> (default <see cref="RenderSettling.DefaultCeiling"/>), said loudly as <paramref name="what"/>.
    /// </summary>
    public static async Task<bool> UntilAsync(Func<bool> done, string what, TimeSpan? ceiling = null)
    {
        ArgumentNullException.ThrowIfNull(done);
        var clock = Stopwatch.StartNew();
        var limit = ceiling ?? RenderSettling.DefaultCeiling;
        while (!done())
        {
            if (clock.Elapsed >= limit)
            {
                var line = $"settle: {what}: NOT SEEN after {limit.TotalSeconds:0.#} s; going on as it stands";
                Log.Warn(Scope, line);
                Console.WriteLine(line);
                Interlocked.Increment(ref _late);
                return false;
            }

            await FramesAsync(1).ConfigureAwait(true);
        }

        return true;
    }

    /// <summary>
    /// <paramref name="task"/> awaited, or false past <paramref name="ceiling"/>, said loudly as <paramref name="what"/>:
    /// a ready signal of the page's own (its pictures drawn, the icons quiet) under a ceiling.
    /// </summary>
    public static async Task<bool> SignalAsync(Task task, string what, TimeSpan? ceiling = null)
    {
        ArgumentNullException.ThrowIfNull(task);
        var limit = ceiling ?? RenderSettling.DefaultCeiling;
        if (await Task.WhenAny(task, Task.Delay(limit)).ConfigureAwait(true) == task)
        {
            return true;
        }

        var line = $"settle: {what}: NOT SIGNALLED after {limit.TotalSeconds:0.#} s; going on as it stands";
        Log.Warn(Scope, line);
        Console.WriteLine(line);
        Interlocked.Increment(ref _late);
        return false;
    }

    /// <summary>
    /// At least <paramref name="atLeast"/> popups open on <paramref name="xamlRoot"/> (a menu, its submenu: the frame they
    /// open on, however long the first menu of a fresh window takes), then each popup's content settled; the popups open.
    /// </summary>
    public static async Task<IReadOnlyList<Popup>> PopupsAsync(XamlRoot xamlRoot, int atLeast, string what)
    {
        ArgumentNullException.ThrowIfNull(xamlRoot);
        _ = await UntilAsync(() => VisualTreeHelper.GetOpenPopupsForXamlRoot(xamlRoot).Count >= atLeast, $"{what}: {atLeast} popup(s) open").ConfigureAwait(true);
        var popups = VisualTreeHelper.GetOpenPopupsForXamlRoot(xamlRoot);
        foreach (var popup in popups)
        {
            if (popup.Child is UIElement content)
            {
                _ = await SettleAsync(content, what).ConfigureAwait(true);
            }
        }

        return popups;
    }

    /// <summary>Every popup on <paramref name="xamlRoot"/> closed (a menu hidden), frame by frame under the ceiling.</summary>
    public static Task<bool> PopupsClosedAsync(XamlRoot xamlRoot, string what)
    {
        ArgumentNullException.ThrowIfNull(xamlRoot);
        return UntilAsync(() => VisualTreeHelper.GetOpenPopupsForXamlRoot(xamlRoot).Count == 0, $"{what}: its popups closed");
    }

    /// <summary>
    /// The UI thread's queue drained: a frame drawn, then a turn of the dispatcher at low priority, which runs after every
    /// item already posted at a higher one (a release's commit, a layout pass a change posted). For a check that reads a
    /// model after a step and draws nothing.
    /// </summary>
    public static async Task IdleAsync()
    {
        await FramesAsync(1).ConfigureAwait(true);
        var turn = new TaskCompletionSource();
        if (Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread() is not { } queue
            || !queue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () => turn.TrySetResult()))
        {
            Log.Warn(Scope, "no dispatcher to drain: one frame only");
            return;
        }

        await turn.Task.ConfigureAwait(true);
    }

    /// <summary><paramref name="count"/> frames drawn from now (the compositor's own clock), each at most <see cref="FrameCeiling"/>.</summary>
    public static async Task FramesAsync(int count)
    {
        for (var i = 0; i < count; i++)
        {
            var drawn = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Waiting.Add(drawn);
            if (!_listening)
            {
                // One handler for the run, not one added and taken away each frame.
                CompositionTarget.Rendering += OnRendering;
                _listening = true;
            }

            if (await Task.WhenAny(drawn.Task, Task.Delay(FrameCeiling)).ConfigureAwait(true) != drawn.Task)
            {
                Waiting.Remove(drawn);
                Log.Warn(Scope, $"no frame drawn in {FrameCeiling.TotalSeconds:0} s");
                Console.WriteLine($"settle: no frame drawn in {FrameCeiling.TotalSeconds:0} s");
            }
        }
    }

    /// <summary>A frame is being drawn: every wait for one ends (UI thread).</summary>
    private static void OnRendering(object? sender, object e)
    {
        if (Waiting.Count == 0)
        {
            return;
        }

        var ended = Waiting.ToArray();
        Waiting.Clear();
        foreach (var wait in ended)
        {
            wait.TrySetResult();
        }
    }

    /// <summary>Whether <paramref name="root"/> draws nothing a bitmap could hold (no size, not in a window, hidden).</summary>
    private static bool Nothing(UIElement root)
        => root is not FrameworkElement { ActualWidth: > 0, ActualHeight: > 0, XamlRoot: not null } || root.Visibility != Visibility.Visible;

    /// <summary>Where the last two looks differed (DIPs) and the elements there, deepest first: what kept the page moving.</summary>
    private static string? Moving(UIElement root, (PixelBox? Box, string? Size, double Scale)? moved)
    {
        if (moved is not { } m)
        {
            return null;
        }

        if (m.Size is { } size)
        {
            return $"(its size changing: {size})";
        }

        if (m.Box is not { } box)
        {
            return null;
        }

        var dips = new Windows.Foundation.Rect(box.X / m.Scale, box.Y / m.Scale, box.Width / m.Scale, box.Height / m.Scale);
        var names = new List<string>();
        try
        {
            var origin = root.TransformToVisual(null).TransformPoint(new Windows.Foundation.Point(0, 0));
            var hit = new Windows.Foundation.Rect(origin.X + dips.X, origin.Y + dips.Y, Math.Max(1, dips.Width), Math.Max(1, dips.Height));
            // Enumerated by hand: the WinRT collection has no generic helper for LINQ's ICollection probe.
            foreach (var element in VisualTreeHelper.FindElementsInHostCoordinates(hit, root, includeAllElements: true))
            {
                names.Add(element is FrameworkElement { Name.Length: > 0 } named ? $"{element.GetType().Name} '{named.Name}'" : element.GetType().Name);
                if (names.Count == 6)
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            // Diagnostic only: the line still says where the page moved, without the elements there.
            Log.Warn(Scope, "the elements where the page moved could not be named", ex);
        }

        return $"({dips.X:0},{dips.Y:0} {dips.Width:0}x{dips.Height:0} DIPs{(names.Count > 0 ? ": " + string.Join(" < ", names) : string.Empty)})";
    }

    /// <summary>
    /// One look at <paramref name="root"/>: its pixels (BGRA) drawn small into one of two buffers kept for the run (never the
    /// one <paramref name="last"/> holds), their width, its scale and the boxes masked; null when it draws nothing.
    /// </summary>
    private static async Task<Look?> LookAsync(UIElement root, Look? last, Survey survey)
    {
        if (Nothing(root))
        {
            return null;
        }

        try
        {
            // A look is bounded too: a bitmap the compositor never hands back is said, and the page counts as not looked at.
            // One bitmap for every look of the run, kept to its end: a new one each frame left thousands of WinRT bitmaps
            // (each holding its pixels) to the finalizer, and one of them released as the application closed crashed it.
            var bitmap = _look ??= new RenderTargetBitmap();

            // Drawn small (its long side at most LookSide pixels): a look tells whether the page still moves, the picture
            // itself is drawn full size afterwards, and a large page drawn whole each frame is what a settle would cost.
            var page = (FrameworkElement)root;
            var fit = Math.Min(1.0, LookSide / (Math.Max(page.ActualWidth, page.ActualHeight) * (page.XamlRoot?.RasterizationScale ?? 1)));
            var drawn = (fit < 1
                ? bitmap.RenderAsync(root, (int)Math.Max(1, Math.Round(page.ActualWidth * (page.XamlRoot?.RasterizationScale ?? 1) * fit)), 0)
                : bitmap.RenderAsync(root)).AsTask();
            if (await Task.WhenAny(drawn, Task.Delay(LookCeiling)).ConfigureAwait(true) != drawn)
            {
                Log.Warn(Scope, $"a look at {root.GetType().Name} took over {LookCeiling.TotalSeconds:0} s");
                Console.WriteLine($"settle: a look at the page took over {LookCeiling.TotalSeconds:0} s; not compared");
                return null;
            }

            await drawn.ConfigureAwait(true);
            if (bitmap.PixelWidth == 0 || bitmap.PixelHeight == 0)
            {
                return null;
            }

            // Copied into a buffer kept for the run, not a new array each frame: a settle draws many looks, and an array of a
            // few MB each frame kept the collector busy for nothing.
            var pixels = await bitmap.GetPixelsAsync();
            var length = (int)pixels.Length;
            ref var buffer = ref ReferenceEquals(last?.Buffer, _bufferA) ? ref _bufferB : ref _bufferA;
            if (buffer is null || buffer.Length < length)
            {
                buffer = new byte[length];
            }

            pixels.CopyTo(0, buffer, 0, length);
            var scale = bitmap.PixelWidth / page.ActualWidth;
            return new Look(buffer, length, bitmap.PixelWidth, scale, Masks(root, scale, survey));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            // A page that cannot be drawn to a bitmap right now (closing, not in a window): nothing to compare.
            Log.Debug(Scope, $"no look at {root.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    /// <summary>One look: <see cref="Length"/> bytes of <see cref="Buffer"/>, rows of <see cref="Width"/> pixels.</summary>
    private sealed record Look(byte[] Buffer, int Length, int Width, double Scale, IReadOnlyList<PixelBox> Masks)
    {
        public ReadOnlySpan<byte> Pixels => Buffer.AsSpan(0, Length);
    }

    /// <summary>The two buffers the looks are drawn into, in turn (UI thread).</summary>
    private static byte[]? _bufferA;

    private static byte[]? _bufferB;

    /// <summary>
    /// [S-15901] What the survey found turning forever on purpose, in the look's pixels: an indeterminate ring (its box and
    /// a ring's size round it) or bar (its box), and the caret of a focused text box (a few DIPs round it: it fades in and
    /// out for as long as it has focus).
    /// </summary>
    private static List<PixelBox> Masks(UIElement root, double scale, Survey survey)
    {
        var masks = new List<PixelBox>(survey.Turning.Count);
        foreach (var (part, local) in survey.Turning)
        {
            if (part.XamlRoot is null)
            {
                continue;
            }

            var box = part.TransformToVisual(root).TransformBounds(local(part));
            masks.Add(new PixelBox(
                (int)Math.Floor(box.X * scale) - 1,
                (int)Math.Floor(box.Y * scale) - 1,
                (int)Math.Ceiling(box.Width * scale) + 3,
                (int)Math.Ceiling(box.Height * scale) + 3));
        }

        return masks;
    }

    /// <summary>The frames a page draws in a settle before its first look.</summary>
    private const int FirstLookAfter = 4;

    /// <summary>The looks that changed after which the page is walked again (a ring or a caret that came since).</summary>
    private const int RewalkAfter = 8;

    /// <summary>
    /// One walk of the page's visual tree: the bitmaps still loading under it and what turns forever on purpose. A walk
    /// makes a managed wrapper of every element it passes, so a settle walks seldom: as it starts, after
    /// <see cref="RewalkAfter"/> looks that changed, and once more before it calls the page settled.
    /// </summary>
    private static Survey SurveyOf(UIElement root)
    {
        var survey = new Survey([], []);
        var seen = new HashSet<BitmapImage>(ReferenceEqualityComparer.Instance);
        void Image(BitmapImage? image)
        {
            if (image is not null && seen.Add(image) && Loading(image) is { } loading)
            {
                survey.Images.Add(($"an image ({image.UriSource})", loading));
            }
        }

        Walk(root, element =>
        {
            switch (element)
            {
                case ProgressRing { IsActive: true, IsIndeterminate: true, Visibility: Visibility.Visible, ActualWidth: > 0, ActualHeight: > 0 } ring:
                    // Its animation is drawn beyond its box (seen: a whole ring's width to the side): a ring's size round it.
                    survey.Turning.Add((ring, static r => new Windows.Foundation.Rect(-r.ActualWidth, -r.ActualHeight, 3 * r.ActualWidth, 3 * r.ActualHeight)));
                    return false;
                case ProgressBar { IsIndeterminate: true, Visibility: Visibility.Visible, ActualWidth: > 0, ActualHeight: > 0 } bar:
                    survey.Turning.Add((bar, static b => new Windows.Foundation.Rect(0, 0, b.ActualWidth, b.ActualHeight)));
                    return false;
                case TextBox { FocusState: not FocusState.Unfocused, ActualWidth: > 0, ActualHeight: > 0 } box:
                    survey.Turning.Add((box, static b => Caret((TextBox)b)));
                    return true;
                case Image { Source: BitmapImage source }:
                    Image(source);
                    return true;
                case Border { Background: ImageBrush { ImageSource: BitmapImage brushed } }:
                    Image(brushed);
                    return true;
                case Panel { Background: ImageBrush { ImageSource: BitmapImage brushed } }:
                    Image(brushed);
                    return true;
                case Control { Background: ImageBrush { ImageSource: BitmapImage brushed } }:
                    Image(brushed);
                    return true;
                case Shape { Fill: ImageBrush { ImageSource: BitmapImage brushed } }:
                    Image(brushed);
                    return true;
                default:
                    return true;
            }
        });

        return survey;
    }

    /// <summary>One walk's findings: the bitmaps loading, and what turns forever with the box it turns in.</summary>
    private sealed record Survey(List<(string What, Task Done)> Images, List<(FrameworkElement Part, Func<FrameworkElement, Windows.Foundation.Rect> Local)> Turning);

    /// <summary>Where <paramref name="box"/>'s caret stands, a few DIPs wide; its whole box when the text has no place for it yet.</summary>
    private static Windows.Foundation.Rect Caret(TextBox box)
    {
        try
        {
            var at = box.GetRectFromCharacterIndex(Math.Max(0, Math.Min(box.SelectionStart, box.Text.Length - 1)), trailingEdge: box.SelectionStart >= box.Text.Length && box.Text.Length > 0);
            if (box.Text.Length > 0 && at.Height > 0)
            {
                return new Windows.Foundation.Rect(at.X - 3, at.Y - 2, 6, at.Height + 4);
            }
        }
        catch (ArgumentException ex)
        {
            // No character to stand by (an empty box): the box's own width holds the caret.
            Log.Debug(Scope, $"no caret rect: {ex.Message}");
        }

        return new Windows.Foundation.Rect(0, 0, box.ActualWidth, box.ActualHeight);
    }

    /// <summary>What is still loading: the render's tracked work, what <paramref name="busy"/> names (polled: no task) and the survey's bitmaps.</summary>
    private static List<(string What, Task? Done)> Pending(Func<string?>? busy, Survey survey)
    {
        var pending = new List<(string, Task?)>();
        lock (Work)
        {
            foreach (var work in Work)
            {
                pending.Add(("the page's own load (an icon, a picture, a recording's answer)", work));
            }
        }

        if (busy?.Invoke() is { } reason)
        {
            pending.Add((reason, null));
        }

        foreach (var (what, done) in survey.Images)
        {
            if (!done.IsCompleted)
            {
                pending.Add((what, done));
            }
        }

        return pending;
    }

    /// <summary>Every element under <paramref name="element"/>, depth first; <paramref name="visit"/> false skips its children.</summary>
    private static void Walk(DependencyObject element, Func<DependencyObject, bool> visit)
    {
        if (!visit(element))
        {
            return;
        }

        var count = VisualTreeHelper.GetChildrenCount(element);
        for (var i = 0; i < count; i++)
        {
            Walk(VisualTreeHelper.GetChild(element, i), visit);
        }
    }

    /// <summary>The bitmaps whose load this run heard end (opened or failed): never waited for again.</summary>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<BitmapImage, Task> Loads = [];

    /// <summary>A task ending when <paramref name="image"/> opens or fails, or null when it is not loading.</summary>
    private static Task? Loading(BitmapImage image)
    {
        // A bitmap read from a URI decodes after it is set: until then it has no pixels. One set from a stream (by the
        // view's own awaited load) or already decoded has them, or never loads by a URI at all.
        if (image.UriSource is null || image.PixelWidth > 0)
        {
            return null;
        }

        if (Loads.TryGetValue(image, out var known))
        {
            return known.IsCompleted ? null : known;
        }

        var done = new TaskCompletionSource();
        void Opened(object sender, RoutedEventArgs e) => Finish();
        void Failed(object sender, ExceptionRoutedEventArgs e) => Finish();
        void Finish()
        {
            image.ImageOpened -= Opened;
            image.ImageFailed -= Failed;
            done.TrySetResult();
        }

        image.ImageOpened += Opened;
        image.ImageFailed += Failed;
        Loads.AddOrUpdate(image, done.Task);
        return done.Task;
    }
}
#endif
