namespace Themia.Core.SystemData;

/// <summary>
/// [S-437] Where the NVIDIA management library is loaded from, so that letting go of it really
/// unloads it.
/// <para>
/// <c>System32\nvml.dll</c> is, with a DCH driver, a small loader: it loads the real library from the
/// driver's own folder in the driver store and never lets go of it (nor of itself), so freeing the
/// loader after <c>nvmlShutdown</c> left about 16 of the 18 MB in the process. The real library loaded
/// by its own path is one module with one reference, ours, and is gone after the free. The driver's
/// folder is what the graphics kernel says for the adapter (<c>D3DKMTQueryAdapterInfo</c>,
/// <c>D3DDDI_QUERYREGISTRY_DRIVERSTOREPATH</c>: Microsoft's way for a component to find its driver's
/// files, as the Vulkan and OpenCL loaders do), never a guessed or enumerated folder. The loader in
/// System32 and the old NVSMI folder stay the fallbacks, for a driver that keeps the library there.
/// </para>
/// </summary>
public static class GpuDriverLibrary
{
    /// <summary>The library's file name, in every folder it is looked for in.</summary>
    public const string FileName = "nvml.dll";

    /// <summary>
    /// The Win32 path of a driver store folder as the graphics kernel names it
    /// (<c>\SystemRoot\System32\DriverStore\FileRepository\nv_dispig.inf_amd64_…</c>, or an NT
    /// <c>\??\C:\…</c> path, or already a drive path), or null for anything else: a relative path, a
    /// device path, one that climbs out with <c>..</c>. The kernel is a trust boundary like any other.
    /// </summary>
    public static string? Win32Path(string? kernelPath, string windowsDirectory)
    {
        ArgumentNullException.ThrowIfNull(windowsDirectory);

        var path = kernelPath?.Trim().TrimEnd('\\');

        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        const string systemRoot = @"\SystemRoot\";
        const string ntPrefix = @"\??\";

        if (path.StartsWith(systemRoot, StringComparison.OrdinalIgnoreCase))
        {
            path = windowsDirectory.TrimEnd('\\') + '\\' + path[systemRoot.Length..];
        }
        else if (path.StartsWith(ntPrefix, StringComparison.Ordinal))
        {
            path = path[ntPrefix.Length..];
        }

        var rooted = path.Length >= 3 && char.IsAsciiLetter(path[0]) && path[1] == ':' && path[2] == '\\';
        var climbs = path.Split('\\').Any(part => part == "..");

        return rooted && !climbs ? path : null;
    }

    /// <summary>
    /// The places to load the library from, in order: each adapter's driver store folder
    /// (<paramref name="driverStores"/>, Win32 paths; one entry per driver), then the loader in
    /// <paramref name="systemDirectory"/>, then the NVSMI folder an old driver used.
    /// </summary>
    public static IReadOnlyList<string> Candidates(IEnumerable<string> driverStores, string systemDirectory, string programFiles)
    {
        ArgumentNullException.ThrowIfNull(driverStores);
        ArgumentNullException.ThrowIfNull(systemDirectory);
        ArgumentNullException.ThrowIfNull(programFiles);

        var candidates = new List<string>();

        void Add(string folder)
        {
            var path = folder.TrimEnd('\\') + '\\' + FileName;
            if (!candidates.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                candidates.Add(path);
            }
        }

        foreach (var store in driverStores)
        {
            if (!string.IsNullOrWhiteSpace(store))
            {
                Add(store);
            }
        }

        Add(systemDirectory);
        Add(programFiles.TrimEnd('\\') + @"\NVIDIA Corporation\NVSMI");
        return candidates;
    }

    /// <summary>Whether a candidate is the System32 loader, which stays loaded once used ([S-437]).</summary>
    public static bool IsLoader(string candidate, string systemDirectory)
        => string.Equals(candidate, systemDirectory.TrimEnd('\\') + '\\' + FileName, StringComparison.OrdinalIgnoreCase);
}
