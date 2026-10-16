using System.Globalization;
using Themia.Core.Diagnostics;

namespace Themia.Core.Storage;

/// <summary>
/// [S-890] The crash-loop guard: counts starts that never reached a stable desktop, and says when
/// to start in safe mode instead (widgets paused, a notice offering to start them or reset).
/// <para>
/// The counter is bumped on disk <b>before</b> anything that could crash runs, and cleared once
/// the surfaces have laid their widgets out. A normal start therefore never accrues; a start that
/// dies while building the desktop leaves its bump behind. After <see cref="Threshold"/> such starts
/// in a row — this one included — the next is safe.
/// </para>
/// <para>
/// The guard must never be the thing that breaks a start, so it fails open: a counter that cannot
/// be read or written means a normal start (logged). A corrupt counter is treated as absent
/// (logged as corrupt): it is a few bytes of our own, and nothing is lost by starting it over.
/// </para>
/// </summary>
public sealed class BootGuard
{
    /// <summary>[S-890] Consecutive starts that never got stable before safe mode, as in the original.</summary>
    public const int Threshold = 3;

    /// <summary>The counter's file name, beside the state file.</summary>
    public const string FileName = "boot_attempts";

    private const string Scope = "boot";

    public BootGuard(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        Path = path;
    }

    public string Path { get; }

    /// <summary>
    /// Count this start. Returns true when it should be a safe one: the previous
    /// <see cref="Threshold"/> - 1 starts never reached <see cref="MarkStable"/>.
    /// </summary>
    public bool Begin()
    {
        var attempts = Read() + 1;

        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path) ?? ".");
            var temp = Path + ".tmp";
            File.WriteAllText(temp, attempts.ToString(CultureInfo.InvariantCulture));
            AtomicFile.Replace(temp, Path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warn(Scope, "could not record this start; the crash-loop guard is off for it", ex);
            return false;
        }

        if (attempts >= Threshold)
        {
            Log.Warn(Scope, $"start {attempts} in a row that never reached a stable desktop; starting in safe mode");
            return true;
        }

        return false;
    }

    /// <summary>The desktop is up and its widgets are laid out: this start counts as good.</summary>
    public void MarkStable()
    {
        try
        {
            File.Delete(Path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A counter left behind trips safe mode on a later start that was fine; say so.
            Log.Warn(Scope, "could not clear the start counter", ex);
        }
    }

    /// <summary>How many unfinished starts are on record: 0 when there is no counter (or it is unreadable).</summary>
    public int Read()
    {
        string text;
        try
        {
            if (!File.Exists(Path))
            {
                return 0;
            }

            text = File.ReadAllText(Path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warn(Scope, "could not read the start counter; treating it as zero", ex);
            return 0;
        }

        if (int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value is >= 0 and < 1000)
        {
            return value;
        }

        Log.Warn(Scope, "the start counter is corrupt; starting it over");
        return 0;
    }
}
