using Themia.App.Services;
using Themia.Sdk;
using Themia.Sdk.Samples;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// The sample day ([S-11850], [S-1917], [S-2056]): a fixed month of sample usage to the sample moment — ten apps, quieter weekends, the morning so far today, under a
/// limit on the whole day and one on Edge (<c>over</c>: tight limits, the readouts over them) — so <c>--widget-png ScreenTimeDonut --state
/// sample</c> draws what a month of tracking would. <c>hovered</c> and <c>selected</c> light an app
/// through the render's one board ([S-3300]), so every card on the page shows the link.
/// <c>packaged</c> lists the packaged (Store) apps running on this machine by their real executables,
/// so their icons go through the one icon path ([S-3960]) as a tracked app's do.
/// </summary>
public sealed class SampleScreenTime : IScreenTimeSource
{
    public const string HoveredApp = "code";
    public const string SelectedApp = "edge";

    private static readonly (string Key, string Name, int Minutes, string? Path)[] SampleApps =
    [
        ("code", "Visual Studio Code", 95, null),
        ("edge", "Microsoft Edge", 45, null),
        ("teams", "Microsoft Teams", 40, null),
        ("outlook", "Outlook", 30, null),
        ("figma", "Figma", 22, null),
        ("spotify", "Spotify", 15, null),
        ("explorer", "File Explorer", 12, null),
        ("slack", "Slack", 9, null),
        ("terminal", "Windows Terminal", 7, null),
        ("discord", "Discord", 4, null),
    ];

    private readonly (string Key, string Name, int Minutes, string? Path)[] _apps;

    private readonly ScreenTimePreferences _preferences;

    public SampleScreenTime(string state = "sample")
    {
        _apps = state == "packaged" ? RunningPackagedApps() : SampleApps;

        // [S-2056] The sample is under its limits (six hours a day, an hour of Edge); "over" is
        // the same morning against tight ones (ninety minutes, a quarter of an hour of Edge).
        _preferences = state == "over"
            ? ScreenTimePreferences.From(null, 90 * 60, [new("edge", 15 * 60)])
            : ScreenTimePreferences.From(null, 6 * 3600, [new("edge", 3600)]);
    }

    public static IReadOnlyList<string> States { get; } = ["sample", "hovered", "selected", "over", "packaged"];

    /// <summary>[S-2050] The sample day.</summary>
    public LocalDay Today => LocalDay.From(SampleDay.Now);

    public ScreenTimePreferences Preferences => _preferences;

    public event Action? Changed
    {
        add { }
        remove { }
    }

    public void SetPreferences(ScreenTimePreferences preferences)
    {
        // ignore: a render's sample history is fixed
    }

    public ScreenTimeDay Day(string? date) => DayOf(date is not null && LocalDay.TryParse(date, out var day) ? day : Today);

    public IReadOnlyList<ScreenTimeDay> Range(int days)
        => [.. Enumerable.Range(0, Math.Max(0, days)).Select(back => DayOf(Today.AddDays(back - days + 1)))];

    /// <summary>One day: the same apps every day, scaled by a weekday factor and a little drift per app, so bars differ.</summary>
    private ScreenTimeDay DayOf(LocalDay day)
    {
        var offset = day.DaysUntil(Today);
        var weekend = day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

        // Today is the morning so far (since half past eight): about a third of a day's use.
        var scale = offset == 0 ? 0.42 : (weekend ? 0.35 : 1.0) * (0.75 + (0.25 * Math.Abs(Math.Sin(offset * 1.7))));

        List<ScreenTimeApp> apps = [.. _apps.Select((app, i) => new ScreenTimeApp(
            app.Key,
            app.Name,
            (long)(app.Minutes * 60 * scale * (0.8 + (0.4 * Math.Abs(Math.Cos((offset + i) * 0.9))))),
            app.Path))];

        return new ScreenTimeDay(day.Key, apps.Sum(a => a.Seconds), apps, 40);
    }

    /// <summary>
    /// The packaged apps running now (their executables under WindowsApps, read the way the tracker
    /// reads a foreground app's), one per executable, and Explorer as an unpackaged one beside them.
    /// </summary>
    private static (string Key, string Name, int Minutes, string? Path)[] RunningPackagedApps()
    {
        var found = new List<(string, string, int, string?)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var processes = System.Diagnostics.Process.GetProcesses();

        try
        {
            foreach (var process in processes)
            {
                if (ProcessPaths.ExePath((uint)process.Id) is not { } path
                    || PackagedApps.PackageOf(path) is null
                    || AppNames.ExeStem(path) is not { } stem
                    || !seen.Add(stem))
                {
                    continue;
                }

                found.Add((stem.ToLowerInvariant(), ProcessPaths.DisplayName(path) ?? stem, 0, path));
            }
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }

        // The apps a person uses first (WhatsApp, whose executable has no icon, leads), then the rest.
        string[] first = ["whatsapp.root", "spotify", "olk", "claude", "calculatorapp", "video.ui"];
        var chosen = found
            .OrderBy(app => Array.IndexOf(first, app.Item1) is var at and >= 0 ? at : first.Length)
            .ThenBy(app => app.Item1, StringComparer.Ordinal)
            .Take(7)
            .Select((app, i) => (app.Item1, app.Item2, 120 - (i * 15), app.Item4))
            .ToList();

        var explorer = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
        chosen.Add(("explorer", "File Explorer", 10, explorer));
        return [.. chosen];
    }
}
