using System.Text.Json;
using System.Text.Json.Serialization;
using Themia.Core.Diagnostics;
using Themia.Core.Widgets;

namespace Themia.Core.Storage;

/// <summary>How a screen-time history came off disk.</summary>
public enum ScreenTimeLoadOutcome
{
    /// <summary>No file yet: a first run, and nothing worth saying.</summary>
    Fresh = 0,

    Loaded = 1,

    /// <summary>The file could not be used. It was set aside and an empty history started, loudly.</summary>
    Recovered = 2,
}

/// <summary>The on-disk shape. Every collection is nullable so "missing" can be told from "empty".</summary>
internal sealed class ScreenTimeFile
{
    public int SchemaVersion { get; set; }

    public SortedDictionary<string, SortedDictionary<string, long>>? Days { get; set; }

    public SortedDictionary<string, string>? Names { get; set; }

    /// <summary>Omitted from the bytes when empty, so absent reads as none.</summary>
    public SortedDictionary<string, string>? Paths { get; set; }

    /// <summary>Omitted when empty, on the same terms as <see cref="Paths"/>.</summary>
    public SortedDictionary<string, long>? Switches { get; set; }

    /// <summary>[S-3322] App keys every widget leaves out. Omitted when empty.</summary>
    public List<string>? Hidden { get; set; }

    /// <summary>[S-3322] The daily limit on the whole, seconds. Omitted when there is none.</summary>
    public long? GoalSeconds { get; set; }

    /// <summary>[S-3322] Daily limits per app key, seconds. Omitted when empty.</summary>
    public SortedDictionary<string, long>? AppLimits { get; set; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ScreenTimeFile))]
internal sealed partial class ScreenTimeJsonContext : JsonSerializerContext;

/// <summary>
/// [S-459] The screen-time history file, <c>usage_stats.json</c> beside the app state.
/// <para>
/// Its own file, never the state file: usage is private to this machine, and keeping it out of the
/// layout keeps it out of copy and paste, exports and anything else that moves a widget. Atomic,
/// single-writer saves; a file that cannot be read is moved aside, never overwritten, so a
/// downgrade or a bad write cannot silently destroy months of history.
/// </para>
/// </summary>
public static class ScreenTimeStore
{
    public const int SchemaVersion = 1;

    public const string FileName = "usage_stats.json";

    private const string Scope = "screentime";

    private static long _tempSequence;

    /// <summary>
    /// Read a history. Never throws for bad data: an absent file is a quiet empty history, a corrupt
    /// or version-mismatched one is set aside as <c>.corrupt</c> and reported.
    /// </summary>
    public static (ScreenTimeTracker Tracker, ScreenTimeLoadOutcome Outcome) Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string json;
        try
        {
            if (!File.Exists(path))
            {
                return (new ScreenTimeTracker(), ScreenTimeLoadOutcome.Fresh);
            }

            json = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Unreadable is not corrupt: the file may be fine and merely locked, so it stays put.
            Log.Warn(Scope, $"could not read {path}; starting with an empty history", ex);
            return (new ScreenTimeTracker(), ScreenTimeLoadOutcome.Recovered);
        }

        if (Parse(json) is { } tracker)
        {
            return (tracker, ScreenTimeLoadOutcome.Loaded);
        }

