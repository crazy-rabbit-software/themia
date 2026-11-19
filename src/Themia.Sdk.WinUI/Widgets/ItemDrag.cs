using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Themia.Core.Diagnostics;
using Themia.Core.Files;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;

namespace Themia.Sdk.WinUI;

/// <summary>
/// [S-5549] [S-1332] The one way a view starts a drag of items: XAML's own drag
/// (<see cref="UIElement.StartDragAsync"/>), called inside the pointer handler with that event's
/// pointer, so the drag takes over the very press that began it.
/// <para>
/// Why not OLE's loop (<c>DoDragDrop</c>, <c>SHDoDragDrop</c>) started by us: a WinUI island gets the
/// mouse as pointer input and keeps the pressed pointer, so a loop of ours saw no mouse until the
/// button was let go; the drag began at the release and ended at the next click.
/// </para>
/// <para>
/// What it offers a target: the files and folders as storage items (which every OLE target reads as
/// <c>CF_HDROP</c> and shell items), resolved only when a target asks for them, off the UI thread, so
/// starting a drag reads no file; and always <see cref="Marker"/>, a format of ours no other app takes,
/// so a drag with no file still starts and only Themia's desktop answers it. UI thread.
/// </para>
/// </summary>
public static class ItemDrag
{
    private const string Scope = "item-drag";

    /// <summary>The format every drag started here carries, and the only one of a drag with no file.</summary>
    public const string Marker = "Themia.ItemDrag";

    /// <summary>
    /// The offer of the drag started here that still runs, or null. The surface's drop target reads what
    /// such a drag carries from it, never from the data object: that would ask the storage items of this
    /// very thread while it waits in the drop target.
    /// </summary>
    public static ItemDragOffer? Running { get; private set; }

    /// <summary>
    /// Start the drag of <paramref name="offer"/> from <paramref name="element"/> with the pointer of
    /// the pointer event being handled (<paramref name="point"/>, in the element's own coordinates):
    /// call it inside that handler, before any await. <paramref name="picture"/> is what follows the
    /// pointer ([S-5564]: the ghost, <see cref="Ghost"/>; null, or a null result: the element as it is
    /// drawn). It is asked for here, before the drag is started, and it answers at once (it composes
    /// pixels its view already holds, it renders nothing), so XAML's <c>DragStarting</c> only hands the
    /// finished bitmap over: no deferral, no wait between the drag beginning and its picture. Completes
    /// when the drag is dropped or cancelled, with what the target did. Throws what XAML throws when the drag cannot start (the
    /// pointer is no longer down); the caller logs it and clears its own state.
    /// <para>
    /// **It ends on every way out** ([S-5549]): XAML's own completion; its <c>DropCompleted</c> on the
    /// element, a second signal of the same end; and the element leaving the tree while the drag runs
    /// (a drop that moved its icon to another display's view takes away the very view that started it,
    /// and an operation whose source is gone has nothing to complete on). The host's log showed drags
    /// that never ended in normal use; which of these it was is not known from reading, so whichever
    /// comes first ends the drag, the others are then ignored, and each step is traced at debug level
    /// (asked, starting, ended by what) for the next occurrence.
    /// </para>
    /// </summary>
    public static async Task<DropEffect> StartAsync(UIElement element, PointerPoint point, ItemDragOffer offer, Func<DragPicture?>? picture = null)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(point);
        ArgumentNullException.ThrowIfNull(offer);

        var ended = new TaskCompletionSource<(DropEffect Effect, string How)>();
        var started = false;
        var asked = System.Diagnostics.Stopwatch.GetTimestamp();
        var drawn = Draw(picture);
        var composed = System.Diagnostics.Stopwatch.GetElapsedTime(asked);

