using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Themia.Core.Diagnostics;
using Themia.Core.Model;
using Themia.Core.Shell;

namespace Themia.App.Shell;

/// <summary>
/// [S-69] A render or check the build runs instead of the app: <c>Themia.exe --render|--check &lt;name&gt;
/// &lt;usage&gt;</c>. <paramref name="Summary"/> is what <c>--render list</c> prints under its line (more lines for its
/// options). <paramref name="Run"/> runs on the UI thread and returns the exit code (<see cref="RenderCommandLine"/>).
/// <paramref name="Theme"/> reads, from the arguments after its name, the application theme it draws in, before the
/// resources load (a scene's own, the family walk's dark by default); without one it is the <c>--theme</c> asked for.
/// </summary>
internal sealed record RenderCommand(RenderVerb Verb, string Name, string Usage, string Summary, Func<RenderRun, Task<int>> Run, Func<IReadOnlyList<string>, AppTheme?>? Theme = null)
{
    /// <summary>The command line it is run with, as its usage error and the list say it.</summary>
    public string Line => RenderCommandLine.Line(Verb, Name, Usage);
}

/// <summary>[S-69] A command line was wrong: the usage is printed and the exit code is <see cref="RenderCommandLine.UsageError"/>.</summary>
internal sealed class RenderUsageException(string message) : Exception(message);

/// <summary>[S-69] One run of a <see cref="RenderCommand"/>: its arguments (after its name) and the app it runs in.</summary>
internal sealed class RenderRun(RenderCommand command, IReadOnlyList<string> args, Application app, DispatcherQueue dispatcher)
{
    public RenderCommand Command => command;

    /// <summary>Every argument after the command's name.</summary>
    public IReadOnlyList<string> Args => args;

    public Application App => app;

    public DispatcherQueue Dispatcher => dispatcher;

    /// <summary>The <paramref name="index"/>-th argument, which must be there and be no option.</summary>
    public string Arg(int index)
        => index < args.Count && !StartArguments.LooksLikeFlag(args[index])
            ? args[index]
            : throw Usage($"argument {index + 1} is missing");

    /// <summary>The <paramref name="index"/>-th argument as a full path.</summary>
    public string PathArg(int index) => Path.GetFullPath(Arg(index));

    /// <summary>The value after <paramref name="name"/>, or null.</summary>
    public string? Option(string name) => RenderCommandLine.Option(args, name);

    /// <summary>Whether the switch <paramref name="name"/> was given.</summary>
    public bool Has(string name) => RenderCommandLine.Has(args, name);

    /// <summary>The error a wrong command line is: thrown, it prints the usage and exits <see cref="RenderCommandLine.UsageError"/>.</summary>
    public RenderUsageException Usage(string why) => new(why);
}

/// <summary>
/// [S-69] Every render and check the build has, by verb and name, and the one way they run: <see cref="TryRun"/> takes a
/// start whose first argument is <c>--render</c> or <c>--check</c>, runs the command on the UI thread, and exits with its
/// code (a usage error 64, a throw 2). A Release build has <c>--render list</c> only ([S-11854] retired with the add page, [S-16728]);
/// a Debug build every render of <c>Shell/Gallery</c> too. <c>--render list</c> prints them all: the generated list of
/// flags.
/// </summary>
internal static partial class RenderCommands
{
    private const string Scope = "render";

    /// <summary>Every command of this build.</summary>
    public static IReadOnlyList<RenderCommand> All { get; } = [.. Shipped(), .. Gallery()];

    private static IEnumerable<RenderCommand> Shipped() =>
    [
        new(RenderVerb.Render, "list", string.Empty, "every render and check of this build, with its usage", List),
    ];

    /// <summary>The Debug build's renders (Shell/Gallery); none in a Release build.</summary>
    private static partial IEnumerable<RenderCommand> Gallery();

    /// <summary>True when <paramref name="commandLine"/> (the process's, the executable first) is a render command, which then runs and exits.</summary>
    public static bool TryRun(Application app, string[] commandLine)
    {
        if (RenderCommandLine.Parse(commandLine) is not { } asked)
        {
            return false;
        }

        if (DispatcherQueue.GetForCurrentThread() is not { } dispatcher)
        {
            Console.Error.WriteLine("render: no dispatcher queue on the launch thread");
            Environment.Exit(RenderCommandLine.Crashed);
            return true;
        }

        _ = dispatcher.TryEnqueue(async () =>
        {
            Environment.ExitCode = await RunAsync(app, dispatcher, asked).ConfigureAwait(true);
            app.Exit();
        });
        return true;
    }

    /// <summary>
    /// The application theme <paramref name="commandLine"/> (the process's) asks for, read before the resources load: its
    /// command's own reading (<see cref="RenderCommand.Theme"/>), else its <c>--theme</c>; null when it is no render command
    /// or asks for none.
    /// </summary>
    public static AppTheme? ThemeOf(IReadOnlyList<string> commandLine)
        => RenderCommandLine.Parse(commandLine) is not { } asked ? null
            : Find(asked)?.Theme is { } read ? read(asked.Args)
            : RenderCommandLine.Theme(commandLine);

    private static RenderCommand? Find(RenderInvocation asked)
        => All.FirstOrDefault(c => c.Verb == asked.Verb && string.Equals(c.Name, asked.Name, StringComparison.Ordinal));

    private static async Task<int> RunAsync(Application app, DispatcherQueue dispatcher, RenderInvocation asked)
    {
        if (Find(asked) is not { } command)
        {
            var known = string.Join(", ", All.Where(c => c.Verb == asked.Verb).Select(c => c.Name));
            Console.WriteLine($"render: no {RenderCommandLine.Line(asked.Verb, asked.Name, string.Empty)} in this build (has: {known}); Themia.exe --render list says them all");
            return RenderCommandLine.UsageError;
        }

        try
        {
            Starting();
            return await command.Run(new RenderRun(command, asked.Args, app, dispatcher)).ConfigureAwait(true);
        }
        catch (RenderUsageException usage)
        {
            // On stdout, with the command's own lines: a script that keeps them (store/capture.ps1's logs) keeps why.
            Console.WriteLine($"{command.Name}: {usage.Message}");
            Console.WriteLine($"usage: {command.Line}");
            return RenderCommandLine.UsageError;
        }
        catch (Exception ex)
        {
            Log.Error(Scope, $"{command.Line} threw", ex);
            Console.WriteLine($"{command.Name}: failed: {ex.Message}");
            return RenderCommandLine.Crashed;
        }
        finally
        {
            Ended();
        }
    }

    /// <summary>[S-15900] Before a command runs: the Debug build's renders listen for the host's loads (none in Release).</summary>
    static partial void Starting();

    /// <summary>[S-15900] After a command ran: the Debug build says how long its settles took (none in Release).</summary>
    static partial void Ended();

    /// <summary><c>--render list</c>: every command's line and summary, renders first.</summary>
    private static Task<int> List(RenderRun run)
    {
        foreach (var command in All.OrderBy(c => c.Verb).ThenBy(c => c.Name, StringComparer.Ordinal))
        {
            Console.WriteLine(command.Line);
            foreach (var line in command.Summary.Split('\n'))
            {
                Console.WriteLine($"    {line}");
            }
        }

        return Task.FromResult(RenderCommandLine.Passed);
    }
}

#if !DEBUG
/// <summary>A Release build has no Gallery: its commands are the shipped ones alone.</summary>
internal static partial class RenderCommands
{
    private static partial IEnumerable<RenderCommand> Gallery() => [];
}
#endif
