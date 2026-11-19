using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;

namespace Themia.Widget.Fence;

/// <summary>
/// [S-10900]..[S-10915] [S-13040] A fence as Fences draws one, in Themia's look: its own title bar (the name, how many items,
/// a click rolls it up) over its body, a region of the desktop icon grid. Its items are not drawn here: each is a desktop
/// icon the host lays on a cell of the body (the owner, 2026-11-19: "the fence should basically just be a helper to align
/// the icon on the underlying engine"), so it looks, selects, drags and drops exactly as a free icon does. The title bar is
/// whole rows of the widget grid and the body whole icon boxes (<see cref="IDesktopFences.Cells"/>, <see cref="FenceGrid"/>).
/// <para>
/// What the fence draws in its body is what is no desktop icon: the files a search fence finds ([S-13023]) and a picture's
/// items (the sample, a render's states), in the same cells by the same layout. What is the fence's own (rolling up, the
/// title, the body's band, drops of files into it) is asked of <see cref="IDesktopFences"/>. A press on the title bar is the
/// card's (it moves the fence, its icons with it); a press on its empty body starts the desktop's band.
/// </para>
/// <para>
/// Split by concern: this file owns the lifecycle, the title bar and the body; <c>.Items.cs</c> the cells it draws (a
/// search's files, a picture's items); <c>.Drop.cs</c> drops of files into the fence.
/// </para>
/// </summary>
internal sealed partial class FenceView : Grid, IWidgetView, IWidgetKeys, IWidgetSurfaceAware, IWidgetContextActions, IWidgetTopEdge
{
    private const string Scope = "fence";

    private readonly IWidgetHost _host;

    private readonly Grid _titleBar = new() { Padding = new Thickness(FamilySpace.S3, 0, FamilySpace.S3, 0) };
    private readonly TextBlock _title = Family.Text(FamilyType.BodyStrong);
    private readonly TextBlock _count = Family.Text(FamilyType.Caption, ColourRole.TextSecondary);
    private readonly FontIcon _chevron = Family.Glyph(Glyphs.ChevronDown, FamilyShape.Icon, ColourRole.TextSecondary);
    private readonly Border _divider = new() { Height = FamilyShape.Hairline, VerticalAlignment = VerticalAlignment.Bottom };

    /// <summary>The body: the cells drawn here (a search's files, a picture's items) on a canvas clipped to it.</summary>
    private readonly Grid _body = new() { Background = Fluent.Clear() };
    private readonly Canvas _canvas = new() { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = true };
    private readonly TextBlock _empty = Family.Text(FamilyType.Caption, ColourRole.TextTertiary);

    /// <summary>[S-13043] Where the body stands in its rows when it has more than it shows: a thin thumb at its right edge.</summary>
    private readonly Border _thumb = new() { Width = FamilySpace.S1, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false };

    private IWidgetSurface? _surface;
    private FenceConfig _config = new();
    private bool _attached;

    /// <summary>[S-10913] The fence opened by the pointer resting on it (hover peek): it rolls up again when the pointer leaves.</summary>
    private bool _peeking;

    /// <summary>[S-10913] While peeking with the pointer over one of the fence's icons: the window's root, heard for the pointer leaving the fence.</summary>
    private UIElement? _peekWatch;

    /// <summary>[S-10910] The title's rename box while it shows.</summary>
    private InlineBox? _titleBox;

    /// <summary>[S-10912] A click on the title that a double-click may follow: the double-click takes its roll-up back.</summary>
    private bool _titleToggled;

    public FenceView(IWidgetHost host)
    {
        _host = host;
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        _titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _title.VerticalAlignment = VerticalAlignment.Center;
        _title.TextTrimming = TextTrimming.CharacterEllipsis;
        _title.TextWrapping = TextWrapping.NoWrap;
        _count.VerticalAlignment = VerticalAlignment.Center;
        _count.Margin = new Thickness(FamilySpace.S2, 0, 0, 0);
        _chevron.VerticalAlignment = VerticalAlignment.Center;
        _chevron.Margin = new Thickness(FamilySpace.S2, 0, 0, 0);
        SetColumn(_count, 1);
        SetColumn(_chevron, 2);
        _titleBar.Children.Add(_title);
        _titleBar.Children.Add(_count);
        _titleBar.Children.Add(_chevron);
        _divider.Background = Family.Brush(ColourRole.Track);
        _titleBar.Children.Add(_divider);
        SetColumnSpan(_divider, 3);

        // Clear, not null: the bar takes the press (the card's move) and the click (the roll-up).
        _titleBar.Background = Fluent.Clear();
        Children.Add(_titleBar);

        _empty.HorizontalAlignment = HorizontalAlignment.Center;
        _empty.VerticalAlignment = VerticalAlignment.Center;
        _empty.TextAlignment = TextAlignment.Center;
        _empty.TextWrapping = TextWrapping.Wrap;
        _empty.Margin = new Thickness(FamilySpace.S3);
        _empty.IsHitTestVisible = false;
        _thumb.Background = Family.Brush(ColourRole.TextTertiary);
        _body.Children.Add(_canvas);
        _body.Children.Add(_empty);
        _body.Children.Add(_thumb);
        SetRow(_body, 1);
        Children.Add(_body);

        SizeChanged += (_, _) =>
        {
            OnRollSized();
            Layout();
        };
    }

