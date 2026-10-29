namespace Themia.Core.Shell;

/// <summary>[S-68] What a start's first argument asks for.</summary>
public enum StartRequest
{
    /// <summary>The app: no argument, an argument that is no flag, or a flag the app itself starts with (<c>--autostart</c>).</summary>
    App,

    /// <summary>A render or check this build knows: it runs instead of the app and exits; it never starts the app or hands off.</summary>
    Render,

    /// <summary>A flag this build does not know: an error naming it, a non-zero exit, never the app and never the hand-off.</summary>
    Unknown,
}

/// <summary>[S-68] The decision on a start's arguments, and the flag it names when there is one.</summary>
/// <param name="Request">What the first argument asks for.</param>
/// <param name="Flag">The first argument when it is a flag, else null.</param>
public readonly record struct StartDecision(StartRequest Request, string? Flag);

/// <summary>
/// [S-68] A start's command line, classified before anything else runs. A start that is not the app's
/// own must never reach the single-instance hand-off ([S-66]): a mistyped or not-yet-built render flag
/// once took the normal start path and toggled the running copy's edit mode. So a first argument that
/// looks like a flag is the app only when it is one the app starts with, a render only when this build
/// knows it, and otherwise unknown: the caller says so and exits non-zero. Pure.
/// </summary>
public static class StartArguments
{
    /// <summary>The app's exit code for an unknown flag (a usage error, as the renders' own).</summary>
    public const int UnknownFlagExitCode = 2;

    /// <summary>
    /// The flags the app itself starts with in every build: the Run entry's <c>--autostart</c> ([S-934]).
    /// The uninstall's <c>--purge-data</c> is the caller's own and runs before this decision.
    /// </summary>
    public static IReadOnlyList<string> AppFlags { get; } = [AutostartRules.Flag];

    /// <summary>
    /// Classify <paramref name="commandLine"/> (as <c>Environment.GetCommandLineArgs()</c> gives it: the
    /// executable first). <paramref name="renderFlags"/> are the renders and checks this build has
    /// (none in a Release build). Flags compare exactly, as every render looks its own up.
    /// </summary>
    public static StartDecision Classify(IReadOnlyList<string> commandLine, IReadOnlyCollection<string> renderFlags)
    {
        ArgumentNullException.ThrowIfNull(commandLine);
        ArgumentNullException.ThrowIfNull(renderFlags);

        if (commandLine.Count < 2 || !LooksLikeFlag(commandLine[1]))
        {
            return new StartDecision(StartRequest.App, null);
        }

        var flag = commandLine[1];
        if (AppFlags.Contains(flag, StringComparer.Ordinal))
        {
            return new StartDecision(StartRequest.App, flag);
        }

        return renderFlags.Contains(flag, StringComparer.Ordinal)
            ? new StartDecision(StartRequest.Render, flag)
            : new StartDecision(StartRequest.Unknown, flag);
    }

    /// <summary>Whether an argument is a flag rather than a value: it starts with a dash (<c>-x</c>, <c>--x</c>, even a lone <c>-</c>).</summary>
    public static bool LooksLikeFlag(string? argument) => argument is { Length: > 0 } && argument[0] == '-';

    /// <summary>The line an unknown flag prints (English: no language is loaded this early).</summary>
    public static string UnknownFlagMessage(string flag) => $"Themia: unknown flag '{flag}'; Themia was not started and no running copy was told anything.";

    /// <summary>The line for a render flag no render took (a render's own lookup and this list disagree).</summary>
    public static string UntakenRenderMessage(string flag) => $"Themia: '{flag}' is a render flag but no render ran; Themia was not started.";
}