        void OnStarting(UIElement sender, DragStartingEventArgs args)
        {
            started = true;
            Fill(args, offer, drawn);

            // The one wait left is XAML's own, between StartDragAsync and this event; it is measured here.
            Log.Debug(Scope, $"drag: starting (XAML asked for its data); picture {(drawn is null ? "none" : "set")} "
                + $"{System.Diagnostics.Stopwatch.GetElapsedTime(asked).TotalMilliseconds:0.0} ms after the drag was asked for, composed in {composed.TotalMilliseconds:0.0} ms");
        }

        void OnCompleted(UIElement sender, DropCompletedEventArgs args)
            => ended.TrySetResult(((DropEffect)(uint)args.DropResult, "DropCompleted"));

        void OnUnloaded(object sender, RoutedEventArgs args)
            => ended.TrySetResult((DropEffect.None, "its source left the tree"));

        element.DragStarting += OnStarting;
        element.DropCompleted += OnCompleted;
        var framework = element as FrameworkElement;
        if (framework is not null)
        {
            framework.Unloaded += OnUnloaded;
        }

        Running = offer;
        RunningPicture = drawn?.Picture;
        try
        {
            // [S-13025] The window the drag starts from is the foreground window before OLE's loop runs, or that loop sees no
            // mouse: the first drag after another app died and needed a second press.
            TakeForeground(element);
            Log.Debug(Scope, $"drag: asked of XAML ({offer.Paths.Count} file(s), {offer.Effects})");

            // DataPackageOperation's bits are the drop effects'. Not awaited alone: see above.
            _ = Observe(element.StartDragAsync(point).AsTask(), ended);
            var (effect, how) = await ended.Task.ConfigureAwait(true);
            Log.Debug(Scope, $"drag: ended by {how} ({effect}{(started ? string.Empty : ", never started")})");
            return effect;
        }
        finally
        {
            element.DragStarting -= OnStarting;
            element.DropCompleted -= OnCompleted;
            if (framework is not null)
            {
                framework.Unloaded -= OnUnloaded;
            }

            if (ReferenceEquals(Running, offer))
            {
                Running = null;
                RunningPicture = null;
            }
        }
    }

    /// <summary>
    /// [S-13026] The picture of the drag started here that still runs (what <see cref="Running"/> shows under the pointer),
    /// or null: the desktop draws it itself while the drag is over one of its surfaces, so it never goes while a fence there
    /// shows where the items would land.
    /// </summary>
    public static DragPicture? RunningPicture { get; private set; }

    /// <summary>[S-13025] The host's way to make the window a drag starts from the foreground window (by its handle); none in a render.</summary>
    private static Func<nint, bool>? _foreground;

    /// <summary>[S-13025] The host's foreground for a drag's window. Once per process, before any drag.</summary>
    internal static void InstallForeground(Func<nint, bool> foreground)
    {
        ArgumentNullException.ThrowIfNull(foreground);
        if (Interlocked.CompareExchange(ref _foreground, foreground, null) is not null)
        {
            throw new InvalidOperationException("the drag's foreground is installed once per process");
        }
    }

    /// <summary>[S-13025] Ask the host to make <paramref name="element"/>'s window the foreground window; what it could not do it logs.</summary>
    private static void TakeForeground(UIElement element)
    {
        if (Volatile.Read(ref _foreground) is not { } take || element.XamlRoot?.ContentIslandEnvironment is not { } environment)
        {
            return;
        }

        try
        {
            var window = Microsoft.UI.Win32Interop.GetWindowFromWindowId(environment.AppWindowId);
            if (window != 0 && !take(window))
            {
                Log.Info(Scope, "drag: its window could not take the foreground; the drag may not follow the mouse until it does");
            }
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "drag: taking the foreground for it threw", ex);
        }
    }

    /// <summary>XAML's own completion of the drag, handed to <paramref name="ended"/>: its result, or what it threw.</summary>
    private static async Task Observe(Task<DataPackageOperation> operation, TaskCompletionSource<(DropEffect Effect, string How)> ended)
    {
        try
        {
            var result = await operation.ConfigureAwait(true);
            ended.TrySetResult(((DropEffect)(uint)result, "StartDragAsync"));
        }
        catch (Exception ex)
        {
            // Thrown to the caller where the drag is still its own; after another end, only noted.
            if (!ended.TrySetException(ex))
            {
                Log.Debug(Scope, $"drag: XAML's operation failed after the drag had ended ({ex.GetType().Name})");
            }
        }
    }

    /// <summary>
    /// Put <paramref name="offer"/> on <paramref name="data"/>: the marker, the effects, and its files
    /// and folders as storage items a target is given when it asks.
    /// </summary>
    internal static void Offer(DataPackage data, ItemDragOffer offer)
    {
        data.RequestedOperation = (DataPackageOperation)(uint)offer.Effects;
        data.SetData(Marker, Marker);

        var paths = offer.Paths;
        if (paths.Count > 0)
        {
            data.SetDataProvider(StandardDataFormats.StorageItems, request => Provide(request, paths));
        }
    }

    /// <summary>XAML's event: nothing thrown here may reach it, a drag that cannot be filled is cancelled.</summary>
    private static void Fill(DragStartingEventArgs args, ItemDragOffer offer, (SoftwareBitmap Bitmap, Point Anchor, DragPicture Picture)? drawn)
    {
        try
        {
            args.AllowedOperations = (DataPackageOperation)(uint)offer.Effects;
            Offer(args.Data, offer);
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "a drag could not be filled; it is cancelled", ex);
            args.Cancel = true;
            return;
        }

        if (drawn is not { } picture)
        {
            return;
        }

        try
        {
            // Not disposed: the drag's UI keeps the bitmap for as long as the drag runs.
            args.DragUI.SetContentFromSoftwareBitmap(picture.Bitmap, picture.Anchor);
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, "the drag's picture could not be set; the drag shows the pressed item instead", ex);
        }
    }

    /// <summary>The drag's picture as the bitmap XAML's drag takes, the pointer's place in it and the picture itself, made before the drag starts; null for none, or one that could not be made (logged).</summary>
    private static (SoftwareBitmap Bitmap, Point Anchor, DragPicture Picture)? Draw(Func<DragPicture?>? picture)
    {
        if (picture is null)
        {
            return null;
        }

        try
        {
            if (picture() is not { Drawable: true } drawn)
            {
                return null;
            }

            var bitmap = SoftwareBitmap.CreateCopyFromBuffer(drawn.Bgra.AsBuffer(), BitmapPixelFormat.Bgra8, drawn.Width, drawn.Height, BitmapAlphaMode.Premultiplied);
            bitmap.DpiX = drawn.Dpi;
            bitmap.DpiY = drawn.Dpi;
            var (x, y) = drawn.Anchor;
            return (bitmap, new Point(x, y), drawn);
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, "the drag's picture could not be drawn; the drag shows the pressed item instead", ex);
            return null;
        }
    }

    // ---- the ghost ([S-5564], [S-5565]) -------------------------------------------------------------

    /// <summary>The host's text renderer for the badge's number: its words at a pixel height, as coverage.</summary>
    private static Func<string, int, GhostInk>? _ink;

    /// <summary>The host's renderer of the badge's number (real text, drawn at once, no XAML). Once per process, before any drag.</summary>
    internal static void InstallInk(Func<string, int, GhostInk> ink)
    {
        ArgumentNullException.ThrowIfNull(ink);
        if (Interlocked.CompareExchange(ref _ink, ink, null) is not null)
        {
            throw new InvalidOperationException("the badge's text renderer is installed once per process");
        }
    }

    /// <summary>
    /// [S-5564] An item's icon as its view holds it, for the ghost: the pixels of the bitmap an
    /// <c>Image</c> shows (the host's icons are writeable bitmaps, premultiplied BGRA), read at once.
    /// Null for no picture, or one of another kind (its view keeps a rendered copy instead).
    /// </summary>
    public static GhostIcon? IconOf(Microsoft.UI.Xaml.Media.ImageSource? source)
    {
        if (source is not Microsoft.UI.Xaml.Media.Imaging.WriteableBitmap { PixelWidth: > 0, PixelHeight: > 0 } bitmap)
        {
            return null;
        }

        try
        {
            var icon = new GhostIcon(bitmap.PixelWidth, bitmap.PixelHeight, bitmap.PixelBuffer.ToArray());
            return icon.Drawable ? icon : null;
        }
        catch (Exception ex)
        {
            Log.Debug(Scope, $"an icon's pixels could not be read for the drag's picture ({ex.GetType().Name})");
            return null;
        }
    }

    /// <summary>
    /// [S-5564] [S-5565] The ghost of a drag of <paramref name="count"/> items: <paramref name="icons"/>
    /// are the pictures at hand, the grabbed item's first; each is drawn in a square
    /// <paramref name="iconSide"/> DIPs a side, the pointer holding the front one at
    /// <paramref name="grab"/> (from that icon's top left, in DIPs), at <paramref name="scale"/> pixels a
    /// DIP. The badge is Windows' accent colour with its number in white, as Explorer's. Composed here and
    /// now from pixels (<see cref="DragGhost"/>): nothing is rendered. Null when there is nothing to show.
    /// </summary>
    public static DragPicture? Ghost(IReadOnlyList<GhostIcon> icons, int count, double iconSide, Point grab, double scale)
    {
        ArgumentNullException.ThrowIfNull(icons);
        if (!double.IsFinite(scale) || scale <= 0)
        {
            scale = 1;
        }

        var ink = default(GhostInk);
        if (DragGhost.BadgeText(count) is { } text && Volatile.Read(ref _ink) is { } draw)
        {
            ink = draw(text, Math.Max(1, (int)Math.Round(DragGhost.BadgeTextHeight * scale)));
        }

        if (DragGhost.Plan(count, icons.Count, iconSide, grab.X, grab.Y, ink.Drawable ? ink.Width / scale : 0) is not { } plan)
        {
            return null;
        }

        var accent = Accent.GetColorValue(Windows.UI.ViewManagement.UIColorType.Accent);
        return DragGhost.Compose(plan, icons, scale, Themia.Core.Widgets.SelectionLook.DragOpacity, new GhostColour(accent.R, accent.G, accent.B), BadgeText, ink);
    }

    /// <summary>The badge's number: white on the accent in both modes, as Explorer's.</summary>
    private static readonly GhostColour BadgeText = new(255, 255, 255);

    /// <summary>Windows' own colours: the accent the badge is filled with.</summary>
    private static readonly Windows.UI.ViewManagement.UISettings Accent = new();

    /// <summary>
    /// A target asks for the files: each path as its storage item, looked up on the thread pool (the
    /// asking target may be waiting on the UI thread). A path that is gone is left out and logged; with
    /// none left the request stays unanswered, which the target sees as no files.
    /// </summary>
    private static void Provide(DataProviderRequest request, IReadOnlyList<string> paths)
    {
        var deferral = request.GetDeferral();
        _ = Task.Run(async () =>
        {
            try
            {
                var items = new List<IStorageItem>(paths.Count);
                foreach (var path in paths)
                {
                    if (await ResolveAsync(path).ConfigureAwait(false) is { } item)
                    {
                        items.Add(item);
                    }
                }

                if (items.Count > 0)
                {
                    // Typed, so the collection crosses to WinRT as a list of storage items under Native AOT too.
                    IEnumerable<IStorageItem> offered = items;
                    request.SetData(offered);
                }
            }
            catch (Exception ex)
            {
                Log.Error(Scope, "a drag's files could not be handed to the target", ex);
            }
            finally
            {
                deferral.Complete();
            }
        });
    }

    private static async Task<IStorageItem?> ResolveAsync(string path)
    {
        try
        {
            return Directory.Exists(path)
                ? await StorageFolder.GetFolderFromPathAsync(path)
                : await StorageFile.GetFileFromPathAsync(path);
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, $"a dragged item is no longer there or cannot be read ({ex.GetType().Name}); it is left out of the drop");
            return null;
        }
    }
}
