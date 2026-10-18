using System.Diagnostics;
using Themia.Core.Diagnostics;
using Themia.Core.Widgets;

namespace Themia.App.Services.Files;

/// <summary>
/// [S-1325] The git badge's data: the repository a folder is in, and <c>git status</c> for it.
/// <para>
/// Only when there is a repository (a <c>.git</c> found walking up from the folder — no process is
/// started for an ordinary folder), only through the user's own <c>git</c> (no libgit2: nothing to
/// ship, AOT-clean), with <c>--no-optional-locks</c> so a background status never takes the index
/// lock out from under the user's own git. Off the UI thread, and single-flight per repository: a
/// burst of reloads shares one running status. No git on the PATH is remembered for the run and
/// simply means no badge.
/// </para>
/// </summary>
internal static class GitStatus
{
    private const string Scope = "git-status";
    private static readonly Dictionary<string, Task<GitRepoStatus?>> Running = new(StringComparer.OrdinalIgnoreCase);
    private static bool _noGit;

    /// <summary>The repository root holding <paramref name="folder"/>, or null. Cheap: directory probes only.</summary>
    public static string? FindRoot(string folder)
    {
        try
        {
            for (var at = new DirectoryInfo(folder); at is not null; at = at.Parent)
            {
                var marker = Path.Combine(at.FullName, ".git");

                if (Directory.Exists(marker) || File.Exists(marker))
                {
                    return at.FullName;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Log.Debug(Scope, $"stopped looking for a repository above '{folder}': {ex.Message}");
        }

        return null;
    }

    /// <summary>The status of the repository holding <paramref name="folder"/>, or null when there is none or git cannot say. UI thread.</summary>
    public static Task<GitRepoStatus?> GetAsync(string folder)
    {
        if (_noGit || FindRoot(folder) is not { } root)
        {
            return Task.FromResult<GitRepoStatus?>(null);
        }

        if (Running.TryGetValue(root, out var running))
        {
            return running;
        }

        var task = RunAsync(root);
        Running[root] = task;
        _ = task.ContinueWith(_ => Running.Remove(root), TaskScheduler.FromCurrentSynchronizationContext());
        return task;
    }

    private static async Task<GitRepoStatus?> RunAsync(string root)
    {
        try
        {
            var output = await Task.Run(() => Status(root)).ConfigureAwait(true);
            return output is null ? null : new GitRepoStatus(root, GitStatusParser.Parse(output));
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            // No git.exe on the PATH. Asked once per run, not once per folder.
            _noGit = true;
            Log.Info(Scope, $"git is not available, so folders show no git badge ({ex.Message})");
            return null;
        }
    }

    private static string? Status(string root)
    {
        var start = new ProcessStartInfo("git")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
        };

        foreach (var arg in new[] { "--no-optional-locks", "-C", root, "status", "--porcelain=v1", "--branch", "-z", "--untracked-files=normal" })
        {
            start.ArgumentList.Add(arg);
        }

        using var process = Process.Start(start);

        if (process is null)
        {
            return null;
        }

        // Both pipes drained concurrently: a status large enough to fill one pipe would otherwise
        // deadlock against a git waiting for the other to be read.
        var error = process.StandardError.ReadToEndAsync();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            Log.Debug(Scope, $"git status exited {process.ExitCode} in '{root}': {error.Result.Trim()}");
            return null;
        }

        return output;
    }
}
