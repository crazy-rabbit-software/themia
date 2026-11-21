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
using Windows.Storage.Streams;

namespace Themia.Sdk.WinUI.Kit;

/// <summary>
/// [S-7060] The list part of every kit widget, the shell its rows stand in: it knows no look. A look
/// (<see cref="IKitLook"/>; the spec's own, <see cref="KitLooks.For"/>, or one the widget hands it) draws each item as the
/// family's Row block ([S-6711]); the list lays the rows for the plan (<see cref="KitPlanner.Plan"/>) at their one fixed
/// size ([S-14810]), full-bleed with their own inset ([S-190]), no separators, in columns read left to right, a group's
/// heading over its first row, as many as the card reaches under a scroll with the shared fade ([S-639], [S-1281]), their
/// text reaching the view's top and the foot's inset ([S-12807]); or in pages ([S-7035]); or in the check layout
/// ([S-7037]).
/// <para>
/// One focus model, <see cref="RowFocus"/>: a click focuses a row, a double-click or Enter runs its primary action
/// ([S-1288], [S-1306]; a list whose spec says so runs it on the click too, <see cref="KitSpec.ClickRuns"/>); the card
/// makes the list inert in edit mode ([S-1751]), so it checks nothing itself. Every row's menu is built from its actions
/// by <see cref="KitMenu"/>, fresh on each right-tap; the keys are <see cref="KitKeys"/>'. Everything a row shows was
/// written by somebody else and is set as text, never as markup. A new reading of the same rows rewrites them in place
/// where the look can (<see cref="IKitLook.Update"/>), so nothing under the pointer is rebuilt.
/// </para>
/// </summary>
public sealed partial class KitList : Grid, IStatusRoom
{
    private const string Scope = "kit";

    private readonly IWidgetHost _host;
    private readonly KitSpec _spec;
    private readonly IKitLook _look;
    private readonly Func<KitItem, IReadOnlyList<KitAction>> _actions;
    private readonly Action<KitItem, KitAction> _run;

    private readonly Grid _rows = new();
    private readonly ScrollViewer _scroll;
    private readonly List<IDisposable> _rowHooks = [];
    private readonly List<Border> _rowElements = [];

    /// <summary>[S-7034] The column titles over the rows (a table look's).</summary>
    private readonly Grid _columnHeader = new();

    /// <summary>Each drawn time, so the ages are rewritten without rebuilding the rows.</summary>
    private readonly List<(TextBlock Text, KitWhen? When, KitTime Time)> _times = [];

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

    /// <summary>The plan the rows standing were laid for (null: none stand), for a new reading rewritten in place.</summary>
    private KitPlan? _drawn;

    /// <summary>The keyboard cursor, by item id, and where it was (for an item that left the list).</summary>
    private string? _cursorId;
    private int _cursorIndex = -1;
    private bool _detached;

    /// <param name="actions">An item's actions, as its spec gives them (read fresh for every menu).</param>
    /// <param name="run">Carries out an action the user chose (<see cref="KitBinding"/>'s runner).</param>
    /// <param name="look">How the rows are drawn; null: the spec's own look.</param>
    public KitList(IWidgetHost host, KitSpec spec, Func<KitItem, IReadOnlyList<KitAction>> actions, Action<KitItem, KitAction> run, IKitLook? look = null)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(run);

        _host = host;
        _spec = spec;
        _look = look ?? KitLooks.For(spec);
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

        // [S-6712] [S-12807] The rows reach the view's top and the foot's inset: the first row's ink on the
        // card's inset (its band and leading over it, KitPlan.RowOver), the last row's on the foot's (KitPlanner.Reach).
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _columnHeader.Visibility = Visibility.Collapsed;
        SetRow(_columnHeader, 0);
        SetRow(_scroll, 1);
        SetRow(_page, 1);
        Children.Add(_columnHeader);

