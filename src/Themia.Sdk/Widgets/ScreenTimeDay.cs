namespace Themia.Core.Widgets;

/// <summary>One app's time on one day.</summary>
/// <param name="App">The bucket key: the executable's lower-case stem, e.g. <c>chrome</c>.</param>
/// <param name="Name">What to call it.</param>
/// <param name="Seconds">Foreground seconds.</param>
/// <param name="Path">The executable, when it has ever been seen, for the icon.</param>
public sealed record ScreenTimeApp(string App, string Name, long Seconds, string? Path);

/// <summary>One day's usage: total, apps busiest first, and how often the foreground app changed.</summary>
public sealed record ScreenTimeDay(string Date, long TotalSeconds, IReadOnlyList<ScreenTimeApp> Apps, long Switches);
