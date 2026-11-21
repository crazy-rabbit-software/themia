using Themia.Core.Model;
using Themia.Sdk;

namespace Themia.Core.Desktop;

/// <summary>
/// [S-15666] One view's fences looked up once: its fences in the view's order, each desktop item's icon by its key (the
/// first, the one a view keeps, [S-13015]), and the icons the fences hold by widget id with their fence and place
/// (<see cref="Members"/>). Built from the view as it is (<see cref="Of"/>) and read as long as the view is unchanged: the
/// host keeps one per view between two changes of the state, so a drag step's questions (<see cref="FenceAt"/>), the
/// surface's paint order and what the fences hide are answered without walking the view again. A rule that changes the
/// view builds its own.
/// </summary>
public sealed class FenceIndex
{
    private readonly Dictionary<string, WidgetInstance> _icons;
    private readonly Dictionary<string, (FenceOn Fence, int Index)> _members;
    private readonly Dictionary<string, FenceOn> _fencesById;
    private readonly Dictionary<string, FenceOn> _holding;

    private FenceIndex(ViewState view, List<FenceOn> fences, Dictionary<string, WidgetInstance> icons, Dictionary<string, (FenceOn, int)> members, Dictionary<string, FenceOn> byId, Dictionary<string, FenceOn> holding)
    {
        View = view;
        Fences = fences;
        _icons = icons;
        _members = members;
        _fencesById = byId;
        _holding = holding;
    }

    /// <summary>The view it was built from.</summary>
    public ViewState View { get; }

    /// <summary>The view's fences, in its widgets' order.</summary>
    public IReadOnlyList<FenceOn> Fences { get; }

    /// <summary>Whether the view has a fence.</summary>
    public bool HasFences => Fences.Count > 0;

    /// <summary>[S-13042] The icons the fences hold, by widget id: the fence and the item's place in its order.</summary>
    public IReadOnlyDictionary<string, (FenceOn Fence, int Index)> Members => _members;

    /// <summary>The view's fences and their icons, read now.</summary>
    public static FenceIndex Of(ViewState view)
    {
        ArgumentNullException.ThrowIfNull(view);

        var fences = new List<FenceOn>();
        var icons = new Dictionary<string, WidgetInstance>(DesktopItemKeys.Comparer);
        var byId = new Dictionary<string, FenceOn>(StringComparer.Ordinal);
        foreach (var widget in view.Widgets)
        {
            if (widget.Config is IHoldsDesktopItems fence)
            {
                var on = new FenceOn(widget, fence);
                fences.Add(on);
                byId.TryAdd(widget.Id, on);
            }

            if (widget.Config is IDesktopItemConfig { Item.Length: > 0 } item)
            {
                icons.TryAdd(item.Item, widget);
            }
        }

        var members = new Dictionary<string, (FenceOn, int)>(StringComparer.Ordinal);
        var holding = new Dictionary<string, FenceOn>(DesktopItemKeys.Comparer);
        foreach (var on in fences)
        {
            for (var i = 0; i < on.Fence.Items.Count; i++)
            {
                var key = on.Fence.Items[i];
                holding.TryAdd(key, on);
                if (icons.TryGetValue(key, out var icon))
                {
                    members.TryAdd(icon.Id, (on, i));
                }
            }
        }

        return new FenceIndex(view, fences, icons, members, byId, holding);
    }

    /// <summary>[S-13015] The icon of the desktop item <paramref name="key"/> on the view, or null.</summary>
    public WidgetInstance? Icon(string key) => _icons.TryGetValue(key, out var icon) ? icon : null;

    /// <summary>[S-13001] The fence of the view that holds <paramref name="key"/> (the first in the view's order), or null.</summary>
    public FenceOn? Holding(string key) => _holding.TryGetValue(key, out var fence) ? fence : null;

    /// <summary>Whether the desktop item <paramref name="key"/> has an icon on the view.</summary>
    public bool HasIcon(string key) => _icons.ContainsKey(key);

    /// <summary>
    /// [S-13041] [S-13047] The fence the widget <paramref name="widgetId"/> stands for to the desktop's own icons: that fence
    /// itself, or the fence that holds that icon; null for any other widget.
    /// </summary>
    public FenceOn? FenceAt(string widgetId)
        => _fencesById.TryGetValue(widgetId, out var fence) ? fence
            : _members.TryGetValue(widgetId, out var member) ? member.Fence : null;

    /// <summary>[S-13042] The free desktop icons of the view (an own icon too): every item widget no fence holds.</summary>
    public List<WidgetInstance> FreeIcons()
        => [.. View.Widgets.Where(w => w.Config is IDesktopItemConfig && !_members.ContainsKey(w.Id))];
}
