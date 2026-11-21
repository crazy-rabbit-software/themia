using Themia.Core.Diagnostics;
using Themia.Core.Shell;

namespace Themia.App.Shell;

/// <summary>
/// [S-69] The one report of every check (<c>Themia.exe --check &lt;name&gt;</c>): its lines on stdout and, when it has
/// one, in its text file; <c>ok</c> or <c>FAIL</c> per expectation; the last line <c>&lt;name&gt;: PASS</c> or
/// <c>&lt;name&gt;: FAIL (n)</c>; and its exit code (<see cref="RenderCommandLine.Verdict"/>: 1 on any failure, 2 when the
/// check threw).
/// </summary>
internal sealed class CheckReport(string name, string? path)
{
    private readonly List<string> _lines = [];
    private bool _crashed;

    /// <summary>The check's name, the first word of its summary lines.</summary>
    public string Name => name;

    /// <summary>The failures so far.</summary>
    public int Failures { get; private set; }

    /// <summary>A line of the report.</summary>
    public void Say(string line)
    {
        Console.WriteLine(line);
        _lines.Add(line);
    }

    /// <summary>An expectation: an <c>ok</c> line, or a <c>FAIL</c> line counted. Returns <paramref name="ok"/>.</summary>
    public bool Expect(bool ok, string what)
    {
        Failures += ok ? 0 : 1;
        Say($"{(ok ? "ok  " : "FAIL")} {what}");
        return ok;
    }

    /// <summary>A failure.</summary>
    public void Fail(string what) => Expect(false, what);

    /// <summary>
    /// A line in the check's own shape that holds <paramref name="failures"/> failures (one kind's line over its several
    /// presses, a summary of cards not at rest): said as it is, the failures counted in the verdict.
    /// </summary>
    public void Say(string line, int failures)
    {
        Failures += Math.Max(0, failures);
        Say(line);
    }

    /// <summary>The check threw: logged, said, and the verdict is <see cref="RenderCommandLine.Crashed"/>.</summary>
    public void Crash(Exception exception)
    {
        _crashed = true;
        Log.Error(name, $"the {name} threw", exception);
        Say($"{name}: failed: {exception.Message}");
    }

    /// <summary>The verdict line, the text file written, and the exit code.</summary>
    public async Task<int> FinishAsync()
    {
        Say(_crashed ? $"{name}: CRASHED" : Failures == 0 ? $"{name}: PASS" : $"{name}: FAIL ({Failures})");
        if (path is not null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            await File.WriteAllLinesAsync(path, _lines).ConfigureAwait(true);
        }

        return RenderCommandLine.Verdict(Failures, _crashed);
    }
}
