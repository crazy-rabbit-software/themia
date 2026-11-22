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
/// and <see cref="StandInDesktop.ReadyAsync"/>. A page is looked at once a frame (the compositor's own clock): laid out,
/// then while anything under it is still loading (every pending image load under it, the render's tracked work: a
/// replayed recording's answers and the host's loads, <see cref="Track(Task)"/>; what the caller says is still busy) that is waited for, then its
/// picture is drawn to a bitmap each frame until it is the same <see cref="RenderSettling.QuietFrames"/> frames in a row
/// (an entrance, a glide, a fade has ended; [S-15901] what turns forever on purpose, an indeterminate ring, is masked out).
/// Core's <see cref="RenderSettling"/> decides. All under one ceiling: past it, what was still busy is said on stdout and
/// in the log (<c>NOT SETTLED</c>) and the picture is taken as it stands. Never a fixed sleep.
/// </summary>
internal static class RenderSettle
{
    private const string Scope = "render-settle";

    /// <summary>The longest one frame is waited for: an off-screen window the compositor stopped drawing must not hang a render.</summary>
    private static readonly TimeSpan FrameCeiling = TimeSpan.FromSeconds(2);

    /// <summary>The render's own work still running (a recording's answers on their way), each a task.</summary>
    private static readonly HashSet<Task> Work = [];

    private static int _settles;
    private static int _late;
    private static long _ticks;

    /// <summary>The settles of this run so far, those that hit their ceiling and the time spent in them.</summary>
    public static (int Settles, int Late, TimeSpan Spent) Totals => (_settles, _late, TimeSpan.FromTicks(_ticks));

    /// <summary>The run's settles said once, at its end: how many, how long they took, and how many hit their ceiling.</summary>
    public static void Report()
    {
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
    public static async Task<bool> SettleAsync(UIElement root, string what, TimeSpan? ceiling = null, Func<string?>? busy = null)
    {
        ArgumentNullException.ThrowIfNull(root);
        var clock = Stopwatch.StartNew();
        var rule = new RenderSettling(ceiling ?? RenderSettling.DefaultCeiling);
        byte[]? last = null;
        try
        {
            while (true)
            {
                root.UpdateLayout();
                await FramesAsync(1).ConfigureAwait(true);

                var pending = Pending(root, busy);
                bool? same = null;
                if (pending.Count == 0)
                {
                    var now = await LookAsync(root).ConfigureAwait(true);
                    // A page that draws nothing (no size, not in a window) cannot change in a picture: quiet.
                    same = now is not { } look ? true
                        : last is null ? null
                        : RenderSettling.SamePicture(last, look.Pixels, look.Width, 4, look.Masks);
                    last = now?.Pixels;
                }
                else
                {
                    last = null;
                }

                switch (rule.Next(clock.Elapsed, pending.Count, same))
                {
                    case SettleStep.Settled:
                        return true;
                    case SettleStep.CeilingHit:
                        var line = RenderSettling.CeilingLine(what, rule.Ceiling, pending.Select(p => p.What));
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
            var drawn = new TaskCompletionSource();
            void OnRendering(object? sender, object e) => drawn.TrySetResult();
            CompositionTarget.Rendering += OnRendering;
            try
            {
                if (await Task.WhenAny(drawn.Task, Task.Delay(FrameCeiling)).ConfigureAwait(true) != drawn.Task)
                {
                    Log.Warn(Scope, $"no frame drawn in {FrameCeiling.TotalSeconds:0} s");
                    Console.WriteLine($"settle: no frame drawn in {FrameCeiling.TotalSeconds:0} s");
                }
            }
            finally
            {
                CompositionTarget.Rendering -= OnRendering;
            }
        }
    }

    /// <summary>Whether <paramref name="root"/> draws nothing a bitmap could hold (no size, not in a window, hidden).</summary>
    private static bool Nothing(UIElement root)
        => root is not FrameworkElement { ActualWidth: > 0, ActualHeight: > 0, XamlRoot: not null } || root.Visibility != Visibility.Visible;

    /// <summary>One look at <paramref name="root"/>: its pixels (BGRA), their width and the boxes masked; null when it draws nothing.</summary>
    private static async Task<(byte[] Pixels, int Width, IReadOnlyList<PixelBox> Masks)?> LookAsync(UIElement root)
    {
        if (Nothing(root))
        {
            return null;
        }

        try
        {
            var bitmap = new RenderTargetBitmap();
            await bitmap.RenderAsync(root);
            if (bitmap.PixelWidth == 0 || bitmap.PixelHeight == 0)
            {
                return null;
            }

            var pixels = (await bitmap.GetPixelsAsync()).ToArray();
            var scale = bitmap.PixelWidth / ((FrameworkElement)root).ActualWidth;
            return (pixels, bitmap.PixelWidth, Masks(root, scale));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            // A page that cannot be drawn to a bitmap right now (closing, not in a window): nothing to compare.
            Log.Debug(Scope, $"no look at {root.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    /// <summary>[S-15901] What under <paramref name="root"/> turns forever on purpose (an indeterminate ring or bar), in its pixels.</summary>
    private static List<PixelBox> Masks(UIElement root, double scale)
    {
        var masks = new List<PixelBox>();
        Walk(root, element =>
        {
            var turning = element switch
            {
                ProgressRing { IsActive: true, IsIndeterminate: true, Visibility: Visibility.Visible } => true,
                ProgressBar { IsIndeterminate: true, Visibility: Visibility.Visible } => true,
                _ => false,
            };

            if (turning && element is FrameworkElement { ActualWidth: > 0, ActualHeight: > 0 } part)
            {
                var box = part.TransformToVisual(root).TransformBounds(new Windows.Foundation.Rect(0, 0, part.ActualWidth, part.ActualHeight));
                masks.Add(new PixelBox(
                    (int)Math.Floor(box.X * scale) - 1,
                    (int)Math.Floor(box.Y * scale) - 1,
                    (int)Math.Ceiling(box.Width * scale) + 3,
                    (int)Math.Ceiling(box.Height * scale) + 3));
            }

            return !turning;
        });

        return masks;
    }

    /// <summary>What under <paramref name="root"/> is still loading, each with the task that ends when it does (none: polled).</summary>
    private static List<(string What, Task? Done)> Pending(UIElement root, Func<string?>? busy)
    {
        var pending = new List<(string, Task?)>();
        lock (Work)
        {
            pending.AddRange(Work.Select(w => ("the page's own load (an icon, a picture, a recording's answer)", (Task?)w)));
        }

        if (busy?.Invoke() is { } reason)
        {
            pending.Add((reason, null));
        }

        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        Walk(root, element =>
        {
            foreach (var image in Images(element))
            {
                if (seen.Add(image) && Loading(image) is { } loading)
                {
                    pending.Add(($"an image ({image.UriSource})", loading));
                }
            }

            return true;
        });

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

    /// <summary>The bitmaps <paramref name="element"/> draws: an image's source, an image brush's.</summary>
    private static IEnumerable<BitmapImage> Images(DependencyObject element)
    {
        if (element is Image { Source: BitmapImage source })
        {
            yield return source;
        }

        var brush = element switch
        {
            Panel panel => panel.Background,
            Border border => border.Background,
            Control control => control.Background,
            Shape shape => shape.Fill,
            _ => null,
        };

        if (brush is ImageBrush { ImageSource: BitmapImage brushed })
        {
            yield return brushed;
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
