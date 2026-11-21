using Themia.Core.Model;

namespace Themia.Core.Shell;

/// <summary>[S-69] Which of the two verbs a render command is run with.</summary>
public enum RenderVerb
{
    /// <summary><c>--render &lt;name&gt;</c>: draws to files (PNGs, JSON) and exits.</summary>
    Render,

    /// <summary><c>--check &lt;name&gt;</c>: runs a check, prints and writes its lines, and exits with its verdict.</summary>
    Check,
}

/// <summary>[S-69] A render command as asked for: its verb, its name and the arguments after the name.</summary>
/// <param name="Verb">Render or check.</param>
/// <param name="Name">The command's name (empty when none was given).</param>
/// <param name="Args">Every argument after the name, in order.</param>
public sealed record RenderInvocation(RenderVerb Verb, string Name, IReadOnlyList<string> Args);

/// <summary>
/// [S-69] The one command surface of the renders and checks: <c>Themia.exe --render &lt;name&gt; …</c> and
/// <c>Themia.exe --check &lt;name&gt; …</c>, as the first argument only ([S-68]). Parses a start's command line into a
/// <see cref="RenderInvocation"/>, reads its options, and holds the exit codes every command ends with. Pure.
/// </summary>
public static class RenderCommandLine
{
    /// <summary>The verb of the renders.</summary>
    public const string RenderFlag = "--render";

    /// <summary>The verb of the checks.</summary>
    public const string CheckFlag = "--check";

    /// <summary>A render drawn, a check passed.</summary>
    public const int Passed = 0;

    /// <summary>A check found failures, or a render could not draw what it was asked for.</summary>
    public const int Failed = 1;

    /// <summary>The command threw: no verdict.</summary>
    public const int Crashed = 2;

    /// <summary>The command line was wrong (an unknown name, a missing argument): nothing ran (BSD's EX_USAGE).</summary>
    public const int UsageError = 64;

    /// <summary>The two verbs, the flags <see cref="StartArguments"/> knows as renders.</summary>
    public static IReadOnlyList<string> Flags { get; } = [RenderFlag, CheckFlag];

    /// <summary>
    /// The render command <paramref name="commandLine"/> (the executable first) asks for, or null when its first argument
    /// is no verb. A verb with no name parses with an empty <see cref="RenderInvocation.Name"/>.
    /// </summary>
    public static RenderInvocation? Parse(IReadOnlyList<string> commandLine)
    {
        ArgumentNullException.ThrowIfNull(commandLine);
        if (commandLine.Count < 2)
        {
            return null;
        }

        RenderVerb? verb = commandLine[1] switch
        {
            RenderFlag => RenderVerb.Render,
            CheckFlag => RenderVerb.Check,
            _ => null,
        };

        if (verb is not { } known)
        {
            return null;
        }

        var name = commandLine.Count > 2 ? commandLine[2] : string.Empty;
        return new RenderInvocation(known, name, [.. commandLine.Skip(3)]);
    }

    /// <summary>The value after the option <paramref name="name"/> in <paramref name="args"/>, or null when it is absent or last.</summary>
    public static string? Option(IReadOnlyList<string> args, string name)
    {
        ArgumentNullException.ThrowIfNull(args);
        for (var i = 0; i < args.Count - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.Ordinal))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    /// <summary>Whether the switch <paramref name="name"/> is among <paramref name="args"/>.</summary>
    public static bool Has(IReadOnlyList<string> args, string name)
    {
        ArgumentNullException.ThrowIfNull(args);
        return args.Contains(name, StringComparer.Ordinal);
    }

    /// <summary>
    /// The application theme a render command asks for with <c>--theme light|dark</c> (it can only be chosen before the
    /// resources load), or null: no render command, no option, or another word.
    /// </summary>
    public static AppTheme? Theme(IReadOnlyList<string> commandLine)
        => Parse(commandLine) is { } invocation
            ? Option(invocation.Args, "--theme")?.ToLowerInvariant() switch
            {
                "light" => AppTheme.Light,
                "dark" => AppTheme.Dark,
                _ => null,
            }
            : null;

    /// <summary>The command line a command is run with, as its usage and the list say it.</summary>
    public static string Line(RenderVerb verb, string name, string usage)
        => $"Themia.exe {(verb == RenderVerb.Render ? RenderFlag : CheckFlag)} {name} {usage}".TrimEnd();

    /// <summary>A check's exit code: <see cref="Crashed"/> when it threw, <see cref="Failed"/> on any failure, else <see cref="Passed"/>.</summary>
    public static int Verdict(int failures, bool crashed) => crashed ? Crashed : failures > 0 ? Failed : Passed;
}