        if (Checked)
        {
            // [S-7037] The check layout's host gives the scroll only its whole lines and fades them; the
            // add row sits under it ([S-3850]).
            BuildCheckParts();
            Children.Add(_checkHost!);
            Children.Add(_addRow!.Element);
            return;
        }

        // [S-7035] A paged list draws its page in the slot; it never scrolls.
        Children.Add(Paged ? _page : _scroll);
    }

    /// <summary>The items shown, in order.</summary>
    public IReadOnlyList<KitItem> Items => _items;

    /// <summary>[S-1564] An empty list takes no keyboard.</summary>
    public bool WantsKeys => _items.Count > 0;

    /// <summary>The focused item's place, or negative for none.</summary>
    public int CursorIndex => _cursorIndex;

    /// <summary>[S-12815] A line of the widget's own stands over the list (a day's line): the rows start under it, not on the card's top edge.</summary>
    public bool UnderLine { get; init; }

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

            // [S-12834] A new size still lays the rows at its pitch (whole cells): under an editor or a drag nothing is
            // rebuilt, the rows standing take the new height.
            if (area.IsMeasured && (plan.RowHeight != _plan.RowHeight || plan.HeadingHeight != _plan.HeadingHeight || plan.Reach != _plan.Reach))
            {
                _plan = _plan with { RowHeight = plan.RowHeight, HeadingHeight = plan.HeadingHeight, Reach = plan.Reach };
                _area = area;
                Repitch();
            }

            return;
        }

        _held = null;
        var was = _items;
        _items = items;
        _plan = plan;
        _area = area;

        // [S-12815] The rows stand on the card's top edge unless the table's column titles or a line of the widget's own
        // stand over them.
        FrameworkElement part = Checked ? _checkHost! : Paged ? _page : _scroll;
        if (plan.ColumnHeader || UnderLine)
        {
            part.Margin = default;
        }
        else
        {
            ListEdge.Apply(part);
        }

        if (_cursorId is not null)
        {
            var at = IndexOf(_cursorId);
            _cursorIndex = at >= 0 ? at : ListKeys.AfterRemoval(_cursorIndex, items.Count);
            _cursorId = _cursorIndex >= 0 ? items[_cursorIndex].Id : null;
        }

        if (!Checked && !Paged && Rewrite(was, now))
        {
            return;
        }

        BuildRows(now);
    }

    /// <summary>Take every row down (an empty or problem screen is up).</summary>
    public void Clear() => Clear(_plan);

    /// <summary>Take every row down for another screen; a check layout's add row follows <paramref name="plan"/> (none without a list).</summary>
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

    /// <summary>[S-7061] Rewrite every drawn time for <paramref name="now"/> (the ages on the ticker).</summary>
    public void Retime(DateTimeOffset now)
    {
        foreach (var (text, when, time) in _times)
        {
            text.Text = KitText.When(Strings.Current, when, time, now);
        }
    }

    // ---- rows --------------------------------------------------------------------------------

    /// <summary>A draw of the plan at <paramref name="now"/>: the look's context, wired to this list's shared parts.</summary>
    private KitDraw NewDraw(DateTimeOffset now)
        => new(_spec, _plan, now, _area.Grid(XamlRoot?.RasterizationScale ?? 1), (text, when, time) => _times.Add((text, when, time)), SiteIcon, HookCheck);

    private void BuildRows(DateTimeOffset now)
    {
        ClearRows();
        var draw = NewDraw(now);

        if (Checked)
        {
            BuildChecks(draw);
        }
        else if (Paged)
        {
            BuildPage(draw);
        }
        else
        {
            BuildScroll(draw);
        }

        if (_cursorIndex >= 0 && _cursorIndex < _rowElements.Count)
        {
            Mark(_cursorIndex, true);
        }

        _drawn = _plan;
        ApplyRoom();
    }

    /// <summary>Whether the rows stand under their groups' headings: a one-column list whose items name them.</summary>
    private bool Sectioned => Math.Max(1, _plan.Columns) == 1 && _items.Any(i => i.Group?.Heading is not null);

    /// <summary>The bottom of each line of a sectioned list and whether it is a heading, for the viewport's whole-line end.</summary>
    private readonly List<(double Bottom, bool Heading)> _lines = [];

    /// <summary>
    /// [S-7060] The scroll of rows: in the plan's columns read left to right, or one column with each group's heading over
    /// its first row ([S-4402]); each line its one fixed size ([S-14810]).
    /// </summary>
    private void BuildScroll(KitDraw draw)
    {
        var columns = Math.Max(1, _plan.Columns);

        for (var c = 0; c < columns; c++)
        {
            _rows.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        _columnHeader.Visibility = _look.Titles(_columnHeader, draw) ? Visibility.Visible : Visibility.Collapsed;

        var sectioned = Sectioned;
        var line = 0;
        for (var i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            if (sectioned && item.Group is { Heading: not null } group && (i == 0 || _items[i - 1].Group?.Key != group.Key))
            {
                draw.Line = line;
                var heading = _look.Heading(group, draw);
                heading.Height = _plan.RowAt(line);
                DecorateHeading?.Invoke(group, heading);
                AddLine(heading, line++, 0, columns);
            }

            draw.Index = i;
            draw.Line = sectioned ? line : i / columns;
            var row = Item(_look.Draw(item, draw), item, i);
            row.Height = _plan.RowAt(draw.Line);
            _rowElements.Add(row);

            // [S-14266] The last item of a line the list does not fill takes the rest of it: no line ends short of the edge.
            if (sectioned)
            {
                AddLine(row, line++, 0, 1);
            }
            else
            {
                AddLine(row, i / columns, i % columns, KitPlanner.ColumnSpan(i, _items.Count, columns));
            }
        }

        FitRows(sectioned ? line : (_items.Count + columns - 1) / columns);
        _fade?.Refresh();
    }

    private void AddLine(FrameworkElement element, int line, int column, int span)
    {
        while (_lines.Count <= line)
        {
            _rows.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _lines.Add((_plan.LineStart(_lines.Count + 1), false));
        }

        if (element is not Border)
        {
            _lines[line] = (_lines[line].Bottom, true);
        }

        SetRow(element, line);
        SetColumn(element, column);
        SetColumnSpan(element, span);
        _rows.Children.Add(element);
    }

    /// <summary>
    /// [S-7060] A new reading of a list laid for the same plan and holding as many rows: each row rewritten in place where
    /// the look can, drawn again where it cannot, the item around it kept (its pointer, its menu, its place on the links).
    /// </summary>
    private bool Rewrite(IReadOnlyList<KitItem> was, DateTimeOffset now)
    {
        if (_drawn is not { } drawn || drawn != _plan || was.Count != _items.Count || _rowElements.Count != _items.Count || Sectioned || _items.Any(i => i.Group?.Heading is not null) != was.Any(i => i.Group?.Heading is not null))
        {
            return false;
        }

        Redrawing?.Invoke();
        _times.Clear();
        var draw = NewDraw(now);
        var columns = Math.Max(1, _plan.Columns);
        for (var i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            var row = _rowElements[i];
            draw.Index = i;
            draw.Line = i / columns;
            if (row.Child is not RowBlock block || !_look.Update(block, was[i], item, draw))
            {
                (row.Child as RowBlock)?.Detach();
                row.Child = _look.Draw(item, draw);
            }

            ToolTipService.SetToolTip(row, item.Tooltip ?? item.Link?.OriginalString);
            Decorate?.Invoke(item, row);
        }

        for (var i = 0; i < _rowElements.Count; i++)
        {
            Mark(i, i == _cursorIndex);
        }

        _fade?.Refresh();
        ApplyRoom();
        return true;
    }

    private double _statusRoom;
    private FrameworkElement? _roomed;

    /// <summary>
    /// [S-12813] [S-12834] The status glyph over the card's top end corner (a stale list's warning, the busy ring) stands
    /// beside the top end row's text, never over it: that row (the first of the last column, or the table's column
    /// titles over the rows) ends that much sooner, as a facts list's first row does.
    /// </summary>
    public void StatusRoom(double width)
    {
        _statusRoom = double.IsFinite(width) ? Math.Max(0, width) : 0;
        ApplyRoom();
    }

    private void ApplyRoom()
    {
        var top = TopEndRow();
        if (_roomed is not null && !ReferenceEquals(_roomed, top))
        {
            EndRoom(_roomed, 0);
        }

        _roomed = top;
        if (top is not null)
        {
            EndRoom(top, _statusRoom);
        }
    }

    /// <summary>The row (or heading, or column titles) standing at the list's top end corner, under the status.</summary>
    private FrameworkElement? TopEndRow()
    {
        if (_plan.ColumnHeader && _columnHeader.Visibility == Visibility.Visible)
        {
            return _columnHeader;
        }

        if (Checked)
        {
            if (_checks is not { } panel || panel.Children.Count == 0)
            {
                return null;
            }

            var order = panel.InSlotOrder().ToList();
            var at = KitChecks.LinesFor(order.Count, panel.Columns) * (panel.Columns - 1);
            return at < order.Count ? order[at] as FrameworkElement : null;
        }

        if (Paged)
        {
            return _page.Children.Count > 0 && _page.Children[^1] is Panel { Children.Count: > 0 } column ? column.Children[0] as FrameworkElement : null;
        }

        // A heading over the first row stands in the corner.
        if (_rows.Children.Count > 0 && _rows.Children[0] is FrameworkElement { } first && first is not Border)
        {
            return first;
        }

        // [S-14266] A first line the list does not fill: its last item spans to the end corner.
        var end = Math.Min(Math.Max(1, _plan.Columns), _rowElements.Count) - 1;
        return end >= 0 ? _rowElements[end] : null;
    }

    /// <summary>A row's (a heading's) end inset: the side inset and <paramref name="room"/> more.</summary>
    private static void EndRoom(FrameworkElement row, double room)
    {
        var part = row is Border { Child: FrameworkElement child } ? child : row;
        switch (part)
        {
            case Control control:
                control.Padding = control.Padding with { Right = FamilySpace.Inset + room };
                break;
            case Grid grid:
                grid.Padding = grid.Padding with { Right = FamilySpace.Inset + room };
                break;
            case Border border:
                border.Padding = border.Padding with { Right = FamilySpace.Inset + room };
                break;
        }
    }

    /// <summary>
    /// [S-639] [S-6712] [S-14810] The list is given its lines' height (each of the plan's one fixed size), at most its room:
    /// a longer list's last line in sight is cut by the card's foot, never ending in a heading, the rest a scroll away.
    /// </summary>
    private void FitRows(int lines)
    {
        if (!_area.IsMeasured || _rowElements.Count == 0)
        {
            _scroll.Height = double.NaN;
            return;
        }

        var content = _plan.LineStart(lines);
        var room = KitPlanner.ListRoom(_plan, KitPlanner.ListHeight(_area), _area.Grid());
        _scroll.Height = content <= room + LayoutMath.Tolerance || !_lines.Any(l => l.Heading)
            ? Math.Min(content, room)
            : KitChecks.ViewEnd([.. _lines.Take(lines).Select(l => l.Bottom)], room, [.. _lines.Take(lines).Select(l => l.Heading)]);
    }

    /// <summary>A full-width item around a row: the item layer's fill and hover, its tag, menu, tooltip and pointer.</summary>
    private Border Item(FrameworkElement content, KitItem item, int index)
    {
        var row = new Border { Child = content, Tag = index };
        ItemMark.Mark(row);
        ItemFill.Paint(row, ItemRole.Row);
        WidgetItems.Mark(row);
        _rowHooks.Add(Fluent.Hover(row));
        _rowHooks.Add(ItemMenu.Attach(row, () => index < _items.Count ? Entries(_items[index]) : []));

        if ((item.Tooltip ?? item.Link?.OriginalString) is { } tip)
        {
            ToolTipService.SetToolTip(row, tip);
        }

        row.Tapped += Checked ? OnCheckRowTapped : OnRowTapped;
        row.DoubleTapped += OnRowDoubleTapped;
        if (Checked)
        {
            HookDrag(row);
        }

        Decorate?.Invoke(item, row);
        return row;
    }

    /// <summary>[S-1288] The keyboard's ring on a row (the Row block's own, over its whole width).</summary>
    private void Mark(int index, bool focused)
    {
        if (index >= 0 && index < _rowElements.Count && _rowElements[index].Child is RowBlock block)
        {
            block.Focused = focused;
        }
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
        Redrawing?.Invoke();
        foreach (var hook in _rowHooks)
        {
            hook.Dispose();
        }

        _rowHooks.Clear();
        _times.Clear();

        foreach (var row in _rowElements)
        {
            row.Tapped -= OnRowTapped;
            row.Tapped -= OnCheckRowTapped;
            row.DoubleTapped -= OnRowDoubleTapped;
            (row.Child as RowBlock)?.Detach();
        }

        _rowElements.Clear();
        _lines.Clear();
        _drawn = null;
        _roomed = null;
        _checksByRow.Clear();
        _checks?.Children.Clear();
        _page.Children.Clear();
        _rows.Children.Clear();
        _rows.ColumnDefinitions.Clear();
        _rows.RowDefinitions.Clear();
    }

    // ---- pointer, menu, keys -----------------------------------------------------------------

    /// <summary>
    /// A click focuses the row, and is the row's: it never reaches the card ([S-2004]); a list whose rows do one light
    /// thing runs it too ([S-7065], <see cref="KitSpec.ClickRuns"/>).
    /// </summary>
    private void OnRowTapped(object sender, TappedRoutedEventArgs args)
    {
        args.Handled = true;

        if (sender is Border { Tag: int index })
        {
            Focus(index);
            if (_spec.ClickRuns)
            {
                RunKind(index, null);
            }
        }
    }

    /// <summary>A double-click runs the row's primary action (a list that runs it on the click has run it already).</summary>
    private void OnRowDoubleTapped(object sender, DoubleTappedRoutedEventArgs args)
    {
        if (sender is Border { Tag: int index } && index < _items.Count && KitMenu.Primary(_actions(_items[index])) is { } primary)
        {
            args.Handled = true;
            if (!_spec.ClickRuns)
            {
                Run(_items[index], primary);
            }
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
                if (had != decision.Index)
                {
                    _rowElements[decision.Index].StartBringIntoView();
                }

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
        if (index < 0 || index >= _items.Count)
        {
            return false;
        }

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
        CursorMoved?.Invoke(_items[index]);
    }

    /// <summary>Drop the cursor.</summary>
    public void Release()
    {
        if (_cursorIndex >= 0 && _cursorIndex < _rowElements.Count)
        {
            Mark(_cursorIndex, false);
        }

        var had = _cursorIndex >= 0;
        _cursorIndex = -1;
        _cursorId = null;
        if (had)
        {
            CursorMoved?.Invoke(null);
        }
    }

    /// <summary>[S-3307] The cursor moved onto an item (null: it let go): a widget publishing its item on its view's links hears it.</summary>
    public Action<KitItem?>? CursorMoved { get; init; }

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

    /// <summary>A hook undone on dispose (a handler pair a row's part added).</summary>
    private sealed class Unhook(Action undo) : IDisposable
    {
        private Action? _undo = undo;

        public void Dispose()
        {
            _undo?.Invoke();
            _undo = null;
        }
    }
}
