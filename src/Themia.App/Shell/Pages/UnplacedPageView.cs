using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Desktop;
using Themia.Core.Diagnostics;
using Themia.Core.Geometry;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Shell;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Windows.System;
using P = Themia.Sdk.Kit.ShellMetrics.Page;

namespace Themia.App.Shell;

/// <summary>[S-13700] What the Unplaced icons page shows now: the view's unplaced items.</summary>
/// <param name="Holds">Whether the view shown on the page's display shows desktop icons at all (<see cref="DesktopViews.Holds"/>).</param>
internal sealed record UnplacedModel(bool Holds, IReadOnlyList<DesktopItem> Items)
{
    /// <summary>
    /// [S-13707] The box the desktop gives an icon on the page's display, in DIPs (<see cref="UnplacedItems.Dips"/> of the
    /// desktop's own box): the cell each icon is drawn in, as on the desktop. None for none.
    /// </summary>
    public SizeDip Box { get; init; }
}

/// <summary>What the Unplaced icons page does, given by its display (or a render's stand-in).</summary>
/// <param name="Read">[S-13700] The page's model as the desktop and the view are now.</param>
/// <param name="Place">[S-13703] Place these items on the view at its next free cells (a double-click, Enter).</param>
/// <param name="Drag">[S-13705] These items were dragged off the page: their placement follows the pointer.</param>
/// <param name="Icon">The shell's icon of an item at so many physical pixels.</param>
internal sealed record UnplacedCommands(
    Func<UnplacedModel> Read,
    Action<IReadOnlyList<string>> Place,
    Action<IReadOnlyList<string>> Drag,
    Func<DesktopItem, int, Task<ImageSource?>> Icon);

/// <summary>
/// [S-13700]..[S-13707] The Unplaced icons page's live part (the page's one line, drawn as the Appearance's preview is,
/// <see cref="IPagePreview"/>): no words over it (owner, 2026-11-20: "remove the text from the on desktop not home page") and
/// no buttons (owner, 2026-11-21: "place on fence and put in this view buttons should be removed from the desktop icons
/// selector at the top", [S-13704] retired), a search box once the list is long (<see cref="UnplacedItems.Searches"/>), then
/// the icons on the page's own ground, each the desktop's own icon view (<c>.Icons.cs</c>) at the
/// desktop's icon size in the box the desktop gives it, with the corner marks the desktop's icons show in edit mode
/// (<see cref="IconMarks"/>), box against box in rows across the page as the desktop lays them
/// (<see cref="UnplacedItems.Cells"/>): what is seen is what is placed (owner, 2026-11-19: "it should show the icons
/// exactly how they would look like on the desktop"). They are selected as files are (<see cref="ItemSelection"/>: click,
/// Ctrl/Shift+click, the arrows across and down the rows, Ctrl+A), painted in the desktop icon's selection look, dragged
/// off onto the desktop to place them where they are let go, and placed by a double-click or Enter. Drawn again from
/// <see cref="UnplacedCommands.Read"/> each time the page is (<see cref="Refresh"/>): a file added to or gone from the
/// desktop, an icon placed or removed, another icon size.
/// </summary>
internal sealed partial class UnplacedPageView : StackPanel, IPagePreview
{
    private const string Scope = "unplaced-page";

    private readonly UnplacedCommands _commands;
    private readonly ItemSelection _selection = new(DesktopItemKeys.Comparer);
    private readonly TextBox _search;
    private readonly IconGrid _icons = new();

    /// <summary>[S-13707] The icons' corner marks, as the desktop's icons show them in edit mode, over the icons.</summary>
    private readonly IconMarks _marks = new();

    private readonly Border _desk;
    private readonly TextBlock _note = Fluent.Text(FamilyType.Body, "TextFillColorSecondaryBrush");
    private readonly Border _noteCard;
    private readonly Dictionary<string, Cell> _cells = new(DesktopItemKeys.Comparer);
    private readonly List<Cell> _shown = [];
    private readonly Ticker _ticker;
    private readonly IconDesk _iconDesk;
    private readonly IconPreviewHost _iconHost;

    /// <summary>[S-13707] The desktop icon's kind (the one whose config is an <see cref="IDesktopItemConfig"/>), or null in a build without it.</summary>
    private readonly WidgetDescriptor? _kind = ModuleCatalog.Widgets.FirstOrDefault(d => typeof(IDesktopItemConfig).IsAssignableFrom(d.ConfigType));
    private UnplacedModel _model = new(true, []);
    private bool _disposed;

    /// <summary>The item a press is on and where it began.</summary>
    private (string Key, Windows.Foundation.Point At)? _press;

    /// <summary>One item's icon: the desktop's own icon view in its box.</summary>
    private sealed record Cell(string Key, Grid Root, FrameworkElement View);