        Log.Warn(Scope, $"{path} is corrupt or from another version; starting with an empty history");
        SetAside(path);
        return (new ScreenTimeTracker(), ScreenTimeLoadOutcome.Recovered);
    }

    /// <summary>
    /// [S-459] Parse a history, or null when it is not one this build can trust: not JSON, the wrong
    /// schema version, or a required map missing. Junk inside a good envelope is dropped rather than
    /// failing the file: day keys that are not dates (they would sort outside retention and become
    /// immortal), empty app keys, empty paths and zero switch counts. Names are cleaned on the way in
    /// ([S-458]), so a corrupt name is repaired rather than shown.
    /// </summary>
    public static ScreenTimeTracker? Parse(string json)
    {
        ScreenTimeFile? file;
        try
        {
            file = JsonSerializer.Deserialize(json, ScreenTimeJsonContext.Default.ScreenTimeFile);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }

        if (file is null || file.SchemaVersion != SchemaVersion || file.Days is null || file.Names is null)
        {
            return null;
        }

        var tracker = new ScreenTimeTracker();

        foreach (var (date, per) in file.Days)
        {
            if (!LocalDay.TryParse(date, out _) || per is null)
            {
                continue;
            }

            foreach (var (app, seconds) in per)
            {
                if (!string.IsNullOrEmpty(app))
                {
                    tracker.Add(date, app, seconds);
                }
            }
        }

        foreach (var (app, name) in file.Names)
        {
            if (!string.IsNullOrEmpty(app) && AppNames.Sanitize(name) is { } clean)
            {
                tracker.Names[app] = clean;
            }
        }

        foreach (var (app, path) in file.Paths ?? [])
        {
            if (!string.IsNullOrEmpty(app) && !string.IsNullOrEmpty(path))
            {
                tracker.Paths[app] = path;
            }
        }

        foreach (var (date, count) in file.Switches ?? [])
        {
            if (LocalDay.TryParse(date, out _) && count > 0)
            {
                tracker.Switches[date] = count;
            }
        }

        // [S-3322] Repaired on the way in: blank keys and limits of zero or less are dropped.
        tracker.LoadPreferences(ScreenTimePreferences.From(file.Hidden, file.GoalSeconds ?? 0, file.AppLimits));

        tracker.Dirty = false;
        return tracker;
    }

    /// <summary>
    /// [S-459] The bytes a history is saved as, after pruning to retention. Empty optional maps are
    /// left out, so a history with no paths or switches is byte-identical to the original format.
    /// </summary>
    public static string Serialize(ScreenTimeTracker tracker)
    {
        ArgumentNullException.ThrowIfNull(tracker);

        tracker.Prune();

        var file = new ScreenTimeFile
        {
            SchemaVersion = SchemaVersion,
            Days = new SortedDictionary<string, SortedDictionary<string, long>>(StringComparer.Ordinal),
            Names = new SortedDictionary<string, string>(tracker.Names, StringComparer.Ordinal),
            Paths = tracker.Paths.Count > 0 ? new SortedDictionary<string, string>(tracker.Paths, StringComparer.Ordinal) : null,
            Switches = tracker.Switches.Count > 0 ? new SortedDictionary<string, long>(tracker.Switches, StringComparer.Ordinal) : null,
        };

        var preferences = tracker.Preferences;
        if (preferences.Hidden.Count > 0)
        {
            file.Hidden = [.. preferences.Hidden.Order(StringComparer.Ordinal)];
        }

        if (preferences.GoalSeconds > 0)
        {
            file.GoalSeconds = preferences.GoalSeconds;
        }

        if (preferences.AppLimits.Count > 0)
        {
            file.AppLimits = new SortedDictionary<string, long>(preferences.AppLimits, StringComparer.Ordinal);
        }

        foreach (var (date, per) in tracker.Days)
        {
            file.Days[date] = new SortedDictionary<string, long>(per, StringComparer.Ordinal);
        }

        return JsonSerializer.Serialize(file, ScreenTimeJsonContext.Default.ScreenTimeFile);
    }

    /// <summary>
    /// [S-459] Write the bytes atomically: a temp file with a name no other write can share, then a
    /// rename over the old one. Readers see the old history or the new one, never half of either.
    /// The caller is the single writer.
    /// </summary>
    /// <returns>False when the write failed; the failure is logged and the old file is untouched.</returns>
    public static bool WriteAtomic(string path, string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(json);

        var sequence = Interlocked.Increment(ref _tempSequence);
        var temp = $"{path}.tmp.{Environment.ProcessId}.{sequence}";

        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(temp, json);
            File.Move(temp, path, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Error(Scope, $"could not save {path}", ex);

            try
            {
                File.Delete(temp);
            }
            catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException)
            {
                Log.Warn(Scope, "could not remove the temp history file", cleanup);
            }

            return false;
        }
    }

    private static void SetAside(string path)
    {
        try
        {
            File.Move(path, path + ".corrupt", overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warn(Scope, "could not preserve the unreadable history", ex);
        }
    }
}
