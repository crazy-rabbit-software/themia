using Microsoft.UI.Xaml;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Sdk.Items;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI.Kit;

namespace Themia.Sdk.WinUI.Items;

/// <summary>
/// [S-16958] A kit widget drawn on the item base (owner, 2026-11-23: "notifications and upcoming meetings should get the same
/// base widget as the bluetooth and drive devices"): the kit's binding keeps its source, its screens, its state row, its
/// status and its actions ([S-7061]); its items are mapped onto the base's (<see cref="Map"/>) and drawn by one
/// <see cref="ItemList"/> in the config's arrangement in place of the kit's rows. A click or Enter runs an item's primary
/// action, a right click or the Menu key shows the kit's menu of it, Delete its delete action; the end's button is the
/// widget's (<see cref="Acted"/>: Join).
/// </summary>
public sealed class ItemKit
{
    private readonly Dictionary<string, KitItem> _byId = new(StringComparer.Ordinal);
    private readonly Func<IItemList> _config;
    private Func<KitItem, IReadOnlyList<KitAction>> _actions = static _ => [];
    private Action<KitItem, KitAction> _run = static (_, _) => { };
    private bool _detached;

    /// <param name="map">A kit item as the base's, at the draw's moment.</param>
    /// <param name="offered">The parts the kind's items have, in its own order.</param>
    /// <param name="config">The config's arrangement, gap, parts and rows, read on every draw.</param>
    /// <param name="action">Its items end with an action's button (Join).</param>
    public ItemKit(IWidgetHost host, Func<KitItem, DateTimeOffset, Item> map, IReadOnlyList<ItemPart> offered, Func<IItemList> config, bool action = false)
    {
        Map = map ?? throw new ArgumentNullException(nameof(map));
        Offered = offered ?? throw new ArgumentNullException(nameof(offered));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        Action = action;
        List = new ItemList(host);
    }

    /// <summary>The list on screen.</summary>
    public ItemList List { get; }

    /// <summary>A kit item as the base's.</summary>
    public Func<KitItem, DateTimeOffset, Item> Map { get; }

    /// <summary>The parts the kind's items have.</summary>
    public IReadOnlyList<ItemPart> Offered { get; }

    /// <summary>Its items end with an action's button.</summary>
    public bool Action { get; }

    /// <summary>[S-16948] The end's button of an item was pressed (Join); not in edit mode.</summary>
    public Action<KitItem>? Acted { get; set; }

    /// <summary>Whether an item opens or acts now (false in edit mode, in a preview).</summary>
    public Func<bool>? CanOpen
    {
        get => List.CanOpen;
        set => List.CanOpen = value;
    }

    /// <summary>The kit item an item of the list is.</summary>
    public KitItem? Of(Item item) => item is not null && _byId.TryGetValue(item.Id, out var kit) ? kit : null;

    /// <summary>[S-7061] The binding's side: its actions and their carrying out, and what only the widget knows of a row.</summary>
    internal void Wire(Func<KitItem, IReadOnlyList<KitAction>> actions, Action<KitItem, KitAction> run, Action<KitItem, FrameworkElement>? decorate, Action? redrawing, Action<KitItem?>? cursor)
    {
        _actions = actions;
        _run = run;
        List.Opened = item => RunKind(item, null);
        List.Menu = Entries;
        List.Deleted = item => RunKind(item, KitActionKind.Delete);
        List.Acted = item =>
        {
            if (Of(item) is { } kit)
            {
                Acted?.Invoke(kit);
            }
        };
        List.Redrawing = redrawing;
        if (decorate is not null)
        {
            List.Decorate = (item, view) =>
            {
                if (Of(item) is { } kit)
                {
                    decorate(kit, view);
                }
            };
        }

        if (cursor is not null)
        {
            List.CursorMoved = item => cursor(item is null ? null : Of(item));
        }
    }

    /// <summary>[S-16958] Draw <paramref name="items"/> at <paramref name="now"/> in the config's arrangement.</summary>
    internal void Show(IReadOnlyList<KitItem> items, DateTimeOffset now)
    {
        _byId.Clear();
        var mapped = new List<Item>(items.Count);
        foreach (var kit in items)
        {
            _byId[kit.Id] = kit;
            mapped.Add(Map(kit, now));
        }

        List.Show(mapped, _config(), Offered, action: Action);
    }

    /// <summary>Nothing drawn (the state row says why).</summary>
    internal void Clear()
    {
        _byId.Clear();
        List.Show([], _config(), Offered, action: Action);
    }

    internal void Detach() => _detached = true;

    /// <summary>Run the item's action of <paramref name="kind"/> (null: its primary one) where it has one that can run.</summary>
    private void RunKind(Item item, KitActionKind? kind)
    {
        if (_detached || Of(item) is not { } kit)
        {
            return;
        }

        var actions = _actions(kit);
        var action = kind is { } k ? actions.FirstOrDefault(a => a.Kind == k && a.Runnable) : KitMenu.Primary(actions);
        if (action is { Runnable: true })
        {
            _run(kit, action);
        }
    }

    /// <summary>[S-7011] An item's menu: the kit's lines as the one menu builder's entries.</summary>
    private IReadOnlyList<ItemMenuEntry> Entries(Item item)
    {
        if (_detached || Of(item) is not { } kit)
        {
            return [];
        }

        return [.. KitMenu.Lines(_actions(kit)).Select(line => line.Action is not { } action
            ? ItemMenuEntry.Separator
            : new ItemMenuEntry(
                KitIcons.Glyph(action),
                action.Label,
                () =>
                {
                    if (!_detached && action.Runnable)
                    {
                        _run(kit, action);
                    }
                },
                Danger: action.Kind == KitActionKind.Delete,
                Disabled: action.Disabled,
                Checked: action.Kind == KitActionKind.Toggle ? action.Checked : null,
                Quick: line.Quick,
                Shortcut: line.Shortcut))];
    }

    /// <summary>
    /// [S-16958] A kit item as the base's by the kit's own fields: its title (or the spec's "Untitled"), its subtitle, its first
    /// badge the note, its value or its time in the spec's words the end, unread strong and read secondary where the spec tells
    /// unread, its icon lead.
    /// </summary>
    public static Item Plain(KitItem item, KitSpec spec, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(spec);
        var locale = Strings.Current;
        var when = item.Value ?? KitText.When(locale, item.When, spec.Time, now);
        var lead = item.Lead.Kind == KitLeadKind.Icon && Enum.TryParse<KitIcon>(item.Lead.Value, out var icon)
            ? new ItemLead(KitIcons.Glyph(icon), KitDraw.Tone(item.Lead.Tone, ColourRole.TextSecondary))
            : null;
        return new Item(
            item.Id,
            item.Title.Length > 0 ? item.Title : spec.Untitled(),
            lead,
            item.Subtitle ?? item.Schedule?.Span ?? string.Empty,
            when.Length > 0 ? ItemEnd.Text(when) : null,
            Note: item.Badges is [{ Text: { Length: > 0 } badge }, ..] ? badge : string.Empty,
            Tooltip: item.Tooltip ?? item.Link?.OriginalString ?? string.Empty,
            Strong: item.Emphasis,
            Muted: spec.Unread && !item.Emphasis);
    }
}
