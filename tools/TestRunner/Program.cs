using System.Diagnostics;
using Xunit.Abstractions;
using Xunit.Runners;

namespace Themia.TestRunner;

/// <summary>
/// [S-14343] Runs one xUnit test assembly in this process with xUnit's own engine (<see cref="AssemblyRunner"/>): its test
/// collections in parallel as <c>dotnet test</c> runs them, fixtures, theories and skips as xUnit has them, and no test
/// host or loopback socket. <c>TestRunner &lt;tests.dll&gt; [--classes A,B | --classes-file f] [--filter text]
/// [--spec 1480] [--serial] [--threads n] [--hang seconds] [--full]</c>: a class list (full names) and a name filter (a
/// part of <c>Namespace.Class.Method</c>) narrow it, <c>--spec</c> keeps the tests named after a SPEC tag (S1480_…).
/// Prints each failure and skip, the slowest tests, and one summary line; exits 1 on a failure, 2 when no test finished
/// for <c>--hang</c> seconds (default 600), naming the tests still running. Built and run by <c>scripts/test.py</c>.
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length == 0 || !File.Exists(args[0]))
        {
            Console.Error.WriteLine("usage: TestRunner <tests.dll> [--classes A,B | --classes-file f] [--filter text] [--spec 1480] [--serial] [--threads n] [--hang seconds] [--full]");
            return 64;
        }

        var path = Path.GetFullPath(args[0]);
        var dir = Path.GetDirectoryName(path)!;
        string? Option(string name)
        {
            var at = Array.IndexOf(args, name);
            return at > 0 && at + 1 < args.Length ? args[at + 1] : null;
        }

        var classes = Option("--classes")?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            ?? (Option("--classes-file") is { } file ? File.ReadAllLines(file).Select(l => l.Trim()).Where(l => l.Length > 0).ToArray() : null);
        var filter = Option("--filter");
        var spec = Option("--spec") is { } tag ? $"S{tag.TrimStart('S', 's', '-')}_" : null;
        var serial = args.Contains("--serial");
        var full = args.Contains("--full");
        var threads = int.TryParse(Option("--threads"), out var n) && n > 0 ? n : Environment.ProcessorCount;
        var hang = TimeSpan.FromSeconds(int.TryParse(Option("--hang"), out var h) && h > 0 ? h : 600);

        // As the test host has it: the tests' own folder is the base and the current directory, and what they reference
        // (xUnit's engine among it) loads from there.
        Directory.SetCurrentDirectory(dir);
        AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY", dir + Path.DirectorySeparatorChar);
        System.Runtime.Loader.AssemblyLoadContext.Default.Resolving += (context, name) =>
            File.Exists(Path.Combine(dir, name.Name + ".dll")) ? context.LoadFromAssemblyPath(Path.Combine(dir, name.Name + ".dll")) : null;

        var gate = new object();
        var running = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        var times = new List<(string Name, decimal Seconds)>();
        var lastFinish = DateTime.UtcNow;
        int failed = 0, errors = 0, toRun = 0;
        ExecutionCompleteInfo? complete = null;
        using var done = new ManualResetEventSlim();
        using var runner = AssemblyRunner.WithoutAppDomain(path);
        var picked = classes is null ? null : new HashSet<string>(classes, StringComparer.Ordinal);
        runner.TestCaseFilter = test =>
        {
            var name = $"{test.TestMethod.TestClass.Class.Name}.{test.TestMethod.Method.Name}";
            return (picked is null || picked.Contains(test.TestMethod.TestClass.Class.Name))
                && (filter is null || name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                && (spec is null || test.TestMethod.Method.Name.StartsWith(spec, StringComparison.Ordinal));
        };
        runner.OnDiscoveryComplete = info => toRun = info.TestCasesToRun;
        runner.OnTestStarting = info =>
        {
            lock (gate)
            {
                running[info.TestDisplayName] = DateTime.UtcNow;
            }
        };
        runner.OnTestFinished = info =>
        {
            lock (gate)
            {
                running.Remove(info.TestDisplayName);
                times.Add((info.TestDisplayName, info.ExecutionTime));
                lastFinish = DateTime.UtcNow;
            }
        };
        runner.OnTestFailed = info =>
        {
            lock (gate)
            {
                failed++;
                var message = full ? info.ExceptionMessage : info.ExceptionMessage.Split('\n')[0].TrimEnd();
                Console.WriteLine($"FAIL {info.TestDisplayName}: {info.ExceptionType}: {message}");
                if (full && info.ExceptionStackTrace is { } stack)
                {
                    Console.WriteLine(stack);
                }
            }
        };
        runner.OnTestSkipped = info =>
        {
            lock (gate)
            {
                Console.WriteLine($"SKIP {info.TypeName}.{info.MethodName}: {info.SkipReason}");
            }
        };
        runner.OnErrorMessage = info =>
        {
            lock (gate)
            {
                errors++;
                Console.WriteLine($"ERROR {info.MesssageType}: {info.ExceptionType}: {info.ExceptionMessage}");
            }
        };
        runner.OnExecutionComplete = info =>
        {
            complete = info;
            done.Set();
        };

        var clock = Stopwatch.StartNew();
        runner.Start(new AssemblyRunnerStartOptions
        {
            Parallel = !serial,
            MaxParallelThreads = serial ? 1 : threads,
            TypesToRun = classes ?? [],
        });

        // A test that never returns holds the run: name it rather than wait for ever.
        while (!done.Wait(TimeSpan.FromSeconds(5)))
        {
            lock (gate)
            {
                if (DateTime.UtcNow - lastFinish > hang)
                {
                    Console.WriteLine($"HANG no test finished for {hang.TotalSeconds:0} s; still running:");
                    foreach (var (name, since) in running.OrderBy(r => r.Value))
                    {
                        Console.WriteLine($"  {name} (for {(DateTime.UtcNow - since).TotalSeconds:0} s)");
                    }

                    Console.Out.Flush();
                    Environment.Exit(2);
                }
            }
        }

        // Let the runner settle back to idle before it is disposed.
        while (runner.Status != AssemblyRunnerStatus.Idle)
        {
            Thread.Sleep(50);
        }

        foreach (var (name, seconds) in times.OrderByDescending(t => t.Seconds).Take(5).Where(t => t.Seconds >= 2))
        {
            Console.WriteLine($"SLOW {seconds:0.0} s {name}");
        }

        var info = complete!;
        var passed = info.TotalTests - info.TestsFailed - info.TestsSkipped;
        Console.WriteLine($"{Path.GetFileNameWithoutExtension(path)}: passed {passed}, failed {info.TestsFailed}, skipped {info.TestsSkipped}"
            + $" of {toRun} in {clock.Elapsed.TotalSeconds:0.0} s{(errors > 0 ? $", {errors} errors" : string.Empty)}");
        return info.TestsFailed > 0 || errors > 0 || failed > 0 ? 1 : 0;
    }
}
