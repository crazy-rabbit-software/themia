using System.Text.Json.Serialization;

namespace Themia.Core.Model;

/// <summary>
/// [S-1404] What a pane of an app layout opens (original S-69: <c>lnk</c>, <c>url</c>, <c>path</c>,
/// <c>explorer</c>).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<LayoutSource>))]
public enum LayoutSource
{
    /// <summary>A Start menu app: the target is its <c>.lnk</c> (or an <c>.exe</c>).</summary>
    App = 0,

    /// <summary>A web address, opened in the default browser.</summary>
    Url = 1,

    /// <summary>Any file, opened with whatever opens it.</summary>
    File = 2,

    /// <summary>A folder, opened in File Explorer.</summary>
    Folder = 3,
}

/// <summary>[S-1403] How a split divides its rectangle.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SplitDirection>))]
public enum SplitDirection
{
    /// <summary>Side by side: <see cref="LayoutNode.A"/> on the left, <see cref="LayoutNode.B"/> on the right.</summary>
    Vertical = 0,

    /// <summary>Stacked: <see cref="LayoutNode.A"/> on top, <see cref="LayoutNode.B"/> below.</summary>
    Horizontal = 1,
}

/// <summary>[S-1403] A node of an app layout: a pane, or a split of two nodes.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<LayoutNodeKind>))]
public enum LayoutNodeKind
{
    Pane = 0,
    Split = 1,
}

/// <summary>
/// [S-1404] What one pane opens. The target is untrusted like every shortcut target: it is checked
/// by <c>LaunchTargets</c> when it is launched, never once on the way in.
/// </summary>
public sealed class LayoutAssignment
{
    public LayoutSource Source { get; set; }

    /// <summary>The <c>.lnk</c>, the URL, the file or the folder.</summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>What to call it in the editor (an app's Start menu name); empty for the target's own.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// When the app already has a window, move that one instead of starting another (original
    /// <c>reuseIfRunning</c>, default true: Word, Outlook, Teams are single-instance anyway). Not used
    /// for a web address or a folder, which always open.
    /// </summary>
    public bool ReuseIfRunning { get; set; } = true;

    public LayoutAssignment Clone() => new()
    {
        Source = Source,
        Target = Target,
        Label = Label,
        ReuseIfRunning = ReuseIfRunning,
    };
}

/// <summary>
/// [S-1403] One node of an app layout's tree (original <c>layout.ts</c>). A pane may carry an
/// <see cref="Assignment"/>; a split carries two children and the share <see cref="Ratio"/> the
/// first one gets. One class with a <see cref="Kind"/> rather than a type hierarchy, so the
/// source-generated JSON needs no polymorphism; <c>LayoutTree.Repair</c> makes the shape honest.
/// </summary>
public sealed class LayoutNode
{
    public required string Id { get; set; }

    public LayoutNodeKind Kind { get; set; }

    /// <summary>Split only.</summary>
    public SplitDirection Direction { get; set; }

    /// <summary>Split only: the first child's share, 0.05..0.95.</summary>
    public double Ratio { get; set; } = 0.5;

    /// <summary>Split only: the left or top child.</summary>
    public LayoutNode? A { get; set; }

    /// <summary>Split only: the right or bottom child.</summary>
    public LayoutNode? B { get; set; }

    /// <summary>Pane only: what it opens, or null for an empty pane.</summary>
    public LayoutAssignment? Assignment { get; set; }

    public static LayoutNode NewPane() => new() { Id = NewId(), Kind = LayoutNodeKind.Pane };

    public static string NewId() => Guid.NewGuid().ToString("n");

    /// <summary>A deep copy that keeps the ids (they are unique within one tree, not across trees).</summary>
    public LayoutNode Clone() => new()
    {
        Id = Id,
        Kind = Kind,
        Direction = Direction,
        Ratio = Ratio,
        A = A?.Clone(),
        B = B?.Clone(),
        Assignment = Assignment?.Clone(),
    };
}
