namespace Themia.Core.Links;

/// <summary>
/// [S-11905] A kind's part in view links, declared in its descriptor as Karpo's list records declare
/// their channels: the topics it publishes and the topics it follows. There is no per-widget opt-out:
/// a kind that follows a topic always does.
/// </summary>
public sealed class LinkUse
{
    private LinkUse(IReadOnlyList<LinkTopicId> publishes, IReadOnlyList<LinkTopicId> follows)
    {
        Publishes = publishes;
        Follows = follows;
    }

    /// <summary>A kind that takes no part.</summary>
    public static LinkUse None { get; } = new([], []);

    /// <summary>The topics it sets, in id order, each once.</summary>
    public IReadOnlyList<LinkTopicId> Publishes { get; }

    /// <summary>The topics it watches, in id order, each once.</summary>
    public IReadOnlyList<LinkTopicId> Follows { get; }

    /// <summary>Every topic it names, either way.</summary>
    public IEnumerable<LinkTopicId> Topics => Publishes.Union(Follows).Order();

    /// <summary>A declaration: what it publishes and what it follows.</summary>
    public static LinkUse Of(IEnumerable<LinkTopicId> publishes, IEnumerable<LinkTopicId> follows)
        => new([.. publishes.Distinct().Order()], [.. follows.Distinct().Order()]);

    /// <summary>A kind that publishes these topics and follows the same ones (the usual case).</summary>
    public static LinkUse Both(params LinkTopicId[] topics) => Of(topics, topics);
}