    public UnplacedPageView(UnplacedCommands commands)
    {
        _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        var dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread() ?? throw new InvalidOperationException("the page is built on the UI thread");
        _ticker = new Ticker(dispatcher);
        _iconDesk = new IconDesk(this, Services.Files.HostDesktopItems.Instance);
        _iconHost = new IconPreviewHost(new CardWidgetHost(new PreviewCardHost(_ticker)), _iconDesk);
        Spacing = P.CardGap;

        _search = new TextBox
        {
            PlaceholderText = Str.Unplaced_Page_Search.Text(),
            IsSpellCheckEnabled = false,
            IsTextPredictionEnabled = false,
            Margin = new Thickness(0, 0, 0, P.HeadingGap),
            Visibility = Visibility.Collapsed,
        };
        AutomationProperties.SetName(_search, Str.Unplaced_Page_Search.Text());
        _search.TextChanged += (_, _) => Draw();
        Children.Add(_search);

        // [S-13707] The icons on the page's own ground (owner, 2026-11-20: "make the background of the icons area the same as
        // the rest of the configuration page"), their corner marks over them; room round them for the marks' outer half.
        _icons.Arranged = DrawMarks;
        _desk = new Border
        {
            Child = new Grid { Children = { _icons, _marks.Halos, _marks.Inks } },
            Padding = new Thickness(Fluent.Space2),
            IsTabStop = true,
            UseSystemFocusVisuals = true,
        };
        _desk.KeyDown += OnKey;
        _desk.GotFocus += (_, _) => Paint();
        _desk.LostFocus += (_, _) => Paint();
        AutomationProperties.SetName(_desk, Str.Unplaced_Page_Name.Text());
        Children.Add(_desk);

        _note.TextWrapping = TextWrapping.Wrap;
        _noteCard = Card(new Border { Child = _note, Padding = new Thickness(P.IconAt, P.EndInset, P.EndInset, P.EndInset) });
        Children.Add(_noteCard);

        if (_kind is null)
        {
            Log.Error(Scope, "this build has no desktop icon kind; the page draws no icon");
        }

        Refresh();
    }

