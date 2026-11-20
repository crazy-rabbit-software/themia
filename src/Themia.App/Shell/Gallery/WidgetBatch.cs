#if DEBUG
using System.Text.Json;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Themia.Core.Diagnostics;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-14340] Debug only: many widget renders in one process, so a check pays WinUI's start-up once, not once a picture.
/// <c>Themia.exe --widget-batch &lt;jobs.json&gt; [--theme light|dark] [--timeout seconds]</c>. The file is
/// <c>{"jobs": [{"id": "…", "args": ["&lt;Kind&gt;", "&lt;out.png&gt;", "--sizes", "range", …]}, …]}</c>: a job's
/// <c>args</c> are what follows <c>--widget-png</c> on its own command line, drawn by the same
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
    public const string Flag = "--widget-batch";

    /// <summary>The exit code of a batch a job ran past its timeout.</summary>
    public const int TimedOut = 3;

    /// <summary>The exit code of a batch whose jobs file was refused: nothing was drawn.</summary>
    public const int Refused = 2;

    private const string Scope = "widget-batch";

    /// <summary>The options a whole process takes: never a job's.</summary>
    private static readonly string[] ProcessOptions = ["--theme", "--lang", "--icon-census"];

    public static bool TryRunFromCommandLine(Application app, string[] args)
    {
        var at = Array.IndexOf(args, Flag);
        if (at < 0)
        {
            return false;
        }

        string? problem = null;
        var jobs = at + 1 < args.Length ? Read(args[at + 1], out problem) : null;
        var timeout = Option(args, "--timeout") is { } seconds
            && int.TryParse(seconds, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var s) && s > 0
                ? TimeSpan.FromSeconds(s)
                : TimeSpan.FromSeconds(120);
        if (jobs is null || DispatcherQueue.GetForCurrentThread() is not { } dispatcher)
        {
            var message = $"batch: refused: {problem ?? $"usage: {Flag} <jobs.json> [--theme light|dark] [--timeout seconds]"}";
            Log.Error(Scope, message);
            Console.WriteLine(message);
            Environment.Exit(Refused);
            return true;
        }

        // The batch's theme reaches every job's design, as --theme does a render's own.
        string[] theme = Option(args, "--theme") is { } chosen ? ["--theme", chosen] : [];
        _ = dispatcher.TryEnqueue(async () =>
        {
            foreach (var (id, jobArgs) in jobs)
            {
                Console.WriteLine($"batch: start {id}");
                if (jobArgs.FirstOrDefault(a => ProcessOptions.Contains(a, StringComparer.Ordinal)) is { } option)
                {
                    Console.WriteLine($"batch: {option} is the whole batch's, on its own command line, never a job's");
                    Console.WriteLine($"batch: done {id} failed");
                    continue;
                }

                var render = WidgetRender.Render([WidgetRender.Flag, .. jobArgs, .. theme]);
                if (await Task.WhenAny(render, Task.Delay(timeout)).ConfigureAwait(true) != render)
                {
                    Log.Warn(Scope, $"job {id} still drawing after {timeout.TotalSeconds:0} s; the batch ends");
                    Console.WriteLine($"batch: timeout {id}");
                    Environment.Exit(TimedOut);
                    return;
                }

                Console.WriteLine($"batch: done {id} {(await render.ConfigureAwait(true) ? "ok" : "failed")}");
            }

            Console.WriteLine($"batch: finished {jobs.Count}");
            app.Exit();
        });

        return true;
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

    private static string? Option(string[] args, string name)
    {
        var at = Array.IndexOf(args, name);
        return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
    }
}
#endif
