using Themia.Tests;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-987] / [S-988] How the process may end. The rules live in the App's startup code, which no
/// test can run (it needs a XAML application), so these read the source the way
/// <see cref="NavIconAssetTests"/> reads the asset list: the lines are one-liners, and losing one is
/// silent until a user reports that Themia "just disappeared" (original issue #87).
/// </summary>
public sealed class ProcessLifetimeTests
{
    private static readonly string AppSource = File.ReadAllText(RepoFiles.At("src", "Themia.App", "App.xaml.cs"));

    /// <summary>
    /// The bug: WinUI's default shuts the dispatcher down when the last window closes. Themia has no
    /// main window, and the display pass closes a surface before it builds its replacement, so a
    /// single display re-keyed, a dead surface rebuilt, or every panel powered off at once exited the
    /// process without a log line.
    /// </summary>
    [Fact]
    public void S987_closing_the_last_surface_does_not_end_the_process()
        => Assert.Contains("DispatcherShutdownMode = DispatcherShutdownMode.OnExplicitShutdown", AppSource, StringComparison.Ordinal);

    /// <summary>A throw on a background thread ends the process whatever we do; it must be on record first.</summary>
    [Fact]
    public void S988_a_fatal_background_exception_is_logged_before_the_process_ends()
    {
        Assert.Contains("AppDomain.CurrentDomain.UnhandledException +=", AppSource, StringComparison.Ordinal);
        Assert.Contains("TaskScheduler.UnobservedTaskException +=", AppSource, StringComparison.Ordinal);
    }
}
