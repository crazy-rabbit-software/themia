using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using Themia.App.Shell;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Windows.Foundation;
using Windows.Storage.Streams;

namespace Themia.Sdk.WinUI.Kit;

/// <summary>
/// [S-7060] The list part of every kit widget: the family's rows ([S-6711], <see cref="RowBlock"/>) for
/// the plan the ladder chose (<see cref="KitPlanner.Plan"/>), full-bleed with their own inset ([S-190]),
/// no separators, in columns read left to right, as many whole rows as fit under a scroll with the
/// shared fade ([S-639], [S-1281]), the card's bottom inset kept under them.
/// <para>
/// One focus model, <see cref="RowFocus"/>: a click focuses a row, a double-click or Enter runs its
/// primary action ([S-1288], [S-1306]); the card makes the list inert in edit mode ([S-1751]), so it
/// checks nothing itself. Every row's menu is built from its actions by <see cref="KitMenu"/>, fresh
/// on each right-tap; the keys are <see cref="KitKeys"/>'. Everything a row shows was written by
/// somebody else and is set as text, never as markup.
/// </para>
/// </summary>
public sealed partial class KitList : Grid
{
    private const string Scope = "kit";

    private readonly IWidgetHost _host;
    private readonly KitSpec _spec;
    private readonly Func<KitItem, IReadOnlyList<KitAction>> _actions;
    private readonly Action<KitItem, KitAction> _run;

    private readonly Grid _rows = new();
    private readonly ScrollViewer _scroll;
    private readonly List<IDisposable> _rowHooks = [];
    private readonly List<Border> _rowElements = [];

    /// <summary>Decoded site icons, one per origin, shared by this list's rows.</summary>
    private readonly Dictionary<string, Task<BitmapImage?>> _icons = new(StringComparer.OrdinalIgnoreCase);

    private ScrollFade? _fade;

    /// <summary>[S-2050] The clock a redraw of its own reads (the binding's).</summary>
    internal Func<DateTimeOffset> Clock { get; set; } = static () => DateTimeOffset.Now;

    /// <summary>[S-7037] A show held back under a drag or an editor, drawn when they let go.</summary>
    private (IReadOnlyList<KitItem> Items, KitPlan Plan, WidgetArea Area, DateTimeOffset Now)? _held;
    private IReadOnlyList<KitItem> _items = [];
    private KitPlan _plan;
    private WidgetArea _area;

    /// <summary>The keyboard cursor, by item id, and where it was (for an item that left the list).</summary>
    private string? _cursorId;
    private int _cursorIndex = -1;
    private bool _detached;

    /// <param name="actions">An item's actions, as its spec gives them (read fresh for every menu).</param>
    /// <param name="run">Carries out an action the user chose (<see cref="KitBinding"/>'s runner).</param>
    public KitList(IWidgetHost host, KitSpec spec, Func<KitItem, IReadOnlyList<KitAction>> actions, Action<KitItem, KitAction> run)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(run);

        _host = host;
        _spec = spec;
        _actions = actions;
        _run = run;

        _scroll = new ScrollViewer
        {
            Content = _rows,
            HorizontalScrollMode = ScrollMode.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalAlignment = VerticalAlignment.Top,
        };

        // [S-6712] The rows stand in the view's top, the card's bottom inset under them.
        Padding = new Thickness(0, 0, 0, FamilySpace.Inset);

        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _columnHeader.Visibility = Visibility.Collapsed;
        SetRow(_columnHeader, 0);
        SetRow(_scroll, 1);
        SetRow(_page, 1);
        Children.Add(_columnHeader);

        if (spec.Look == KitRowLook.Check)
        {
            // [S-7037] The check look's host gives the scroll only its whole lines and fades them; the
            // add row sits under it ([S-3850]).
            BuildCheckParts();
            Children.Add(_checkHost!);
            Children.Add(_addRow!.Element);
            return;
        }

