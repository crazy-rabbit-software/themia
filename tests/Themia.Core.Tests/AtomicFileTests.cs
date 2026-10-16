using Themia.Core.Storage;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-49] The atomic write's last step, the rename over the target, retried a few times on a
/// sharing or access error, which an indexer or a virus scanner holding the fresh file for a moment
/// causes; any other error, and the last attempt's, is the caller's as before.
/// </summary>
public sealed class AtomicFileTests
{
    private static IOException SharingViolation() => new("in use", unchecked((int)0x80070020));

    [Fact]
    public void S49_a_rename_refused_for_a_moment_lands_on_a_later_attempt()
    {
        var calls = 0;
        var waits = new List<TimeSpan>();

        AtomicFile.Replace("a.tmp", "a", (_, _) =>
        {
            if (++calls < 3)
            {
                throw calls == 1 ? new UnauthorizedAccessException("denied") : SharingViolation();
            }
        }, waits.Add);

        Assert.Equal(3, calls);
        Assert.Equal([TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(20)], waits);
    }

    [Fact]
    public void S49_the_retry_is_bounded_and_the_last_error_is_thrown()
    {
        var calls = 0;
        var waits = new List<TimeSpan>();

        Assert.Throws<UnauthorizedAccessException>(() =>
            AtomicFile.Replace("a.tmp", "a", (_, _) => { calls++; throw new UnauthorizedAccessException("denied"); }, waits.Add));

        Assert.Equal(AtomicFile.Attempts, calls);
        Assert.Equal(AtomicFile.Attempts - 1, waits.Count);
        Assert.True(waits.Sum(w => w.TotalMilliseconds) <= 200, "the retry waits a moment, never long");
    }

    [Fact]
    public void S49_any_other_error_is_not_retried()
    {
        var calls = 0;

        Assert.Throws<FileNotFoundException>(() =>
            AtomicFile.Replace("a.tmp", "a", (_, _) => { calls++; throw new FileNotFoundException("gone"); }, _ => { }));
        Assert.Throws<IOException>(() =>
            AtomicFile.Replace("a.tmp", "a", (_, _) => { calls++; throw new IOException("disk full", unchecked((int)0x80070070)); }, _ => { }));

        Assert.Equal(2, calls);
    }

    [Fact]
    public void S49_a_real_rename_replaces_the_target()
    {
        var dir = Directory.CreateTempSubdirectory("themia-atomic-").FullName;
        try
        {
            var path = Path.Combine(dir, "a.json");
            File.WriteAllText(path, "old");
            File.WriteAllText(path + ".tmp", "new");

            AtomicFile.Replace(path + ".tmp", path);

            Assert.Equal("new", File.ReadAllText(path));
            Assert.False(File.Exists(path + ".tmp"));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
