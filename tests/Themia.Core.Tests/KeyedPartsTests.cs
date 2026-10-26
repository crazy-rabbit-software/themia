using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-3325] Drawn parts kept by their item's key across reads.</summary>
public sealed class KeyedPartsTests
{
    private sealed class Part(string key)
    {
        public string Key { get; } = key;
    }

    [Fact]
    public void S3325_the_same_items_keep_their_parts()
    {
        var parts = new KeyedParts<Part>();
        var made = 0;
        Part Make(string key) { made++; return new Part(key); }

        var (first, _) = parts.Sync(["code", "chrome", "other"], Make, _ => Assert.Fail("nothing leaves"));
        var (again, reordered) = parts.Sync(["code", "chrome", "other"], Make, _ => Assert.Fail("nothing leaves"));

        Assert.Equal(3, made);
        Assert.False(reordered);
        Assert.Equal(first, again);
    }

    [Fact]
    public void S3325_a_new_item_is_made_and_one_that_left_is_dropped()
    {
        var parts = new KeyedParts<Part>();
        var dropped = new List<string>();
        var (first, _) = parts.Sync(["code", "chrome"], key => new Part(key), part => dropped.Add(part.Key));

        var (next, reordered) = parts.Sync(["code", "teams"], key => new Part(key), part => dropped.Add(part.Key));

        Assert.True(reordered);
        Assert.Same(first[0], next[0]);
        Assert.Equal("teams", next[1].Key);
        Assert.Equal(["chrome"], dropped);
        Assert.Null(parts.Of("chrome"));
        Assert.Equal(2, parts.Count);
    }

    [Fact]
    public void S3325_a_new_order_reuses_every_part_and_says_so()
    {
        var parts = new KeyedParts<Part>();
        var (first, _) = parts.Sync(["code", "chrome", "other"], key => new Part(key), _ => { });

        var (next, reordered) = parts.Sync(["chrome", "code", "other"], _ => throw new InvalidOperationException("reused"), _ => throw new InvalidOperationException("kept"));

        Assert.True(reordered);
        Assert.Same(first[1], next[0]);
        Assert.Same(first[0], next[1]);
        Assert.Equal(["chrome", "code", "other"], parts.Keys);
    }

    [Fact]
    public void S3325_a_key_twice_is_refused_before_anything_changes()
    {
        var parts = new KeyedParts<Part>();
        parts.Sync(["code"], key => new Part(key), _ => { });

        Assert.Throws<ArgumentException>(() => parts.Sync(["chrome", "chrome"], key => new Part(key), _ => { }));
        Assert.Equal(["code"], parts.Keys);
        Assert.NotNull(parts.Of("code"));
    }

    [Fact]
    public void S3325_clear_lets_every_part_go()
    {
        var parts = new KeyedParts<Part>();
        var dropped = new List<string>();
        parts.Sync(["code", "chrome"], key => new Part(key), _ => { });

        parts.Clear(part => dropped.Add(part.Key));

        Assert.Equal(["code", "chrome"], dropped);
        Assert.Equal(0, parts.Count);
        Assert.Empty(parts.Keys);
    }
}