    /// <summary>[S-13706] The page is drawn again: the desktop and the view as they are now; the selection keeps what is still listed.</summary>
    public void Refresh()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _model = _commands.Read();
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "the unplaced icons could not be read; the page shows none", ex);
            _model = new(true, []);
        }

        _search.Visibility = UnplacedItems.Searches(_model.Items.Count) ? Visibility.Visible : Visibility.Collapsed;
        Draw();
    }

    /// <summary>A render's hand: these items selected as Ctrl+clicks select them.</summary>
    internal void DebugSelect(IReadOnlyList<string> keys)
    {
        foreach (var key in keys)
        {
            _selection.Click(_selection.IndexOf(key), ctrl: true, shift: false);
        }

        Paint();
    }

    /// <summary>A render's hand: words typed into the search box.</summary>
    internal void DebugSearch(string text)
    {
        _search.Text = text;
        Draw();
    }

    /// <summary>The page goes: its icons let go of the desktop, and the ticker stops.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var cell in _cells.Values)
        {
            DropCell(cell);
        }

        _cells.Clear();
        _shown.Clear();
        _icons.Children.Clear();
        _ticker.Dispose();
    }

    /// <summary>[S-6626] The selection is drawn active while the page has the keyboard or holds a selection.</summary>
    private bool Active => _desk.FocusState != FocusState.Unfocused || _selection.Count > 0;

    /// <summary>[S-13702] [S-13707] The icons of what the search matches, in their cells; the selection kept over them.</summary>
    private void Draw()
    {
        if (_disposed)
        {
            return;
        }

        var query = _search.Visibility == Visibility.Visible ? _search.Text : null;
        var items = UnplacedItems.Matching(_model.Items, query);
        _selection.SetOrder([.. items.Select(i => i.Key)]);
        SyncCells(items);

        _desk.Visibility = _shown.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        _noteCard.Visibility = items.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
        _note.Text = !_model.Holds ? Str.Unplaced_Page_NoneHere.Text()
            : _model.Items.Count == 0 ? Str.Unplaced_Page_None.Text()
            : Str.AddMenu_NoResults.Format(("query", (query ?? string.Empty).Trim()));
        Paint();
    }

    /// <summary>
    /// [S-13707] One icon per item shown, in the desktop's box at the icon size in force: kept for one still there, made for a
    /// new one, taken down for one gone; laid in the order shown.
    /// </summary>
    private void SyncCells(IReadOnlyList<DesktopItem> items)
    {
        var keep = new HashSet<string>(items.Select(i => i.Key), DesktopItemKeys.Comparer);
        foreach (var gone in _cells.Keys.Where(k => !keep.Contains(k)).ToList())
        {
            DropCell(_cells[gone]);
            _cells.Remove(gone);
        }

        var box = _model.Box;
        _icons.Box = box;
        _shown.Clear();
        foreach (var item in items)
        {
            if (!_cells.TryGetValue(item.Key, out var cell))
            {
                if (MakeCell(item) is not { } made)
                {
                    continue;
                }

                cell = made;
                _cells[item.Key] = cell;
            }

            cell.Root.Width = box.Width;
            cell.Root.Height = box.Height;
            cell.View.Width = box.Width;
            cell.View.Height = box.Height;
            AutomationProperties.SetName(cell.Root, item.Name);
            _shown.Add(cell);
        }

        // The children follow the order shown; the ones that stay are not taken off and put back unless it changed.
        if (!_icons.Children.SequenceEqual(_shown.Select(c => (UIElement)c.Root)))
        {
            _icons.Children.Clear();
            foreach (var cell in _shown)
            {
                _icons.Children.Add(cell.Root);
            }
        }

        _icons.InvalidateMeasure();
    }

    /// <summary>[S-13707] The desktop's icon view for <paramref name="item"/>, attached as the desktop attaches it, and the press it takes.</summary>
    private Cell? MakeCell(DesktopItem item)
    {
        if (_kind is null)
        {
            return null;
        }

        FrameworkElement view;
        try
        {
            view = ModuleHost.CreateView(_kind.Id, _iconHost);
            if (view is IWidgetView widget)
            {
                widget.Attach(_ticker, AppHost.NewItemWidget(_kind.Id, item, default).Config);
            }
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "a desktop icon could not be drawn on the unplaced icons page; the page shows the rest", ex);
            return null;
        }

        var root = new Grid { Background = Fluent.Clear(), Tag = item.Key };
        root.Children.Add(view);
        var key = item.Key;

        // The view marks its double-clicks and right-clicks handled (they are the desktop's there): the page hears them anyway.
        root.AddHandler(PointerPressedEvent, new PointerEventHandler((_, e) => OnPressed(key, root, e)), handledEventsToo: true);
        root.PointerMoved += (_, e) => OnMoved(root, e);
        root.PointerReleased += (_, e) => OnReleased(key, root, e);
        root.PointerCaptureLost += (_, _) => _press = null;
        root.AddHandler(DoubleTappedEvent, new DoubleTappedEventHandler((_, e) =>
        {
            e.Handled = true;
            if (_model.Holds)
            {
                Do(() => _commands.Place([key]));
            }
        }), handledEventsToo: true);
        return new Cell(key, root, view);
    }

    private static void DropCell(Cell cell)
    {
        try
        {
            (cell.View as IWidgetView)?.Detach();
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, "a desktop icon on the unplaced icons page could not let go", ex);
        }
    }

    /// <summary>[S-6621] A left press selects as a file's does (Ctrl toggles, Shift the range); it may become a drag.</summary>
    private void OnPressed(string key, UIElement cell, PointerRoutedEventArgs e)
    {
        if (!e.GetCurrentPoint(cell).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var ctrl = (e.KeyModifiers & VirtualKeyModifiers.Control) != 0;
        var shift = (e.KeyModifiers & VirtualKeyModifiers.Shift) != 0;
        _selection.Press(_selection.IndexOf(key), ctrl, shift);
        _press = (key, e.GetCurrentPoint(this).Position);
        _ = cell.CapturePointer(e.Pointer);
        _ = _desk.Focus(FocusState.Pointer);
        Paint();
        e.Handled = true;
    }

    /// <summary>[S-13705] Past the drag's dead zone the press drags what it acts on off the page, onto the desktop.</summary>
    private void OnMoved(UIElement cell, PointerRoutedEventArgs e)
    {
        if (_press is not { } press || !e.GetCurrentPoint(cell).Properties.IsLeftButtonPressed || !_model.Holds)
        {
            return;
        }

        var now = e.GetCurrentPoint(this).Position;
        if (!CardGestures.Travelled(now.X - press.At.X, now.Y - press.At.Y))
        {
            return;
        }

        _press = null;
        _selection.CancelPress();
        cell.ReleasePointerCapture(e.Pointer);
        var keys = UnplacedItems.Acting(_selection.Selected, press.Key);
        e.Handled = true;

        // [S-2346] The placement closes the page: never inside the pointer's own event on it.
        if (!DispatcherQueue.TryEnqueue(() => Do(() => _commands.Drag(keys))))
        {
            Log.Warn(Scope, "the drag off the unplaced icons page could not be started");
        }
    }

    private void OnReleased(string key, UIElement cell, PointerRoutedEventArgs e)
    {
        if (_press is null)
        {
            return;
        }

        _press = null;
        cell.ReleasePointerCapture(e.Pointer);
        _selection.Release(_selection.IndexOf(key));
        Paint();
        e.Handled = true;
    }

    /// <summary>
    /// [S-6623] [S-13707] The icons' keys as the desktop's: the arrows across a row and down to the same column of the next,
    /// Page Up/Down, Home/End, Ctrl+A, Ctrl+Space; Enter places the selection.
    /// </summary>
    private void OnKey(object sender, KeyRoutedEventArgs e)
    {
        var shift = SelectionInput.ShiftHeld();
        var ctrl = (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control) & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;
        if (WidgetKeys.FromVirtualKey((int)e.Key, shift, ctrl, alt: false) is not { } key)
        {
            return;
        }

        if (key == WidgetKey.Enter)
        {
            if (_selection.Count > 0 && _model.Holds)
            {
                Do(() => _commands.Place(_selection.Selected));
            }

            e.Handled = true;
            return;
        }

        var box = _model.Box;
        var columns = UnplacedItems.Columns(_icons.ActualWidth, box.Width);
        var pageRows = Math.Max(1, (int)((XamlRoot?.Size.Height ?? box.Height) * ShellMetrics.Flyout.MostShare / Math.Max(1, box.Height)));
        if (_selection.Key(key, shift, columns, pageRows))
        {
            Paint();
            if (_selection.Focus is { } focus && _cells.TryGetValue(focus, out var cell))
            {
                cell.Root.StartBringIntoView();
            }

            e.Handled = true;
        }
    }

    /// <summary>[S-13707] Every icon paints the selection as the page has it, in the desktop icon's look, its marks too.</summary>
    private void Paint()
    {
        if (_disposed)
        {
            return;
        }

        _iconDesk.Paint();
        DrawMarks();
    }

    /// <summary>
    /// [S-13707] Each icon's corner marks round the box it is laid in, as the desktop draws an icon's in edit mode: neutral, a
    /// selected one's in the accent (<see cref="IconMarks"/>).
    /// </summary>
    private void DrawMarks()
    {
        if (_disposed)
        {
            return;
        }

        var laid = _icons.Laid;
        var boxes = new List<(PixelRect Box, bool Selected)>(Math.Min(laid.Count, _shown.Count));
        for (var i = 0; i < laid.Count && i < _shown.Count; i++)
        {
            boxes.Add((laid[i], _selection.IsSelected(_shown[i].Key)));
        }

        _marks.Draw(boxes, XamlRoot?.RasterizationScale ?? 1.0);
    }

    /// <summary>A page card: the cards' fill and rim, radius 4.</summary>
    private static Border Card(UIElement child) => new()
    {
        Child = child,
        Background = ShellBrush.Of(ShellRole.CardFill),
        BorderBrush = ShellBrush.Of(ShellRole.CardStroke),
        BorderThickness = new Thickness(FamilyShape.Hairline),
        CornerRadius = new CornerRadius(P.CardRadius),
        Padding = new Thickness(Fluent.Space1),
    };

    private static void Do(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "an action of the unplaced icons page failed", ex);
        }
    }

    /// <summary>
    /// [S-13707] The icons' cells as the desktop lays icons (<see cref="UnplacedItems.Cells"/>): each child in a desktop icon's
    /// box, box against box, as many to a row as the width holds, row after row; as tall as its rows. Measured at the width
    /// it is given, so the flyout's height follows the bar's width. Each arrange hands its cells on (<see cref="Arranged"/>).
    /// </summary>
    private sealed partial class IconGrid : Panel
    {
        public SizeDip Box { get; set; }

        /// <summary>The cells the children were last laid in, in their order.</summary>
        public IReadOnlyList<PixelRect> Laid { get; private set; } = [];

        /// <summary>Called after an arrange that laid the children in other cells, once <see cref="Laid"/> holds them.</summary>
        public Action? Arranged { get; set; }

        protected override Windows.Foundation.Size MeasureOverride(Windows.Foundation.Size availableSize)
        {
            var box = new Windows.Foundation.Size(Box.Width, Box.Height);
            foreach (var child in Children)
            {
                child.Measure(box);
            }

            var count = Children.Count;
            var width = double.IsFinite(availableSize.Width) ? availableSize.Width : count * Box.Width;
            var columns = UnplacedItems.Columns(width, Box.Width);
            return new Windows.Foundation.Size(Math.Min(count, columns) * Box.Width, UnplacedItems.Height(count, width, Box));
        }

        protected override Windows.Foundation.Size ArrangeOverride(Windows.Foundation.Size finalSize)
        {
            var cells = UnplacedItems.Cells(Children.Count, finalSize.Width, Box);
            for (var i = 0; i < Children.Count; i++)
            {
                var cell = cells[i];
                Children[i].Arrange(new Windows.Foundation.Rect(cell.X, cell.Y, cell.Width, cell.Height));
            }

            if (!Laid.SequenceEqual(cells))
            {
                Laid = cells;
                Arranged?.Invoke();
            }

            return finalSize;
        }
    }
}