        // [S-7035] A paged list draws its page in the slot; it never scrolls.
        Children.Add(spec.Overflow == KitOverflow.Page ? _page : _scroll);
    }

    /// <summary>The items shown, in order.</summary>
    public IReadOnlyList<KitItem> Items => _items;

    /// <summary>[S-1564] An empty list takes no keyboard.</summary>
    public bool WantsKeys => _items.Count > 0;

    /// <summary>The focused item's place, or negative for none.</summary>
    public int CursorIndex => _cursorIndex;

    /// <summary>Hook the fade; once per attach.</summary>
    public void Attach()
    {
        _detached = false;
        if (!Paged && !Checked)
        {
            _fade ??= new ScrollFade(_scroll, _rows);
        }
    }

    /// <summary>Draw <paramref name="items"/> for <paramref name="plan"/> in <paramref name="area"/>; the cursor follows its item.</summary>
    public void Show(IReadOnlyList<KitItem> items, KitPlan plan, WidgetArea area, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (_detached)
        {
            return;
        }

        if (Checked && HoldBack(items))
        {
            _held = (items, plan, area, now);
            return;
        }

        _held = null;
        _items = items;
        _plan = plan;
        _area = area;

        if (_cursorId is not null)
        {
            var at = IndexOf(_cursorId);
            _cursorIndex = at >= 0 ? at : ListKeys.AfterRemoval(_cursorIndex, items.Count);
            _cursorId = _cursorIndex >= 0 ? items[_cursorIndex].Id : null;
        }

        BuildRows(now);
    }

    /// <summary>Take every row down (an empty or problem screen is up).</summary>
    public void Clear() => Clear(_plan);

    /// <summary>Take every row down for another screen; a check look's add row follows <paramref name="plan"/> (none without a list).</summary>
    public void Clear(KitPlan plan)
    {
        if (Checked && _editor is { Id: not null })
        {
            CloseEditor();
        }

        ClearRows();
        _items = [];
        HideAddRow(plan);
    }

    public void Detach()
    {
        if (_detached)
        {
            return;
        }

        CancelDrag();
        CloseEditor();
        _detached = true;
        ClearRows();
        _addRow?.Detach();
        _fade?.Dispose();
        _fade = null;
    }

    // ---- rows --------------------------------------------------------------------------------

    private void BuildRows(DateTimeOffset now)
    {
        ClearRows();

        if (Checked)
        {
            BuildChecks();
            if (_cursorIndex >= 0 && _cursorIndex < _rowElements.Count)
            {
                Mark(_cursorIndex, true);
            }

            return;
        }

        if (Paged)
        {
            BuildPage(now);
            if (_cursorIndex >= 0 && _cursorIndex < _rowElements.Count)
            {
                Mark(_cursorIndex, true);
            }

            return;
        }

        var columns = Math.Max(1, _plan.Columns);

        for (var c = 0; c < columns; c++)
        {
            _rows.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        for (var r = 0; r < (_items.Count + columns - 1) / columns; r++)
        {
            _rows.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        if (_spec.Look == KitRowLook.Table)
        {
            BuildColumnHeader();
        }
        else
        {
            _columnHeader.Visibility = Visibility.Collapsed;
        }

        for (var i = 0; i < _items.Count; i++)
        {
            var row = BuildFamilyRow(_items[i], i, now);
            SetRow(row, i / columns);
            SetColumn(row, i % columns);
            _rows.Children.Add(row);
            _rowElements.Add(row);
        }

        if (_cursorIndex >= 0 && _cursorIndex < _rowElements.Count)
        {
            Mark(_cursorIndex, true);
        }

        FitWholeRows(columns);
        _fade?.Refresh();
    }

    /// <summary>
    /// [S-639] [S-6712] The list is given the height of the rows that fit whole (each of the plan's one
    /// height); the rest is a scroll away.
    /// </summary>
    private void FitWholeRows(int columns)
    {
        if (!_area.IsMeasured || _rowElements.Count == 0)
        {
            _scroll.Height = double.NaN;
            return;
        }

        var lines = (_rowElements.Count + columns - 1) / columns;
        var shown = Math.Max(1, Math.Min(lines, KitPlanner.Fit(_plan, KitPlanner.ListHeight(_area))));
        _scroll.Height = shown * _plan.RowHeight;
    }

    /// <summary>[S-1304] The site's own icon (the host's one cache), with a globe in its place until it arrives or when the site has none.</summary>
    private Grid SiteIcon(string link)
    {
        var globe = Family.Glyph(Glyphs.Globe, FamilyShape.Icon, ColourRole.TextSecondary);
        globe.HorizontalAlignment = HorizontalAlignment.Center;

        var image = new Image { Width = FamilyShape.Icon, Height = FamilyShape.Icon, Visibility = Visibility.Collapsed };
        var holder = new Grid { Width = FamilyShape.Icon, Height = FamilyShape.Icon, VerticalAlignment = VerticalAlignment.Center };
        holder.Children.Add(globe);
        holder.Children.Add(image);

        _ = ShowIconAsync(link, image, globe);
        return holder;
    }

    private async Task ShowIconAsync(string link, Image image, FontIcon globe)
    {
        if (SiteIcons.FaviconOf(link) is not { } icon)
        {
            return;
        }

        var origin = icon.GetLeftPart(UriPartial.Authority);

        if (!_icons.TryGetValue(origin, out var decoding))
        {
            decoding = DecodeIconAsync(link);
            _icons[origin] = decoding;
        }

        var bitmap = await decoding.ConfigureAwait(true);

        if (_detached || bitmap is null)
        {
            return;
        }

        image.ImageFailed += (_, _) =>
        {
            image.Visibility = Visibility.Collapsed;
            globe.Visibility = Visibility.Visible;
        };

        image.Source = bitmap;
        image.Visibility = Visibility.Visible;
        globe.Visibility = Visibility.Collapsed;
    }

    private async Task<BitmapImage?> DecodeIconAsync(string link)
    {
        var bytes = await _host.Favicons.GetAsync(link).ConfigureAwait(true);

        if (bytes is null)
        {
            return null;
        }

        try
        {
            using var stream = new InMemoryRandomAccessStream();
            await stream.WriteAsync(bytes.AsBuffer());
            stream.Seek(0);

            // Decoded at twice the drawn size, so the icon is sharp at 200% scale and no bigger.
            var bitmap = new BitmapImage { DecodePixelWidth = (int)(FamilyShape.Icon * 2) };
            await bitmap.SetSourceAsync(stream);
            return bitmap;
        }
        catch (Exception ex) when (ex is COMException or ArgumentException or InvalidOperationException)
        {
            // Bytes that passed the magic-number check and still would not decode: a broken icon,
            // and the globe stays.
            Log.Debug(Scope, $"a site icon would not decode: {ex.Message}");
            return null;
        }
    }

    private void ClearRows()
    {
        foreach (var hook in _rowHooks)
        {
            hook.Dispose();
        }

        _rowHooks.Clear();
        _times.Clear();

        foreach (var row in _rowElements)
        {
            row.Tapped -= OnRowTapped;
            row.DoubleTapped -= OnRowDoubleTapped;
        }

        _rowElements.Clear();
        _checksByRow.Clear();
        _checks?.Children.Clear();
        _page.Children.Clear();
        _rows.Children.Clear();
        _rows.ColumnDefinitions.Clear();
        _rows.RowDefinitions.Clear();
    }

    // ---- pointer, menu, keys -----------------------------------------------------------------

    /// <summary>A click focuses the row, and is the row's: it never reaches the card ([S-2004]).</summary>
    private void OnRowTapped(object sender, TappedRoutedEventArgs args)
    {
        args.Handled = true;

        if (sender is Border { Tag: int index })
        {
            Focus(index);
        }
    }

    /// <summary>A double-click runs the row's primary action.</summary>
    private void OnRowDoubleTapped(object sender, DoubleTappedRoutedEventArgs args)
    {
        if (sender is Border { Tag: int index } && index < _items.Count && KitMenu.Primary(_actions(_items[index])) is { } primary)
        {
            args.Handled = true;
            Run(_items[index], primary);
        }
    }

    /// <summary>[S-7011] An item's menu lines as the one menu builder's entries.</summary>
    private IEnumerable<ItemMenuEntry> Entries(KitItem item)
    {
        if (_detached)
        {
            return [];
        }

        return KitMenu.Lines(_actions(item)).Select(line => line.Action is not { } action
            ? ItemMenuEntry.Separator
            : new ItemMenuEntry(
                KitIcons.Glyph(action),
                action.Label,
                () => Run(item, action),
                Danger: action.Kind == KitActionKind.Delete,
                Disabled: action.Disabled,
                Checked: action.Kind == KitActionKind.Toggle ? action.Checked : null,
                Quick: line.Quick,
                Shortcut: line.Shortcut));
    }

    private void Run(KitItem item, KitAction action)
    {
        if (!_detached && action.Runnable)
        {
            _run(item, action);
        }
    }

    /// <summary>
    /// [S-7020] A key from the shell's keyboard lease ([S-800]; a click in the widget takes it).
    /// Returns whether the list used it.
    /// </summary>
    public bool HandleKey(WidgetKey key)
    {
        if (_detached)
        {
            return false;
        }

        // [S-808] An open editor owns its keys; the lease ending while one is open only forgets the
        // row, and the box's blur commits it.
        if (_editor is not null)
        {
            if (key == WidgetKey.Escape)
            {
                Release();
            }

            return false;
        }

        if (_items.Count == 0)
        {
            return false;
        }

        var had = _cursorIndex;
        var decision = KitKeys.Decide(key, _cursorIndex, _items.Count);

        // [S-7035] [S-7020] With nothing focused a paged list's movement key lands on the page on
        // screen: its first row, End its last.
        if (Paged && had < 0 && decision.Command == KitKeyCommand.Focus)
        {
            var start = _pageStarts[Math.Clamp(_pageIndex, 0, _pageStarts.Count - 1)];
            var end = _pageIndex + 1 < _pageStarts.Count ? _pageStarts[_pageIndex + 1] - 1 : _items.Count - 1;
            decision = decision with { Index = key == WidgetKey.End ? end : start };
        }

        switch (decision.Command)
        {
            case KitKeyCommand.Focus:
                Focus(decision.Index);
                return had != decision.Index;

            case KitKeyCommand.Release:
                Release();
                return had >= 0;

            case KitKeyCommand.Menu when decision.Index < _rowElements.Count:
                return ItemMenu.Open(_rowElements[decision.Index], Entries(_items[decision.Index]));

            case KitKeyCommand.Open:
                return RunKind(decision.Index, null);

            case KitKeyCommand.Toggle:
                return Checked ? Toggle(decision.Index) : RunKind(decision.Index, KitActionKind.Toggle);

            case KitKeyCommand.MoveUp or KitKeyCommand.MoveDown:
                return Move(decision.Index, decision.Command == KitKeyCommand.MoveUp ? -1 : 1);

            case KitKeyCommand.Rename:
                return RunKind(decision.Index, KitActionKind.Rename);

            case KitKeyCommand.Delete:
                return RunKind(decision.Index, KitActionKind.Delete);

            case KitKeyCommand.Copy:
                return RunCopy(decision.Index);

            default:
                return false;
        }
    }

    /// <summary>Run the focused item's action of <paramref name="kind"/> (null: its primary one), if it has one that can run.</summary>
    private bool RunKind(int index, KitActionKind? kind)
    {
        var item = _items[index];
        var actions = _actions(item);
        var action = kind is { } k ? actions.FirstOrDefault(a => a.Kind == k && a.Runnable) : KitMenu.Primary(actions);

        if (action is null)
        {
            return false;
        }

        Run(item, action);
        return true;
    }

    /// <summary>[S-7020] Ctrl+C: the focused item's first copy, as its menu says.</summary>
    private bool RunCopy(int index)
    {
        var item = _items[index];
        var copy = _actions(item).FirstOrDefault(a => a.Kind is KitActionKind.CopyLink or KitActionKind.CopyText);
        if (copy is not { Runnable: true })
        {
            return false;
        }

        Run(item, copy);
        return true;
    }

    /// <summary>Put the cursor on the item at <paramref name="index"/>.</summary>
    public void Focus(int index)
    {
        if (index < 0 || index >= _rowElements.Count)
        {
            return;
        }

        if (_cursorIndex >= 0 && _cursorIndex < _rowElements.Count)
        {
            Mark(_cursorIndex, false);
        }

        _cursorIndex = index;
        _cursorId = _items[index].Id;

        // [S-7035] The cursor takes the page with it (a turned page is drawn with the cursor on it).
        TurnTo(index);
        Mark(index, true);
    }

    /// <summary>Drop the cursor.</summary>
    public void Release()
    {
        if (_cursorIndex >= 0 && _cursorIndex < _rowElements.Count)
        {
            Mark(_cursorIndex, false);
        }

        _cursorIndex = -1;
        _cursorId = null;
    }

    private int IndexOf(string id)
    {
        for (var i = 0; i < _items.Count; i++)
        {
            if (string.Equals(_items[i].Id, id, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }
}
