using System.Text;
using System.Text.RegularExpressions;
using Themia.Core.Links;
using Themia.Modules.Tests.Modules;
using Themia.Sdk;
using Themia.Tests;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>
/// [S-11905]..[S-11907] View links are declared in data: a kind's descriptor says what it publishes and
/// follows, its code names exactly those topics, and SPEC's topic table is the one these declarations make.
/// </summary>
public sealed partial class ViewLinksConformanceTests
{
    private const string TableStart = "<!-- view-links:table (made from the descriptors; ViewLinksConformanceTests) -->";
    private const string TableEnd = "<!-- /view-links:table -->";

    public static TheoryData<string> Widgets() => [.. ModuleRegistry.WidgetIds];

    [Theory]
    [MemberData(nameof(Widgets))]
    public void S11906_a_kinds_code_names_exactly_the_topics_its_descriptor_declares(string id)
    {
        var descriptor = ModuleRegistry.Widgets.Single(m => m.Descriptor.Id.Value == id).Descriptor;
        var folder = Path.Combine(RepoFiles.Src, "Widgets", "Themia.Widget." + char.ToUpperInvariant(id[0]) + id[1..]);
        Assert.True(Directory.Exists(folder), folder);

        var named = Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories)
            .Where(file => !IsOutside(folder, file))
            .SelectMany(file => TopicUse().Matches(File.ReadAllText(file)).Select(m => m.Groups[1].Value))
            .Where(name => name != nameof(LinkTopics.All))
            .Select(name => Enum.Parse<LinkTopicId>(name))
            .Distinct()
            .Order()
            .ToList();

        Assert.Equal(descriptor.Links.Topics.ToList(), named);
    }

    [Fact]
    public void S11905_every_declared_topic_is_published_by_someone_or_followed_by_someone()
    {
        var all = ModuleRegistry.Widgets.Select(m => m.Descriptor).ToList();

        // A topic followed by a kind is published by some kind: nothing waits on a value no one sets
        // (the scene's links are a picture's stand-in, not a member).
        foreach (var topic in all.SelectMany(d => d.Links.Follows).Distinct())
        {
            Assert.True(all.Any(d => d.Links.Publishes.Contains(topic)), $"{topic} is followed but no kind publishes it");
        }
    }

    [Fact]
    public void S11907_specs_topic_table_is_the_one_the_descriptors_make()
    {
        var spec = File.ReadAllText(RepoFiles.At("SPEC.md")).Replace("\r\n", "\n", StringComparison.Ordinal);
        var start = spec.IndexOf(TableStart, StringComparison.Ordinal);
        var end = spec.IndexOf(TableEnd, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, "SPEC.md has no view-links table markers");

        var shown = spec[(start + TableStart.Length)..end].Trim('\n');
        var made = Table();
        Assert.True(made == shown, "SPEC.md's view-links table is not the descriptors'; it should read:\n" + made);
    }

    /// <summary>The topic table: each topic, its value, its lifetime, who publishes it and who follows it.</summary>
    private static string Table()
    {
        var all = ModuleRegistry.Widgets.Select(m => m.Descriptor).OrderBy(d => d.Id.Value, StringComparer.Ordinal).ToList();
        var text = new StringBuilder();
        text.Append("| Topic | Lifetime | Published by | Followed by |\n");
        text.Append("|---|---|---|---|\n");
        foreach (var topic in LinkTopics.All)
        {
            var publishes = string.Join(", ", all.Where(d => d.Links.Publishes.Contains(topic.Id)).Select(d => $"`{d.Id.Value}`"));
            var follows = string.Join(", ", all.Where(d => d.Links.Follows.Contains(topic.Id)).Select(d => $"`{d.Id.Value}`"));
            text.Append($"| `{topic.Id}` | {topic.Lifetime} | {Or(publishes)} | {Or(follows)} |\n");
        }

        return text.ToString().TrimEnd('\n');
    }

    private static string Or(string kinds) => kinds.Length == 0 ? "none" : kinds;

    private static bool IsOutside(string folder, string file)
    {
        var relative = Path.GetRelativePath(folder, file).Replace('\\', '/');
        return relative.StartsWith("Tests/", StringComparison.Ordinal)
            || relative.StartsWith("obj/", StringComparison.Ordinal)
            || relative.StartsWith("bin/", StringComparison.Ordinal);
    }

    [GeneratedRegex(@"\bLinkTopics\.(\w+)")]
    private static partial Regex TopicUse();
}
