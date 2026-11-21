#if DEBUG
using System.Text.Json;
using Themia.Core.Diagnostics;
using Themia.Core.Shell;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-14340] Debug only: many widget renders in one process, so a check pays WinUI's start-up once, not once a picture.
/// <c>Themia.exe --render widget-batch &lt;jobs.json&gt; [--theme light|dark] [--timeout seconds]</c>. The file is
/// <c>{"jobs": [{"id": "…", "args": ["&lt;Kind&gt;", "&lt;out.png&gt;", "--sizes", "range", …]}, …]}</c>: a job's
/// <c>args</c> are what follows <c>--render widget</c> on its own command line, drawn by the same
/// <see cref="WidgetRender.Render"/>, one job after another, so its PNG and layout dump are that render's. What a whole
/// process takes (the theme, chosen before the resources load; <c>--lang</c>; <c>--icon-census</c>) is the batch's own
/// command line, never a job's: a job naming one fails. On stdout, per job: <c>batch: start &lt;id&gt;</c>, the render's
/// own lines, <c>batch: done &lt;id&gt; ok|failed</c>. A job still drawing at the timeout (default 120 s) prints
/// <c>batch: timeout &lt;id&gt;</c> and ends the process (exit <see cref="TimedOut"/>): its window and services are in no
/// known state, so the caller starts a new batch from the next job (<c>scripts/renders.py</c>), as it does when a job
/// holds the UI thread and the process prints nothing more (the caller's own watchdog kills it).
/// </summary>
internal static class WidgetBatch
{
    /// <summary>The exit code of a batch a job ran past its timeout.</summary>
    public const int TimedOut = 3;

    private const string Scope = "widget-batch";

    /// <summary>The options a whole process takes: never a job's.</summary>
    private static readonly string[] ProcessOptions = ["--theme", "--lang", "--icon-census"];

    public static RenderCommand Command { get; } = new(
        RenderVerb.Render,
        "widget-batch",
        "<jobs.json> [--theme light|dark] [--timeout s]",
        """
        many widget renders in one process [S-14340]: {"jobs": [{"id", "args": [<what follows --render widget>]}]}
        the checks' renders go through it (scripts/renders.py); per job "batch: start <id>", its lines, "batch: done <id> ok|failed"
        """,
        RunAsync);

    private static async Task<int> RunAsync(RenderRun run)
    {
        var jobs = Read(run.PathArg(0), out var problem) ?? throw run.Usage($"refused: {problem}");
        var timeout = run.Option("--timeout") is { } seconds
            && int.TryParse(seconds, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var s) && s > 0
                ? TimeSpan.FromSeconds(s)
                : TimeSpan.FromSeconds(120);

        foreach (var (id, jobArgs) in jobs)
        {
            Console.WriteLine($"batch: start {id}");
            if (jobArgs.FirstOrDefault(a => ProcessOptions.Contains(a, StringComparer.Ordinal)) is { } option)
            {
                Console.WriteLine($"batch: {option} is the whole batch's, on its own command line, never a job's");
                Console.WriteLine($"batch: done {id} failed");
                continue;
            }

            var render = Job(run, jobArgs);
            if (await Task.WhenAny(render, Task.Delay(timeout)).ConfigureAwait(true) != render)
            {
                Log.Warn(Scope, $"job {id} still drawing after {timeout.TotalSeconds:0} s; the batch ends");
                Console.WriteLine($"batch: timeout {id}");
                Environment.Exit(TimedOut);
                return TimedOut;
            }

            Console.WriteLine($"batch: done {id} {(await render.ConfigureAwait(true) ? "ok" : "failed")}");
        }

        Console.WriteLine($"batch: finished {jobs.Count}");
        return RenderCommandLine.Passed;
    }

    /// <summary>One job, as its own <c>--render widget</c> would draw it: a usage error fails the job, never the batch.</summary>
    private static async Task<bool> Job(RenderRun run, string[] jobArgs)
    {
        try
        {
            return await WidgetRender.Render(run, jobArgs).ConfigureAwait(true);
        }
        catch (RenderUsageException usage)
        {
            Console.WriteLine($"widget: {usage.Message}");
            return false;
        }
    }

    /// <summary>The jobs of a batch file, or null (and why) when it is not one: refused whole, never half-read.</summary>
    internal static List<(string Id, string[] Args)>? Read(string path, out string? problem)
    {
        problem = null;
        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            problem = $"cannot read {path}: {ex.Message}";
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("jobs", out var list)
                || list.ValueKind != JsonValueKind.Array)
            {
                problem = $"{path} is not {{\"jobs\": [...]}}";
                return null;
            }

            var jobs = new List<(string, string[])>();
            foreach (var job in list.EnumerateArray())
            {
                if (job.ValueKind != JsonValueKind.Object
                    || !job.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(id.GetString())
                    || !job.TryGetProperty("args", out var jobArgs) || jobArgs.ValueKind != JsonValueKind.Array
                    || jobArgs.GetArrayLength() < 2 || jobArgs.EnumerateArray().Any(a => a.ValueKind != JsonValueKind.String))
                {
                    problem = $"job {jobs.Count} of {path} is not {{\"id\": \"…\", \"args\": [\"<Kind>\", \"<out.png>\", …]}}";
                    return null;
                }

                jobs.Add((id.GetString()!, [.. jobArgs.EnumerateArray().Select(a => a.GetString()!)]));
            }

            return jobs;
        }
        catch (JsonException ex)
        {
            problem = $"{path} is no JSON: {ex.Message}";
            return null;
        }
    }
}
#endif