    /// <summary>[S-13028] The roll-up the body was last drawn for; null before the first.</summary>
    private bool? _shownRolled;

    /// <summary>[S-13028] Rolled up and the card still gliding down to its title bar: the body stays till it is there.</summary>
    private bool _rolling;

    /// <summary>[S-13028] The card reached its title bar's height: the rolled-up body goes.</summary>
    private void OnRollSized()
    {
        if (_rolling && ActualHeight <= Cells.Title + 1)
        {
            _rolling = false;
            _body.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>[S-10901] The title bar is the card's top edge: the card adds no inset above it.</summary>
    public bool OwnsTopEdge => true;

    /// <summary>The fence's widget id: what it is asked by. Null in a render.</summary>
    private string? WidgetId => _surface?.WidgetId;

    /// <summary>Whether gestures act: on the desktop, outside edit mode, never in a preview.</summary>
    private bool Acts => _attached && _surface?.EditMode != true && _surface?.IsPreview != true && !_fixture.On && !_host.IsPreview && WidgetId is not null;

    private IDesktopFences Fences => _host.DesktopItems.Fences;

    /// <summary>
    /// [S-13040] The title bar's height and an icon box, in DIPs: the host's for the fence's display (whole grid rows, a desktop
    /// icon's box on the grid); in a picture the title bar as the sizing states it and the desktop's plain cell.
    /// </summary>
    private FenceCellsDip Cells
        => WidgetId is { } id && !_fixture.On && !_host.IsPreview
            ? Fences.Cells(id)
            : new FenceCellsDip(FenceGrid.TitleDip, DesktopCell.Grid(DesktopCell.Current));

    public void Bind(IWidgetSurface surface) => _surface = surface;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        Detach();
        _attached = true;
        _host.DesktopItems.Changed += OnDeskChanged;
        Strings.Changed += OnLanguage;
        _titleBar.Tapped += OnTitleTapped;
        _titleBar.DoubleTapped += OnTitleDoubleTapped;
        _body.PointerPressed += OnBodyPressed;
        _body.PointerWheelChanged += OnBodyWheel;
        PointerEntered += OnPointerEntered;
        PointerExited += OnPointerExited;
        ApplyConfig(config);
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as FenceConfig ?? new FenceConfig();
        Render();

        // [S-13023] A search fence searches when shown, and again when its rules or query changed.
        SearchIfNeeded();
    }

    public void Detach()
    {
        if (!_attached)
        {
            return;
        }

        _attached = false;
        _host.DesktopItems.Changed -= OnDeskChanged;
        Strings.Changed -= OnLanguage;
        _titleBar.Tapped -= OnTitleTapped;
        _titleBar.DoubleTapped -= OnTitleDoubleTapped;
        _body.PointerPressed -= OnBodyPressed;
        _body.PointerWheelChanged -= OnBodyWheel;
        PointerEntered -= OnPointerEntered;
        PointerExited -= OnPointerExited;
        WatchPeek(false);
        CloseTitleBox(commit: false);
        CancelSearch();
        _searchedFor = null;
        ClearCells();
    }

    private void OnLanguage(Locale locale) => Render();

    /// <summary>The listing or the selection changed: the count may be another, a search's files their looks.</summary>
    private void OnDeskChanged() => Render();

    // ---- keys ([S-13011]) ---------------------------------------------------------------------------

    /// <summary>The keys with the lease are the desktop's, from this fence (so a paste or a new folder lands here); F2 with no item of it focused renames the fence.</summary>
    public bool HandleKey(WidgetKey key)
    {
        if (key == WidgetKey.Escape || !Acts || _titleBox is not null)
        {
            return false;
        }

        // [S-13023] A search fence's files are its own: its keys work on them.
        if (Searches && key != WidgetKey.Rename)
        {
            return FoundKey(key);
        }

        if (_host.DesktopItems.Key(key, SelectionInput.ShiftHeld(), WidgetId))
        {
            return true;
        }

        if (key == WidgetKey.Rename)
        {
            OpenTitleBox();
            return true;
        }

        return false;
    }

    public bool HandleText(char character) => Acts && _titleBox is null && !Searches && _host.DesktopItems.Type(character);

    public bool KeepsSelectionWhenLeft => true;

    // ---- the card's menu ([S-10911]) ---------------------------------------------------------------

    /// <summary>[S-10911] The fence's own commands on its card's menu: Paste and New folder into it, Rename, Roll up, Remove fence.</summary>
    public IReadOnlyList<WidgetAction> ContextActions
    {
        get
        {
            if (!Acts || WidgetId is not { } id)
            {
                return [];
            }

            // [S-13023] A search fence holds no file: nothing is pasted or made in it; it searches again.
            if (Searches)
            {
                return
                [
                    new(Glyphs.Refresh, Str.Common_Refresh.Text(), () => SearchIfNeeded(again: true)),
                    new(Glyphs.Rename, FenceStr.Fence_Rename.Text(), OpenTitleBox),
                    new(_config.RolledUp ? Glyphs.ChevronDown : Glyphs.ChevronUp, (_config.RolledUp ? FenceStr.Fence_Unroll : FenceStr.Fence_RollUp).Text(), () => Fences.RollUp(id, !_config.RolledUp)),
                    new(Glyphs.Delete, FenceStr.Fence_Remove.Text(), () => _ = RemoveAsync(id)),
                ];
            }

            return
            [
                new(Glyphs.Paste, Str.CtxMenu_Paste.Text(), () => Fences.Paste(id)),
                new(Glyphs.NewFolder, Str.Shell_Files_NewFolder.Text(), () => Fences.NewFolder(id)),
                new(Glyphs.Rename, FenceStr.Fence_Rename.Text(), OpenTitleBox),
                new(_config.RolledUp ? Glyphs.ChevronDown : Glyphs.ChevronUp, (_config.RolledUp ? FenceStr.Fence_Unroll : FenceStr.Fence_RollUp).Text(), () => Fences.RollUp(id, !_config.RolledUp)),
                new(Glyphs.Delete, FenceStr.Fence_Remove.Text(), () => _ = RemoveAsync(id)),
            ];
        }
    }

    /// <summary>
    /// [S-13009] Remove fence: never a file. With items, it asks first ("Put the 3 items back on the desktop?", Put back
    /// the action); the answer leaves them where it stood, Cancel keeps the fence. An empty fence goes at once.
    /// </summary>
    private async Task RemoveAsync(string id)
    {
        try
        {
            var count = _config.Items.Count;
            if (count == 0 || await ConfirmFlyout.AskAsync(_titleBar, FenceStr.Fence_PutBack.Plural(count), FenceStr.Fence_PutBack_Action.Text()).ConfigureAwait(true))
            {
                Fences.Remove(id, putBack: true);
            }
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "removing a fence failed", ex);
        }
    }

