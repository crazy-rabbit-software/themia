using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-5175] The folder widget's icon and thumbnail cache.</summary>
public sealed class LoadCacheTests
{
    /// <summary>
    /// Regression for the original's issues #4 and #16 ("sometimes not all of the icons load"): a
    /// failed extraction was remembered as the answer, so that type's items stayed glyphs for the
    /// whole session. The next ask must load again.
    /// </summary>
    [Fact]
    public async Task S5175_a_load_that_found_nothing_is_asked_again()
    {
        var cache = new LoadCache<string>(0, StringComparer.OrdinalIgnoreCase);
        var loads = 0;
        var answers = new Queue<string?>([null, "icon"]);

        Task<string?> Load()
        {
            loads++;
            return Task.FromResult(answers.Dequeue());
        }

        Assert.Null(await cache.GetAsync("32|.pdf", Load));
        Assert.False(cache.TryGet("32|.pdf", out _));

        Assert.Equal("icon", await cache.GetAsync("32|.pdf", Load));
        Assert.Equal(2, loads);
    }

    [Fact]
    public async Task S5175_what_was_found_is_kept_and_not_loaded_twice()
    {
        var cache = new LoadCache<string>(0, StringComparer.OrdinalIgnoreCase);
        var loads = 0;

        Task<string?> Load()
        {
            loads++;
            return Task.FromResult<string?>("icon");
        }

        await cache.GetAsync("32|.TXT", Load);
        Assert.Equal("icon", await cache.GetAsync("32|.txt", Load));
        Assert.True(cache.TryGet("32|.txt", out var kept));
        Assert.Equal("icon", kept);
        Assert.Equal(1, loads);
    }

    [Fact]
    public async Task S5175_rows_asking_at_once_share_one_load()
    {
        var cache = new LoadCache<string>(0, StringComparer.Ordinal);
        var gate = new TaskCompletionSource<string?>();
        var loads = 0;

        Task<string?> Load()
        {
            loads++;
            return gate.Task;
        }

        var first = cache.GetAsync("k", Load);
        var second = cache.GetAsync("k", Load);
        gate.SetResult("icon");

        Assert.Equal("icon", await first);
        Assert.Equal("icon", await second);
        Assert.Equal(1, loads);
    }

    [Fact]
    public async Task S5175_past_its_cap_the_cache_starts_over()
    {
        var cache = new LoadCache<string>(2, StringComparer.Ordinal);

        await cache.GetAsync("a", () => Task.FromResult<string?>("1"));
        await cache.GetAsync("b", () => Task.FromResult<string?>("2"));
        await cache.GetAsync("c", () => Task.FromResult<string?>("3"));

        Assert.Equal(1, cache.Count);
        Assert.True(cache.TryGet("c", out _));
        Assert.False(cache.TryGet("a", out _));
    }
}
