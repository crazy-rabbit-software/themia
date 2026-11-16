namespace Themia.Core.Files;

/// <summary>
/// [S-5549] [S-1332] What one drag of items started by a view offers: the files and folders another
/// target may take, and the effects the drag is started with. One object per drag: the host and the
/// view tell their drags apart by it.
/// </summary>
public sealed class ItemDragOffer
{
    /// <param name="paths">What the drag carries; only <see cref="StoragePaths"/> of it is offered.</param>
    /// <param name="effects">The effects the drag is started with.</param>
    public ItemDragOffer(IReadOnlyList<string> paths, DropEffect effects)
    {
        ArgumentNullException.ThrowIfNull(paths);
        Paths = StoragePaths(paths);
        Effects = effects;
    }

    /// <summary>The files and folders offered to a target, as full paths; none: the drag carries only its marker, which no other app takes.</summary>
    public IReadOnlyList<string> Paths { get; }

    /// <summary>The effects the drag is started with.</summary>
    public DropEffect Effects { get; }

    /// <summary>
    /// Which of <paramref name="paths"/> a drag offers as files and folders: full file-system paths (a
    /// drive's or a share's), each once, in their order. A shell name (<c>::{CLSID}</c>, This PC, the
    /// Recycle Bin), a relative path and a blank are no storage item and are left out.
    /// </summary>
    public static IReadOnlyList<string> StoragePaths(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        return [.. paths
            .Where(path => !string.IsNullOrWhiteSpace(path) && !path.StartsWith("::", StringComparison.Ordinal) && Path.IsPathFullyQualified(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)];
    }
}

/// <summary>
/// [S-5549] The picture a drag shows under the pointer: its pixels (BGRA, premultiplied,
/// <see cref="Width"/> × <see cref="Height"/>, top row first), the pointer's place in it in pixels, and
/// how many pixels a DIP is on the display it was drawn for.
/// </summary>
public sealed record DragPicture(byte[] Bgra, int Width, int Height, int HotX, int HotY, double Scale)
{
    /// <summary>Whether there is anything to show: a size, and exactly its pixels.</summary>
    public bool Drawable => Width > 0 && Height > 0 && Bgra is not null && Bgra.Length == (long)Width * Height * 4;

    /// <summary>Pixels per DIP; 1 when <see cref="Scale"/> is no usable number.</summary>
    public double PixelsPerDip => double.IsFinite(Scale) && Scale > 0 ? Scale : 1;

    /// <summary>The picture's resolution, so its pixels are drawn one to one on its display.</summary>
    public double Dpi => 96 * PixelsPerDip;

    /// <summary>The pointer's place in the picture in DIPs: where the drag holds it, so the pointer keeps its place over the dragged items.</summary>
    public (double X, double Y) Anchor => (HotX / PixelsPerDip, HotY / PixelsPerDip);
}