    // ---- the title bar ([S-10910], [S-10912], [S-10913]) ------------------------------------------

    /// <summary>[S-10912] A click on the title rolls the fence up or unrolls it (the setting's); one that peeked it open keeps it open.</summary>
    private void OnTitleTapped(object sender, TappedRoutedEventArgs e)
    {
        _titleToggled = false;
        if (!Acts || _titleBox is not null || WidgetId is not { } id)
        {
            return;
        }

        if (_peeking)
        {
            _peeking = false;
            WatchPeek(false);
            return;
        }

        if (_config.RollUpOnClick)
        {
            _titleToggled = true;
            Fences.RollUp(id, !_config.RolledUp);
        }
    }

    /// <summary>[S-10910] A double-click on the title renames the fence (the click before it rolled nothing for good: taken back).</summary>
    private void OnTitleDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (!Acts || WidgetId is not { } id)
        {
            return;
        }

        e.Handled = true;
        if (_titleToggled)
        {
            _titleToggled = false;
            Fences.RollUp(id, !_config.RolledUp);
        }

        OpenTitleBox();
    }

    /// <summary>[S-10913] The pointer rests on a rolled-up fence: it opens while the pointer is over it.</summary>
    private void OnPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        WatchPeek(false);
        if (Acts && _config.RolledUp && _config.HoverPeek && WidgetId is { } id && !_peeking)
        {
            _peeking = true;
            Fences.RollUp(id, false);
        }
    }

    /// <summary>
    /// [S-10913] The pointer left a fence it peeked open: it rolls up again (unless a drag or a rename holds it). Onto one of
    /// its own icons (drawn over the card) it has not left: the window's root is heard till the pointer leaves the fence.
    /// </summary>
    private void OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (!_peeking)
        {
            return;
        }

        if (Inside(e.GetCurrentPoint(this).Position))
        {
            WatchPeek(true);
            return;
        }

        EndPeek();
    }

    /// <summary>[S-10913] Roll a peeked fence up again, unless its rename box or a drag holds it.</summary>
    private void EndPeek()
    {
        WatchPeek(false);
        if (_peeking && WidgetId is { } id && _titleBox is null && !Fences.DeskDragRunning)
        {
            _peeking = false;
            Fences.RollUp(id, true);
        }
    }

    private bool Inside(Windows.Foundation.Point at) => at.X >= 0 && at.Y >= 0 && at.X < ActualWidth && at.Y < ActualHeight;

    /// <summary>[S-10913] Hear the window's root (every pointer move, handled or not) while the pointer is over the fence's icons.</summary>
    private void WatchPeek(bool on)
    {
        if (_peekWatch is { } watched)
        {
            watched.RemoveHandler(PointerMovedEvent, (PointerEventHandler)OnPeekMoved);
            _peekWatch = null;
        }

        if (on && XamlRoot?.Content is UIElement root)
        {
            root.AddHandler(PointerMovedEvent, (PointerEventHandler)OnPeekMoved, handledEventsToo: true);
            _peekWatch = root;
        }
    }

    private void OnPeekMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!Inside(e.GetCurrentPoint(this).Position))
        {
            EndPeek();
        }
    }

    /// <summary>
    /// [S-10910] The title becomes a box (F2, a double-click, the menu's Rename): Enter or a click elsewhere keeps it, Escape
    /// takes it back. [S-13950] The box stands in the title's own box (its line, its type, its text edge): the title bar keeps
    /// its height and the count and the chevron stay where they are.
    /// </summary>
    private void OpenTitleBox()
    {
        if (_titleBox is not null || (!Acts && !_fixture.On))
        {
            return;
        }

        var box = new InlineBox(_title, _config.Name.Length > 0 ? _config.Name : FenceStr.Fence_Label.Text())
        {
            IsSpellCheckEnabled = false,
            MaxLength = FenceConfig.MaxName,
        };
        _titleBox = box;
        box.StandIn();
        if (_fixture.On)
        {
            box.Loaded += (_, _) => box.SelectAll();
            return;
        }

        box.GotFocus += (_, _) => _surface?.RequestTextInput(true);
        box.LostFocus += (_, _) => CloseTitleBox(commit: true);
        box.KeyDown += (_, args) =>
        {
            if (args.Key == Windows.System.VirtualKey.Enter)
            {
                args.Handled = true;
                CloseTitleBox(commit: true);
            }
            else if (args.Key == Windows.System.VirtualKey.Escape)
            {
                args.Handled = true;
                CloseTitleBox(commit: false);
            }
        };
        box.Loaded += (_, _) =>
        {
            box.Focus(FocusState.Programmatic);
            box.SelectAll();
        };
    }

    private void CloseTitleBox(bool commit)
    {
        if (_titleBox is not { } box)
        {
            return;
        }

        _titleBox = null;
        box.StandOut();
        _surface?.RequestTextInput(false);
        if (commit && WidgetId is { } id)
        {
            var name = FenceConfig.CleanName(box.Text);
            Fences.Rename(id, string.Equals(name, FenceStr.Fence_Label.Text(), StringComparison.Ordinal) ? string.Empty : name);
        }
    }

    /// <summary>
    /// [S-13020] A left press on the fence's empty body (between its icons, past its last) starts the desktop's band there,
    /// as on empty desktop: it selects this fence's icons (and the free icons and other fences' it reaches) by the
    /// desktop's rules. Never the card's move: the fence moves by its title bar.
    /// </summary>
    private void OnBodyPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!Acts)
        {
            return;
        }

        e.Handled = true;

        // [S-13023] A search fence's files are in no desktop selection: a press on its empty body clears its own.
        if (Searches)
        {
            if (_picks.Clear())
            {
                PaintCells();
            }

            return;
        }

        if (WidgetId is { } id && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _ = Fences.Band(id, e);
        }
    }

    /// <summary>
    /// [S-13043] The wheel over a search fence's body scrolls its files by whole rows. A fence of desktop icons is scrolled by
    /// the surface (the wheel over its icons reaches it there too), so its icons move a row of the grid at once.
    /// </summary>
    private void OnBodyWheel(object sender, PointerRoutedEventArgs e)
    {
        if (!Searches || !Acts)
        {
            return;
        }

        var delta = e.GetCurrentPoint(this).Properties.MouseWheelDelta;
        var layout = CellLayout(_found.Count);
        var to = (layout with { Scroll = _foundScroll + (delta > 0 ? -1 : 1) }).Clamped(_found.Count).Scroll;
        e.Handled = true;
        if (to != _foundScroll)
        {
            _foundScroll = to;
            Layout();
        }
    }

    // ---- drawing ------------------------------------------------------------------------------------

    /// <summary>[S-10901] [S-10902] The title bar and the body as the config and the desktop say now.</summary>
    private void Render()
    {
        var items = ShownItems();
        var count = Searches || _fixture.On ? items.Count : _config.Items.Count;
        _title.Text = _config.Name.Length > 0 ? _config.Name : FenceStr.Fence_Label.Text();
        _count.Text = FenceStr.Fence_Count.Plural(count);
        _chevron.Glyph = _config.RolledUp ? Glyphs.ChevronDown : Glyphs.ChevronUp;
        _divider.Visibility = _config.RolledUp ? Visibility.Collapsed : Visibility.Visible;
        _titleBar.Height = Cells.Title;

        // [S-13028] Rolled up just now, the body stays while the card's foot glides up over it (its row the title bar's
        // leftover), so what it draws is covered rather than gone at once; it goes when the card is down to its title bar
        // (OnRollSized). Let down, it is there at once and the foot glides down to show it. The empty sentence, centred,
        // would stand over the title bar in a row of no height: it goes while rolled up.
        _rolling = _config.RolledUp && (_rolling || (_shownRolled == false && _attached && !_fixture.On));
        _shownRolled = _config.RolledUp;
        _body.Visibility = !_config.RolledUp || _rolling ? Visibility.Visible : Visibility.Collapsed;
        _empty.Text = Searches && !_fixture.On ? SearchEmptyText() : FenceStr.Fence_Empty.Text();
        _empty.Visibility = count == 0 && !_config.RolledUp ? Visibility.Visible : Visibility.Collapsed;
        SyncCells(items);
        Layout();
        PaintCells();
    }

    /// <summary>[S-13040] The body's cells in the fence's DIPs: under the title bar, whole icon boxes, from the scroll given.</summary>
    private FenceGrid.Layout CellLayout(int count, int scroll = 0)
    {
        var cells = Cells;
        return new FenceGrid.Layout(0, 0, Math.Max(0, ActualWidth), _config.RolledUp ? 0 : Math.Max(0, ActualHeight - cells.Title), cells.Box.Width, cells.Box.Height, scroll)
            .Clamped(count);
    }

    /// <summary>
    /// [S-13040] [S-13043] The cells it draws (a search's files, a picture's items) on the body's grid, and the thumb that
    /// says where the body stands in its rows (a fence of desktop icons: its config's scroll, the host's).
    /// </summary>
    private void Layout()
    {
        var count = Searches || _fixture.On ? _shown.Count : _config.Items.Count;
        var layout = CellLayout(count, Searches ? _foundScroll : _fixture.On ? 0 : _config.Scroll);
        if (Searches)
        {
            _foundScroll = layout.Scroll;
        }

        for (var i = 0; i < _shown.Count; i++)
        {
            if (_cells.TryGetValue(_shown[i], out var view))
            {
                var (x, y, w, h) = layout.Cell(i);
                view.Root.Visibility = layout.Shows(i) ? Visibility.Visible : Visibility.Collapsed;
                Canvas.SetLeft(view.Root, x);
                Canvas.SetTop(view.Root, y);
                view.Root.Width = w;
                view.Root.Height = h;
            }
        }

        _canvas.Width = Math.Max(0, ActualWidth);
        _canvas.Height = Math.Max(0, layout.Height);
        _canvas.Clip = new Microsoft.UI.Xaml.Media.RectangleGeometry { Rect = new Windows.Foundation.Rect(0, 0, _canvas.Width, _canvas.Height) };

        var rows = FenceGrid.RowsOf(count, layout.Columns);
        if (layout.Rows > 0 && rows > layout.Rows)
        {
            var track = layout.Rows * layout.CellHeight;
            _thumb.Height = Math.Max(FamilySpace.S3, track * layout.Rows / rows);
            _thumb.Margin = new Thickness(0, (track - _thumb.Height) * layout.Scroll / Math.Max(1, rows - layout.Rows), 0, 0);
            _thumb.Visibility = Visibility.Visible;
        }
        else
        {
            _thumb.Visibility = Visibility.Collapsed;
        }
    }
}
