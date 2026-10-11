#if DEBUG
using Themia.App.Services;
using Themia.App.Shell.Gallery;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917]): a fixed month of sample usage — ten apps, quieter weekends, a
/// limit on the whole day and one app over its own — so <c>--widget-png ScreenTimeDonut --state
/// sample</c> draws what a month of tracking would. <c>hovered</c> and <c>selected</c> light an app
/// through the render's one board ([S-3300]), so every card on the page shows the link.
/// </summary>
internal sealed class SampleScreenTime : IScreenTimeSource
{
    public const string HoveredApp = "code";
    public const string SelectedApp = "chrome";

    private static readonly (string Key, string Name, int Minutes)[] Apps =
    [
        ("code", "Visual Studio Code", 190),
        ("chrome", "Google Chrome", 95),
        ("teams", "Microsoft Teams", 70),
        ("outlook", "Outlook", 45),
        ("spotify", "Spotify", 30),
        ("explorer", "File Explorer", 22),
        ("slack", "Slack", 18),
        ("terminal", "Windows Terminal", 14),
        ("figma", "Figma", 9),
        ("discord", "Discord", 6),
    ];

    private readonly ScreenTimePreferences _preferences = ScreenTimePreferences.From(null, 6 * 3600, [new("chrome", 3600)]);

    public static IReadOnlyList<string> States { get; } = ["sample", "hovered", "selected"];

    public LocalDay Today => LocalDay.From(DateTimeOffset.Now);

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
    private static ScreenTimeDay DayOf(LocalDay day)
    {
        var offset = day.DaysUntil(LocalDay.From(DateTimeOffset.Now));
        var weekend = day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
        var scale = (weekend ? 0.35 : 1.0) * (0.75 + (0.25 * Math.Abs(Math.Sin(offset * 1.7))));

        List<ScreenTimeApp> apps = [.. Apps.Select((app, i) => new ScreenTimeApp(
            app.Key,
            app.Name,
            (long)(app.Minutes * 60 * scale * (0.8 + (0.4 * Math.Abs(Math.Cos((offset + i) * 0.9))))),
            null))];

        return new ScreenTimeDay(day.Key, apps.Sum(a => a.Seconds), apps, 40);
    }
}

internal sealed partial class ScreenTimeDonutView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates => SampleScreenTime.States;

    public Task ShowPreviewStateAsync(string state)
    {
        UseSource(new SampleScreenTime());
        ScreenTimePreviewStates.Apply(_links, state);
        Render();
        return Task.CompletedTask;
    }
}

internal sealed partial class ScreenTimeAppsView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates => SampleScreenTime.States;

    public Task ShowPreviewStateAsync(string state)
    {
        UseSource(new SampleScreenTime());
        ScreenTimePreviewStates.Apply(_links, state);
        Render();
        return Task.CompletedTask;
    }
}

internal sealed partial class ScreenTimeChartView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates => SampleScreenTime.States;

    public Task ShowPreviewStateAsync(string state)
    {
        UseSource(new SampleScreenTime());
        ScreenTimePreviewStates.Apply(_links, state);
        Render();
        return Task.CompletedTask;
    }
}

/// <summary>The linked states a render can ask for.</summary>
internal static class ScreenTimePreviewStates
{
    public static void Apply(ScreenTimeLinks links, string state)
    {
        switch (state)
        {
            case "hovered":
                links.Hover(SampleScreenTime.HoveredApp);
                break;
            case "selected":
                links.Select(SampleScreenTime.SelectedApp);
                break;
            default:
                break;
        }
    }
}
#endif
