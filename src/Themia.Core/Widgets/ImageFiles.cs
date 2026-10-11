namespace Themia.Core.Widgets;

/// <summary>
/// Which files in a folder are pictures this app can draw, and which of them a tile gets.
/// <para>
/// The filter is by <b>extension</b> and deliberately conservative: it lists the formats Windows
/// Imaging Component decodes out of the box, and nothing else. Being generous here does not make a
/// picture appear — it makes a tile that spends a disk read and a decode attempt on a file that was
/// never going to open, once per file, every time the folder is scanned.
/// </para>
/// <para>
/// A pure function of a name, so it needs no disk: the scan calls it per entry while it walks, and
/// the tests call it on names no filesystem would let you create.
/// </para>
/// </summary>
public static class ImageFiles
{
    /// <summary>
    /// The extensions taken as pictures, lower case and with their dot.
    /// <para>
    /// HEIC and RAW are absent on purpose. Both need a codec from the Store that most machines do
    /// not have, so listing them would show a folder of grey tiles on the machines that lack it and
    /// no warning anywhere about why.
    /// </para>
    /// </summary>
    public static IReadOnlyList<string> Extensions { get; } =
        [".bmp", ".dib", ".gif", ".ico", ".jfif", ".jpe", ".jpeg", ".jpg", ".png", ".tif", ".tiff", ".webp"];

    private static readonly HashSet<string> Known = new(Extensions, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Whether a file name or path names a picture.
    /// <para>
    /// A name whose whole content is the extension — <c>.jpg</c> — is refused. On Windows that is a
    /// file with no name, which nothing writes deliberately; treating it as a picture is how a
    /// folder of editor leftovers ends up in a photo frame.
    /// </para>
    /// </summary>
    public static bool IsImage(string? nameOrPath)
    {
        if (string.IsNullOrWhiteSpace(nameOrPath))
        {
            return false;
        }

        var path = nameOrPath.TrimEnd();
        var lastSeparator = path.LastIndexOfAny(['\\', '/']);
        var name = lastSeparator < 0 ? path : path[(lastSeparator + 1)..];
        var dot = name.LastIndexOf('.');

        // No dot, nothing after it, or nothing before it.
        if (dot <= 0 || dot == name.Length - 1)
        {
            return false;
        }

        return Known.Contains(name[dot..]);
    }

    /// <summary>
    /// Which of the pictures found a tile actually shows, and in what order.
    /// <para>
    /// Separated from the scan because it is the whole of the "shuffle" and "how many" behaviour and
    /// the scan is I/O. A folder of fifty thousand photographs is cut to a handful <i>here</i>, so
    /// nothing downstream ever holds more than the tile asked for.
    /// </para>
    /// </summary>
    /// <param name="found">Every picture the scan turned up, in whatever order the filesystem gave.</param>
    /// <param name="shuffle">Take a random selection rather than the first by name.</param>
    /// <param name="seed">
    /// The shuffle's seed, passed in rather than taken from the clock so the choice is reproducible:
    /// each widget keeps its own ([S-3412]), and a test can pin it.
    /// </param>
    /// <param name="max">How many to keep. Always at least one.</param>
    public static List<string> Choose(IReadOnlyList<string>? found, bool shuffle, int seed, int max)
    {
        var chosen = new List<string>();

        if (found is null || found.Count == 0)
        {
            return chosen;
        }

        foreach (var path in found)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                chosen.Add(path);
            }
        }

        var wanted = Math.Max(1, max);

        if (shuffle)
        {
            // [S-3412] Ranked by a hash of each path mixed with the seed, then the front taken: every
            // picture in the folder has the same chance of being one of the ones shown (the whole
            // list is ranked, not only its first N), and the order is stable under change: a picture
            // added to or taken from the folder moves no other one, so a slideshow or grid on show
            // does not reshuffle because a file arrived.
            chosen.Sort((a, b) =>
            {
                var order = Rank(a, seed).CompareTo(Rank(b, seed));
                return order != 0 ? order : StringComparer.Ordinal.Compare(a, b);
            });
        }
        else
        {
            // Ordinal, not culture-aware: there is no culture in this process, and a stable order is
            // worth more here than a clever one.
            chosen.Sort(StringComparer.OrdinalIgnoreCase);
        }

        if (chosen.Count > wanted)
        {
            chosen.RemoveRange(wanted, chosen.Count - wanted);
        }

        return chosen;
    }

    /// <summary>A path's place in a shuffle: FNV-1a over the path, case folded, mixed with the seed (SplitMix64).</summary>
    private static ulong Rank(string path, int seed)
    {
        var hash = 14695981039346656037UL;

        foreach (var c in path)
        {
            hash ^= char.ToUpperInvariant(c);
            hash *= 1099511628211UL;
        }

        var z = hash ^ ((ulong)(uint)seed * 0x9E3779B97F4A7C15UL);
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
