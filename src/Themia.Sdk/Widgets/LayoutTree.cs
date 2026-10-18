using Themia.Core.Model;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-1403] Editing an app layout's pane tree (original <c>layout.ts</c>): split a pane, remove one,
/// drag a divider, assign what a pane opens — and repair a tree read from disk. Pure; every
/// operation returns the root to keep (a remove can replace it), and none ever leaves a split with a
/// missing child or a tree with no pane.
/// </summary>
public static class LayoutTree
{
    /// <summary>The smallest share a divider can give one side (original MIN_RATIO).</summary>
    public const double MinRatio = 0.05;

    /// <summary>The largest share (original MAX_RATIO).</summary>
    public const double MaxRatio = 0.95;

    /// <summary>
    /// How deep a tree may nest. Sixteen panes is already more windows than any screen holds; the
    /// bound is what stops a hand-edited file from recursing the app off its stack.
    /// </summary>
    public const int MaxDepth = 6;

    /// <summary>[S-1403] The node with <paramref name="id"/>, or null.</summary>
    public static LayoutNode? Find(LayoutNode? root, string id)
    {
        if (root is null)
        {
            return null;
        }

        if (root.Id == id)
        {
            return root;
        }

        return root.Kind == LayoutNodeKind.Split ? Find(root.A, id) ?? Find(root.B, id) : null;
    }

    /// <summary>[S-1403] Every pane, left to right and top to bottom.</summary>
    public static IReadOnlyList<LayoutNode> Panes(LayoutNode? root)
    {
        var panes = new List<LayoutNode>();
        Collect(root, panes);
        return panes;
    }

    private static void Collect(LayoutNode? node, List<LayoutNode> into)
    {
        if (node is null)
        {
            return;
        }

        if (node.Kind == LayoutNodeKind.Pane)
        {
            into.Add(node);
            return;
        }

        Collect(node.A, into);
        Collect(node.B, into);
    }

    /// <summary>[S-1405] How many panes open something (original <c>countAssignedLeaves</c>).</summary>
    public static int AssignedCount(LayoutNode? root) => Panes(root).Count(IsAssigned);

    /// <summary>A pane with a non-blank target.</summary>
    public static bool IsAssigned(LayoutNode pane)
        => pane.Kind == LayoutNodeKind.Pane && pane.Assignment is { } a && !string.IsNullOrWhiteSpace(a.Target);

    /// <summary>
    /// [S-1403] Split a pane in two (original <c>splitLeaf</c>): the pane keeps its id and what it
    /// opens as the first half, a new empty pane is the second, half and half. A pane already at
    /// <see cref="MaxDepth"/> does not split. Returns the root and whether it changed.
    /// </summary>
    public static (LayoutNode Root, bool Changed) Split(LayoutNode root, string paneId, SplitDirection direction)
    {
        ArgumentNullException.ThrowIfNull(root);

        var depth = DepthOf(root, paneId, 0);

        if (depth < 0 || depth >= MaxDepth || Find(root, paneId) is not { Kind: LayoutNodeKind.Pane })
        {
            return (root, false);
        }

        return (Replace(root, paneId, pane => new LayoutNode
        {
            Id = LayoutNode.NewId(),
            Kind = LayoutNodeKind.Split,
            Direction = direction,
            Ratio = 0.5,
            A = pane,
            B = LayoutNode.NewPane(),
        }), true);
    }

    /// <summary>
    /// [S-1403] Remove a pane (original <c>removeLeafFromTree</c>): its sibling takes the parent
    /// split's whole rectangle. Removing the only pane leaves one fresh empty pane, never nothing.
    /// </summary>
    public static (LayoutNode Root, bool Changed) Remove(LayoutNode root, string paneId)
    {
        ArgumentNullException.ThrowIfNull(root);

        if (Find(root, paneId) is not { Kind: LayoutNodeKind.Pane })
        {
            return (root, false);
        }

        return (Without(root, paneId) ?? LayoutNode.NewPane(), true);
    }

    private static LayoutNode? Without(LayoutNode node, string paneId)
    {
        if (node.Kind == LayoutNodeKind.Pane)
        {
            return node.Id == paneId ? null : node;
        }

        var a = node.A is null ? null : Without(node.A, paneId);
        var b = node.B is null ? null : Without(node.B, paneId);

        if (a is null)
        {
            return b;
        }

        if (b is null)
        {
            return a;
        }

        node.A = a;
        node.B = b;
        return node;
    }

    /// <summary>[S-1403] Move a split's divider: the first child's share, held to 0.05..0.95. Returns whether it changed.</summary>
    public static bool SetRatio(LayoutNode root, string splitId, double ratio)
    {
        if (Find(root, splitId) is not { Kind: LayoutNodeKind.Split } split || double.IsNaN(ratio))
        {
            return false;
        }

        var clamped = ClampRatio(ratio);

        if (Math.Abs(split.Ratio - clamped) < 1e-9)
        {
            return false;
        }

        split.Ratio = clamped;
        return true;
    }

    /// <summary>[S-1403] A share held inside the divider's range; NaN is the middle.</summary>
    public static double ClampRatio(double ratio) => double.IsNaN(ratio) ? 0.5 : Math.Clamp(ratio, MinRatio, MaxRatio);

