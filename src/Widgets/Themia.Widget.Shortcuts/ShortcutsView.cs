using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.Shortcuts;

namespace Themia.App.Widgets;

/// <summary>
/// The links, files, folders and Windows locations somebody keeps to hand, as tiles, a list or a
/// table ([S-1380]), with the icon Explorer would show for each ([S-1383]).
/// <para>
/// <b>Every target is re-checked at the moment it is opened</b>, by <see cref="ShellLauncher"/>.
/// The config file it came from lives in the user's profile and anything running as the user can
/// rewrite it, so a shortcut that was valid when it was added is not evidence that it is valid now.
/// A target the rule refuses is drawn as a refused item rather than quietly dropped. Looking at a
/// target (its icon, whether it is still there) is gated too: a network path is never probed
/// unasked ([S-1386]).
/// </para>
/// <para>
/// Split by concern: this file owns the lifecycle, the ladder and the widget-level commands;
/// <c>.Cells.cs</c> builds and paints tiles and rows and loads their icons; <c>.Input.cs</c> owns
/// selection, the rubber band, keys, the item menu, rename and search.
/// </para>
/// <para>
/// Items only ever enter through <see cref="AddItems"/> — a drop too ([S-1400], <c>.Drop.cs</c>,
/// which also owns dragging items to reorder them and out to other apps). An app layout
/// ([S-1405]) is an item with a pane tree instead of a target: its icon, menu, detail and open each
/// branch on <see cref="ShortcutItem.IsLayout"/> before the target is classified.
/// </para>
/// </summary>
internal sealed partial class ShortcutsView : Grid, IWidgetView, IWidgetSurfaceAware, IWidgetHeaderContent,
    IWidgetContextActions, IWidgetSizeAware, IWidgetKeys, IWidgetDropTarget
{
    private const string Scope = "shortcuts";

    /// <summary>How often local targets are looked at again for the broken state ([S-1386]).</summary>
    private static readonly TimeSpan HealthInterval = TimeSpan.FromMinutes(1);

    private readonly TextBox _search = new();
    private readonly Border _searchHost;
    private readonly Grid _columnHeader = new();
    private readonly Grid _panel = new();
    private readonly Canvas _bandLayer = new() { IsHitTestVisible = false };
    private readonly Border _band = new() { Visibility = Visibility.Collapsed, IsHitTestVisible = false };
    private readonly SelectionLabel _fullName;
    private readonly Grid _surface = new();
    private readonly ScrollViewer _scroll;
    private readonly HeaderParts _header = new();
    private readonly Border _searchButton;
    private readonly ScrollFade _fade;

    private readonly ItemSelection _selection = new();

    /// <summary>[S-6626] Whether the widget has the keyboard: a selection without it is drawn inactive.</summary>
    private bool _active;
    private readonly List<Cell> _cells = [];
    private readonly Dictionary<string, TargetHealth> _health = new(StringComparer.OrdinalIgnoreCase);

    private ShortcutsConfig _config = new();
    private IWidgetSurface? _surfaceHost;
    private Ticker? _ticker;
    private IDisposable? _healthTick;
    private IDisposable? _binWatch;
    private WidgetArea _area;
    private CellShape _shape;
    private string _query = string.Empty;
    private bool _searchOpen;
    private bool _detached;
    private int _healthGeneration;

    /// <summary>The last batch removed, with where each item was, for one step of "Undo remove".</summary>
    private List<(int Index, ShortcutItem Item)>? _lastRemoved;

    /// <summary>[S-6913] What the view asks of the host: the launcher, ⚙ › Accounts.</summary>
    private readonly IWidgetHost _widgetHost;

    public ShortcutsView(IWidgetHost host)
    {
        _widgetHost = host;
        Background = Family.Brush(ColourRole.Clear);

        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        _search.PlaceholderText = ShortcutsStr.WidgetsC_Shortcuts_Search.Text();
        _search.IsSpellCheckEnabled = false;
        _search.IsTextPredictionEnabled = false;
        _search.Height = FamilyShape.Button;
        _search.TextChanged += OnSearchChanged;
        _search.KeyDown += OnSearchKeyDown;
        _search.GotFocus += OnTextGotFocus;
        _search.LostFocus += OnTextLostFocus;

        // [S-1385] The search box: a 32 text box at the side inset, 8 over the items.
        _searchHost = new Border
        {
            Child = _search,
            Padding = new Thickness(FamilySpace.Inset, 0, FamilySpace.Inset, FamilySpace.S2),
            Visibility = Visibility.Collapsed,
        };

        _columnHeader.Visibility = Visibility.Collapsed;

        // [S-6624] The desktop's rubber band, and [S-6629] a selected tile's full name, over the items.
        SelectionChrome.PaintBand(_band);
        _bandLayer.Children.Add(_band);
        _fullName = new SelectionLabel(_bandLayer);

        _surface.Background = Family.Brush(ColourRole.Clear);
        _surface.Children.Add(_panel);
        _surface.Children.Add(_bandLayer);

        _scroll = new ScrollViewer
        {
            Content = _surface,
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalScrollMode = ScrollMode.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
        };

        _fade = new ScrollFade(_scroll, _panel);

        // [S-1385] [S-6710] The header's one button: search.
        _searchButton = _header.Add(HeaderPartKind.Button, HeaderBlock.Button(Glyphs.Search, ShortcutsStr.WidgetsC_Shortcuts_Search.Text(), ToggleSearch));

        SetRow(_searchHost, 0);
        SetRow(_columnHeader, 1);
        SetRow(_scroll, 2);

        Children.Add(_searchHost);
        Children.Add(_columnHeader);
        Children.Add(_scroll);

        PointerPressed += OnRootPointerPressed;
        PointerMoved += OnRootPointerMoved;
        PointerReleased += OnRootPointerReleased;
        PointerCaptureLost += OnRootPointerLost;
        PointerCanceled += OnRootPointerLost;
    }

    /// <inheritdoc />
    public FrameworkElement? HeaderTrailing => _header;

    public void Bind(IWidgetSurface surface) => _surfaceHost = surface;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _ticker = ticker;
        Strings.Changed -= OnLanguage;
        Strings.Changed += OnLanguage;
        ApplyConfig(config);
    }

    /// <summary>
    /// The language changed: the texts set once in the constructor are set again, and the cells
    /// are rebuilt, since their kind labels, details and tooltips are words too.
    /// </summary>
    private void OnLanguage(Locale locale)
    {
        if (_detached)
        {
            return;
        }

        _search.PlaceholderText = ShortcutsStr.WidgetsC_Shortcuts_Search.Text();
        ToolTipService.SetToolTip(_searchButton, ShortcutsStr.WidgetsC_Shortcuts_Search.Text());
        Relayout();
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as ShortcutsConfig ?? new ShortcutsConfig();
        _selection.SetOrder(_config.Items.Select(item => item.Id).ToList());
        Changed(rebuild: true);
    }

    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Lay();
    }

    /// <summary>
    /// [S-1392] Add items (the properties menu and a drop, [S-1400]). The one way an item enters the
    /// widget from its own surface.
    /// </summary>
    public void AddItems(IEnumerable<ShortcutItem> items)
    {
        var added = false;

        foreach (var item in items)
        {
            _config.Items.Add(item);
            added = true;
        }

        if (added)
        {
            Save();
            Changed(rebuild: true);
        }
    }

    /// <summary>
    /// Something about the items changed: re-run the checks that depend on them and redraw. The
    /// health and bin subscriptions follow what the list now holds.
    /// </summary>
    private void Changed(bool rebuild)
    {
        if (_detached)
        {
            return;
        }

        UpdateSubscriptions();

        if (rebuild)
        {
            _shape = default;
        }

        Lay();
    }

    /// <summary>Redraw for a change that does not touch the items (a search): no checks re-run.</summary>
    private void Relayout()
    {
        _shape = default;
        Lay();
    }

    private void UpdateSubscriptions()
    {
        if (_ticker is null)
        {
            return;
        }

        var probes = _config.Items.Any(item => LaunchTargets.Classify(item.Target) is { Kind: LaunchKind.LocalPath } target
            && LaunchTargets.MayProbe(target));

        if (probes && _healthTick is null)
        {
            // Fires once now, then every minute: a target deleted while the widget is up shows as
            // broken within the minute, and nothing is looked at per frame.
            _healthTick = _ticker.Subscribe(HealthInterval, CheckHealth);
        }
        else if (probes)
        {
            CheckHealth();
        }
        else if (_healthTick is not null)
        {
            _healthTick.Dispose();
            _healthTick = null;
        }

        var bin = _config.Items.Any(item => ShortcutRules.IsRecycleBin(item.Target));

        if (bin && _binWatch is null)
        {
            _widgetHost.RecycleBin.Changed += OnBinChanged;
            _binWatch = _widgetHost.RecycleBin.Watch();
        }
        else if (!bin && _binWatch is not null)
        {
            _widgetHost.RecycleBin.Changed -= OnBinChanged;
            _binWatch.Dispose();
            _binWatch = null;
        }
    }

    /// <summary>
    /// [S-1386] Look at every probeable local target on the thread pool (an existence check on a
    /// sleeping drive blocks), then repaint the items whose answer changed.
    /// </summary>
    private void CheckHealth()
    {
        var targets = _config.Items
            .Select(item => LaunchTargets.Classify(item.Target))
            .Where(target => target.Kind == LaunchKind.LocalPath && LaunchTargets.MayProbe(target))
            .Select(target => target.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (targets.Count == 0)
        {
            return;
        }

        var generation = ++_healthGeneration;
        _ = CheckHealthAsync(targets, generation);
    }

    private async Task CheckHealthAsync(List<string> targets, int generation)
    {
        Dictionary<string, TargetHealth> answers;

        try
        {
            answers = await Task.Run(() => targets.ToDictionary(
                target => target,
                target => ShortcutRules.Check(LaunchTargets.Classify(target), Probe),
                StringComparer.OrdinalIgnoreCase)).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, "could not check whether the shortcut targets are still there", ex);
            return;
        }

        if (_detached || generation != _healthGeneration)
        {
            return;
        }

        var changed = false;

        foreach (var (target, health) in answers)
        {
            if (!_health.TryGetValue(target, out var was) || was != health)
            {
                _health[target] = health;
                changed = true;
            }
        }

        if (changed)
        {
            foreach (var cell in _cells)
            {
                PaintHealth(cell);
            }
        }
    }

    /// <summary>Whether a local path is there, and what it is. Never throws; unclear is Unknown.</summary>
    private static TargetHealth Probe(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                return TargetHealth.Folder;
            }

            return File.Exists(path) ? TargetHealth.File : TargetHealth.Missing;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Log.Debug(Scope, $"could not tell whether a shortcut target exists: {ex.Message}");
            return TargetHealth.Unknown;
        }
    }

    private void OnBinChanged()
    {
        if (_detached)
        {
            return;
        }

        foreach (var cell in _cells)
        {
            if (cell.IsBin)
            {
                PaintBin(cell);
            }
        }
    }

    /// <summary>
    /// The ladder ([S-1382]..[S-1384]): the search box (when open) takes its line first; the items
    /// take the rest (no status footer at any size), laid out by the view's plan. A change of plan rebuilds the
    /// cells; anything else repaints them.
    /// </summary>
    private void Lay()
    {
        if (_detached)
        {
            return;
        }

        var visible = Visible();
        var hasItems = _config.Items.Count > 0;

        // No items: the card's Set up button covers this ([S-1862]); a drop still lands here.
        _scroll.Visibility = hasItems ? Visibility.Visible : Visibility.Collapsed;
        _searchHost.Visibility = _searchOpen && hasItems ? Visibility.Visible : Visibility.Collapsed;
        _header.Want(_searchButton, hasItems);

        if (!hasItems)
        {
            _columnHeader.Visibility = Visibility.Collapsed;
            ClearCells();
            return;
        }

        // The view's area less the card's bottom inset and the open search box: the items' room.
        var area = _area.IsMeasured ? _area : new WidgetArea(320, 200);
        var height = Math.Max(0, area.Height - FamilySpace.Inset - (_searchOpen ? ShortcutLayout.SearchHeight : 0));
        var width = Math.Max(0, area.Width - (2 * FamilySpace.Inset));

        CellShape shape;

        if (_config.Style == ShortcutsStyle.Grid)
        {
            var tiles = ShortcutLayout.Grid(area, width, height, visible.Count, _config.GridIconOnly);
            shape = new CellShape(_config.Style, tiles, default, visible.Count, width);

            // [S-6713] A cell's mark stands 4 around it: the marks start 4 over the items' top and 12 in
            // from the edge, and only whole rows of cells show.
            _scroll.Padding = new Thickness(FamilySpace.S3, 0, FamilySpace.S3, 0);
            _scroll.Margin = new Thickness(0, -FamilySpace.S1, 0, 0);
            _scroll.Height = Math.Max(1, tiles.Rows) * tiles.PitchY;
        }
        else
        {
            var rows = ShortcutLayout.Rows(area, _config.Style == ShortcutsStyle.Details);

            // The column titles cost a Caption line and 8; only where the list keeps two rows under them.
            if (rows.ColumnHeader && RowRules.Fit(height - KitLadder.ColumnHeaderHeight, rows.Lines) < 2)
            {
                rows = rows with { ColumnHeader = false };
            }

            shape = new CellShape(_config.Style, default, rows, visible.Count, width);

            // [S-6712] As tall as its whole rows: never a part of one at its foot.
            _scroll.Padding = new Thickness(0);
            _scroll.Margin = new Thickness(0);
            _scroll.Height = Math.Max(1, RowRules.Fit(height - (rows.ColumnHeader ? KitLadder.ColumnHeaderHeight : 0), rows.Lines)) * rows.RowHeight;
        }

        _surface.MinHeight = Math.Max(0, _scroll.Height - _scroll.Padding.Top - _scroll.Padding.Bottom);

        if (shape != _shape)
        {
            _shape = shape;
            Render(visible);
        }
        else
        {
            PaintAll();
        }
    }

    /// <summary>The items a search leaves, in order.</summary>
    private List<ShortcutItem> Visible()
    {
        if (!_searchOpen || _query.Trim().Length == 0)
        {
            return _config.Items;
        }

        return _config.Items
            .Where(item => ShortcutRules.Matches(NameOf(item), item.Target, _query))
            .ToList();
    }

    private string NameOf(ShortcutItem item) => item.IsLayout
        ? (item.Label.Length > 0 ? item.Label : ShortcutRules.LayoutKindLabel())
        : LaunchTargets.DisplayName(item.Label, item.Target, _config.ShowFileExtension);

    /// <summary>The visible ids, in order: what selection ranges, select-all and arrows walk.</summary>
    private List<string> Order() => _cells.Select(cell => cell.Item.Id).ToList();

    /// <summary>Persist an edit the widget made to its own config.</summary>
    private void Save() => _surfaceHost?.RequestSave();

    /// <summary>
    /// [S-1391] The widget's own commands on the card's menu (original S-27 / S-49): search, the
    /// click, select all and one step of undo for the last remove. Each one names the state it
    /// switches to. [S-1746] Nothing here changes how the widget looks.
    /// </summary>
    public IReadOnlyList<WidgetAction> ContextActions
    {
        get
        {
            var actions = new List<WidgetAction>();

            if (_config.Items.Count > 0)
            {
                actions.Add(new WidgetAction(Glyphs.Search,
                    _searchOpen ? ShortcutsStr.WidgetsC_Shortcuts_Menu_CloseSearch.Text() : ShortcutsStr.Common_Search.Text(), ToggleSearch));
            }

            actions.Add(new WidgetAction(Glyphs.Open,
                _config.ClickMode == ItemClickMode.Double ? ShortcutsStr.CtxMenu_OpenSingle.Text() : ShortcutsStr.CtxMenu_OpenDouble.Text(),
                () => SetConfig(c => c.ClickMode = c.ClickMode == ItemClickMode.Double ? ItemClickMode.Single : ItemClickMode.Double)));

            if (_cells.Count > 0)
            {
                actions.Add(new WidgetAction(Glyphs.Done, Str.Folder_SelectAll.Text(), SelectAll));
            }

            if (_lastRemoved is { Count: > 0 } removed)
            {
                actions.Add(new WidgetAction(Glyphs.Undo,
                    removed.Count == 1 ? ShortcutsStr.WidgetsC_Shortcuts_Menu_UndoRemove.Text() : ShortcutsStr.WidgetsC_Shortcuts_Menu_UndoRemoveCount.Plural(removed.Count),
                    UndoRemove));
            }

            return actions;
        }
    }

    private void SetConfig(Action<ShortcutsConfig> change)
    {
        change(_config);
        Save();
        Changed(rebuild: true);
    }

    public void Detach()
    {
        if (_detached)
        {
            return;
        }

        _detached = true;
        Strings.Changed -= OnLanguage;

        _healthTick?.Dispose();
        _healthTick = null;

        if (_binWatch is not null)
        {
            _widgetHost.RecycleBin.Changed -= OnBinChanged;
            _binWatch.Dispose();
            _binWatch = null;
        }

        _search.TextChanged -= OnSearchChanged;
        _search.KeyDown -= OnSearchKeyDown;
        _search.GotFocus -= OnTextGotFocus;
        _search.LostFocus -= OnTextLostFocus;

        PointerPressed -= OnRootPointerPressed;
        PointerMoved -= OnRootPointerMoved;
        PointerReleased -= OnRootPointerReleased;
        PointerCaptureLost -= OnRootPointerLost;
        PointerCanceled -= OnRootPointerLost;

        EndRename(commit: false);
        EndReorder(commit: false);
        LeaveDrop();
        _fade.Dispose();
        ClearCells();
    }

    /// <summary>What the cells were last built for; a different one rebuilds them.</summary>
    private readonly record struct CellShape(ShortcutsStyle View, CellGrid Tiles, ShortcutRowPlan Rows, int Count, double Width);
}
