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
/// [S-7060] The list part of every kit widget: rows for the plan the ladder chose
/// (<see cref="KitLadder.Plan"/>), full-bleed with their own inset ([S-190]), no separators, in
/// columns read left to right, shown whole under a scroll with the shared fade ([S-639], [S-1281]).
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

    /// <summary>The globe that stands in for a site's icon: the caption's size, so it sits on the caption's line.</summary>
    private const double GlobeSize = 12;

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
            Padding = new Thickness(0, KitLadder.ListInset / 2, 0, KitLadder.ListInset / 2),
            HorizontalScrollMode = ScrollMode.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalAlignment = VerticalAlignment.Top,
        };

        // [S-7032] The lined looks fill the slot and scroll under the fade, the table's column
        // titles over them; the rich look is given the height of its whole rows (FitWholeRows).
        if (spec.Look != KitRowLook.Rich)
        {
            _scroll.Padding = Fluent.ListPadding;
            _scroll.VerticalAlignment = VerticalAlignment.Stretch;
        }

        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _columnHeader.Visibility = Visibility.Collapsed;
        SetRow(_columnHeader, 0);
        SetRow(_scroll, 1);
        SetRow(_page, 1);
        Children.Add(_columnHeader);

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
        if (!Paged)
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
    public void Clear()
    {
        ClearRows();
        _items = [];
    }

    public void Detach()
    {
        if (_detached)
        {
            return;
        }

        _detached = true;
        ClearRows();
        _fade?.Dispose();
        _fade = null;
    }

    // ---- rows --------------------------------------------------------------------------------

    private void BuildRows(DateTimeOffset now)
    {
        ClearRows();

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
            var row = _spec.Look == KitRowLook.Rich ? BuildRow(_items[i], i, now) : BuildLinedRow(_items[i], i, now);
            SetRow(row, i / columns);
            SetColumn(row, i % columns);
            _rows.Children.Add(row);
            _rowElements.Add(row);
        }

        if (_cursorIndex >= 0 && _cursorIndex < _rowElements.Count)
        {
            Mark(_cursorIndex, true);
        }

        if (_spec.Look == KitRowLook.Rich)
        {
            FitWholeRows(columns);
        }

        _fade?.Refresh();
    }

    /// <summary>
    /// [S-639] The list is given the height of the rows that fit whole; the rest is a scroll away.
    /// Rows are as tall as their titles, so they are measured at their column's width.
    /// </summary>
    private void FitWholeRows(int columns)
    {
        if (!_area.IsMeasured || _rowElements.Count == 0)
        {
            _scroll.Height = double.NaN;
            return;
        }

        var columnWidth = Math.Max(1, _area.Width / columns);
        var heights = new List<double>();

        for (var i = 0; i < _rowElements.Count; i++)
        {
            _rowElements[i].Measure(new Size(columnWidth, double.PositiveInfinity));
            var line = i / columns;

            if (line == heights.Count)
            {
                heights.Add(0);
            }

            heights[line] = Math.Max(heights[line], _rowElements[i].DesiredSize.Height);
        }

        var available = Math.Max(0, _area.Height - KitLadder.ListInset);
        var shown = KitLadder.WholeRows(heights, available);
        _scroll.Height = KitLadder.ListInset + heights.Take(shown).Sum();
    }

    /// <summary>
    /// [S-7031] One rich row: the subtitle (with the lead) on its own line, the title wrapping to the
    /// plan's lines, the preview where the ladder affords it, and the time (with the lead at its start
    /// when the subtitle has no line). As tall as its title needs, not a fixed list row.
    /// </summary>
    private Border BuildRow(KitItem item, int index, DateTimeOffset now)
    {
        var stack = new StackPanel { Spacing = KitLadder.LineGap };

        if (_plan.Subtitle && item.Subtitle is { Length: > 0 } subtitle)
        {
            var source = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Fluent.Space2 };
            if (_plan.Lead && Lead(item) is { } lead)
            {
                source.Children.Add(lead);
            }

            var name = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
            name.Text = subtitle;
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            source.Children.Add(name);
            stack.Children.Add(source);
        }

        var title = Fluent.Text(item.Emphasis ? FluentType.BodyStrong : FluentType.Body);
        title.Text = item.Title.Length > 0 ? item.Title : _spec.Untitled();
        title.TextWrapping = TextWrapping.Wrap;
        title.TextTrimming = TextTrimming.CharacterEllipsis;
        title.MaxLines = _plan.TitleLines;
        stack.Children.Add(title);

        if (_plan.PreviewLines > 0 && item.Preview is { Length: > 0 } text)
        {
            var preview = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
            preview.Text = text;
            preview.TextWrapping = TextWrapping.Wrap;
            preview.TextTrimming = TextTrimming.CharacterEllipsis;
            preview.MaxLines = _plan.PreviewLines;
            stack.Children.Add(preview);
        }

        var leadInline = _plan.Lead && !_plan.Subtitle ? Lead(item) : null;
        var when = _plan.When ? KitText.When(Strings.Current, item.When, _spec.Time, now) : string.Empty;

        if (leadInline is not null || when.Length > 0)
        {
            var meta = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Fluent.Space2 };

            if (leadInline is not null)
            {
                meta.Children.Add(leadInline);
            }

            if (when.Length > 0)
            {
                var time = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
                time.Text = when;
                meta.Children.Add(time);
            }

            stack.Children.Add(meta);
        }

        var row = new Border
        {
            Child = stack,
            Padding = new Thickness(Fluent.Space4, KitLadder.RowPadding, Fluent.Space4, KitLadder.RowPadding),
            Tag = index,
        };

        ItemFill.Paint(row, ItemRole.Row);
        RowFocus.Prepare(row);
        _rowHooks.Add(Fluent.Hover(row));
        _rowHooks.Add(ItemMenu.Attach(row, () => Entries(item)));

        if ((item.Tooltip ?? item.Link?.OriginalString) is { } tip)
        {
            ToolTipService.SetToolTip(row, tip);
        }

        row.Tapped += OnRowTapped;
        row.DoubleTapped += OnRowDoubleTapped;

        return row;
    }

    /// <summary>The row's lead, or null when it has none to draw.</summary>
    private FrameworkElement? Lead(KitItem item) => item.Lead.Kind switch
    {
        KitLeadKind.SiteIcon when item.Lead.Value is { Length: > 0 } link => SiteIcon(link),
        KitLeadKind.Icon when Enum.TryParse<KitIcon>(item.Lead.Value, out var icon) => Fluent.Icon(KitIcons.Glyph(icon)),
        _ => null,
    };

    /// <summary>[S-1304] The site's own icon (the host's one cache), with a globe in its place until it arrives or when the site has none.</summary>
    private Grid SiteIcon(string link)
    {
        var globe = Fluent.Icon(Glyphs.Globe, "TextFillColorTertiaryBrush", GlobeSize);
        globe.HorizontalAlignment = HorizontalAlignment.Center;

        var image = new Image { Width = Fluent.IconSize, Height = Fluent.IconSize, Visibility = Visibility.Collapsed };
        var holder = new Grid { Width = Fluent.IconSize, Height = Fluent.IconSize, VerticalAlignment = VerticalAlignment.Center };
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
            var bitmap = new BitmapImage { DecodePixelWidth = (int)(Fluent.IconSize * 2) };
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
        _reveals.Clear();

        foreach (var row in _rowElements)
        {
            row.Tapped -= OnRowTapped;
            row.DoubleTapped -= OnRowDoubleTapped;
        }

        _rowElements.Clear();
        _page.Children.Clear();
        _rows.Children.Clear();
        _rows.ColumnDefinitions.Clear();
        _rows.RowDefinitions.Clear();
    }

    // ---- pointer, menu, keys -----------------------------------------------------------------

    /// <summary>A click focuses the row.</summary>
    private void OnRowTapped(object sender, TappedRoutedEventArgs args)
    {
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
        if (_detached || _items.Count == 0)
        {
            return false;
        }

        var had = _cursorIndex;
        var decision = KitKeys.Decide(key, _cursorIndex, _items.Count);

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
                return RunKind(decision.Index, KitActionKind.Toggle);

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