    /// <summary>
    /// [S-1404] Say what a pane opens, or clear it with null or a blank target (original S-60: an
    /// empty target clears the assignment). Returns whether anything changed.
    /// </summary>
    public static bool Assign(LayoutNode root, string paneId, LayoutAssignment? assignment)
    {
        if (Find(root, paneId) is not { Kind: LayoutNodeKind.Pane } pane)
        {
            return false;
        }

        var next = assignment is null || string.IsNullOrWhiteSpace(assignment.Target) ? null : assignment.Clone();

        if (next is not null)
        {
            next.Target = next.Target.Trim();
        }

        if (Same(pane.Assignment, next))
        {
            return false;
        }

        pane.Assignment = next;
        return true;
    }

    private static bool Same(LayoutAssignment? a, LayoutAssignment? b)
    {
        if (a is null || b is null)
        {
            return a is null && b is null;
        }

        return a.Source == b.Source
            && string.Equals(a.Target, b.Target, StringComparison.Ordinal)
            && string.Equals(a.Label, b.Label, StringComparison.Ordinal)
            && a.ReuseIfRunning == b.ReuseIfRunning;
    }

    /// <summary>
    /// [S-1406] Make a tree read from disk honest (a hand-edited or truncated file): a missing root
    /// becomes one empty pane; a split missing a child becomes its other child (or an empty pane);
    /// a ratio out of range or NaN is clamped; a pane's assignment with no target or an unknown
    /// source is dropped; blank or repeated ids get new ones; anything nested deeper than
    /// <see cref="MaxDepth"/> is cut to an empty pane. Every repair is named in
    /// <paramref name="repairs"/>; a sound tree comes back untouched.
    /// </summary>
    public static LayoutNode Repair(LayoutNode? root, List<string> repairs)
    {
        ArgumentNullException.ThrowIfNull(repairs);

        if (root is null)
        {
            repairs.Add("App layout had no panes");
            return LayoutNode.NewPane();
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        return RepairNode(root, 0, seen, repairs);
    }

    private static LayoutNode RepairNode(LayoutNode node, int depth, HashSet<string> seen, List<string> repairs)
    {
        if (string.IsNullOrWhiteSpace(node.Id) || !seen.Add(node.Id))
        {
            node.Id = LayoutNode.NewId();
            seen.Add(node.Id);
            repairs.Add("App layout had a missing or repeated pane id");
        }

        if (!Enum.IsDefined(node.Kind))
        {
            repairs.Add("App layout had a node of an unknown kind");
            return EmptyPane(node.Id);
        }

        if (node.Kind == LayoutNodeKind.Pane)
        {
            if (node.A is not null || node.B is not null)
            {
                node.A = null;
                node.B = null;
                repairs.Add("App layout had a pane with children");
            }

            if (node.Assignment is { } a
                && (a.Target is null || string.IsNullOrWhiteSpace(a.Target) || !Enum.IsDefined(a.Source)))
            {
                node.Assignment = null;
                repairs.Add("App layout had a pane with a broken assignment");
            }
            else if (node.Assignment is { Label: null } unlabelled)
            {
                unlabelled.Label = string.Empty;
            }

            return node;
        }

        if (depth >= MaxDepth)
        {
            repairs.Add("App layout was nested too deeply");
            return EmptyPane(node.Id);
        }

        if (node.Assignment is not null)
        {
            node.Assignment = null;
            repairs.Add("App layout had a split with an assignment");
        }

        if (!Enum.IsDefined(node.Direction))
        {
            node.Direction = SplitDirection.Vertical;
            repairs.Add("App layout had a split of an unknown direction");
        }

        if (double.IsNaN(node.Ratio) || node.Ratio < MinRatio || node.Ratio > MaxRatio)
        {
            node.Ratio = ClampRatio(node.Ratio);
            repairs.Add("App layout had a divider out of range");
        }

        if (node.A is null || node.B is null)
        {
            repairs.Add("App layout had a split with a missing side");
            var only = node.A ?? node.B;
            return only is null ? EmptyPane(node.Id) : RepairNode(only, depth, seen, repairs);
        }

        node.A = RepairNode(node.A, depth + 1, seen, repairs);
        node.B = RepairNode(node.B, depth + 1, seen, repairs);
        return node;
    }

    private static LayoutNode EmptyPane(string id) => new() { Id = id, Kind = LayoutNodeKind.Pane };

    /// <summary>How deep <paramref name="id"/> sits (the root is 0), or -1 when it is not in the tree.</summary>
    public static int DepthOf(LayoutNode? node, string id, int depth)
    {
        if (node is null)
        {
            return -1;
        }

        if (node.Id == id)
        {
            return depth;
        }

        if (node.Kind != LayoutNodeKind.Split)
        {
            return -1;
        }

        var inA = DepthOf(node.A, id, depth + 1);
        return inA >= 0 ? inA : DepthOf(node.B, id, depth + 1);
    }

    private static LayoutNode Replace(LayoutNode node, string id, Func<LayoutNode, LayoutNode> with)
    {
        if (node.Id == id)
        {
            return with(node);
        }

        if (node.Kind == LayoutNodeKind.Split)
        {
            if (node.A is not null)
            {
                node.A = Replace(node.A, id, with);
            }

            if (node.B is not null)
            {
                node.B = Replace(node.B, id, with);
            }
        }

        return node;
    }
}
