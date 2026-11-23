#if DEBUG
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Widgets;
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
using Windows.Foundation;
using Windows.System;
using G = Themia.Sdk.Kit.ShellMetrics.WidgetGallery;

namespace Themia.App.Shell;

/// <summary>
/// [S-16745] The owner's editor of the gallery's tabs (owner, 2026-11-23: "for the widget gallery i want to have an editor ...
/// this should only be for me to be able to edit the widget gallery"; asked where: "In the gallery"), in Debug builds alone:
/// this file is compiled only there, so the Store's app has no trace of it. "Edit gallery" in the head turns it on for the
/// tab shown: every column its outline and its tools (a section added, the column removed), the edge between two columns
/// dragged to resize them (whole cells), every section its title box and its tools (up, down, a widget added from every
/// kind and preset or the widgets not placed yet, removed), a widget dragged into another section or column (it lands in
/// reading order where it is let go), resized by its corner in whole cells (its section packs again as it goes), removed by
/// Delete or its ×. Ctrl+Z and Ctrl+Y step back and forth through the session's edits; Save writes the edited tabs' files
/// into the repo the build was made from (<see cref="CuratedLayouts.Folder"/>), for the owner to commit.
/// </summary>
internal sealed partial class WidgetGalleryView
{
    /// <summary>[S-16745] The tabs edited this session, over the build's files (the files saved are only read at the next build).</summary>
    private static readonly Dictionary<AddFolderId, CuratedLayout> EditedLayouts = [];

    /// <summary>[S-16745] The tabs edited since they were last saved.</summary>
    private static readonly HashSet<AddFolderId> Unsaved = [];

    private static readonly List<(AddFolderId Tab, CuratedLayout Layout)> UndoSteps = [];
    private static readonly List<(AddFolderId Tab, CuratedLayout Layout)> RedoSteps = [];

    private bool _editOn;

    /// <summary>The editor's chrome over the cards: rebuilt with the sheet (<see cref="_editChrome"/>), and what a gesture draws (<see cref="_editOver"/>).</summary>
    private readonly Canvas _editLayer = new() { Visibility = Visibility.Collapsed };
    private readonly Canvas _editChrome = new();
    private readonly Canvas _editPick = new();
    private readonly Canvas _editOver = new() { IsHitTestVisible = false };
    private readonly Rectangle _editDrop = new() { IsHitTestVisible = false, Visibility = Visibility.Collapsed, StrokeThickness = FamilyShape.Stroke };

    private readonly StackPanel _editHead = new() { Orientation = Orientation.Horizontal, Spacing = Fluent.Space1, VerticalAlignment = VerticalAlignment.Center };
    private readonly StackPanel _editTools = new() { Orientation = Orientation.Horizontal, Spacing = Fluent.Space1, Visibility = Visibility.Collapsed };
    private readonly TextBlock _editStatus = Fluent.Text(FamilyType.Caption, "TextFillColorSecondaryBrush");
    private readonly Button _editToggle = new();

    /// <summary>A drag of the editor's under way (a column's edge, a widget's corner, a widget moved): the pointer is the cards' room's.</summary>
    private EditGesture? _gesture;

    /// <summary>[S-16745] Whether the tab shown is being edited: the editor on, a tab (never a search, never Not on view).</summary>
    private bool Editing => _editOn && _unplaced is null && !Searching;

    /// <summary>[S-16745] Whether the editor is on, for a check and a render.</summary>
    internal bool EditingForCheck => Editing;

    private sealed class EditGesture(Action<Point> step, Action<Point> end)
    {
        public bool Changed { get; set; }

        public void Step(Point at) => step(at);

        public void End(Point at) => end(at);
    }

    /// <summary>The editor's parts: its toggle and tools in the head, its layer over the cards.</summary>
    private void BuildEditor()
    {
        _head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _editHead.Margin = new Thickness(Fluent.Space2, 0, 0, 0);
        SetColumn(_editHead, 3);
        _editTools.Children.Add(Tool(Glyphs.Undo, Str.WidgetGallery_EditUndo.Text(), Undo));
        _editTools.Children.Add(Tool(Glyphs.Redo, Str.WidgetGallery_EditRedo.Text(), Redo));
        _editTools.Children.Add(Worded(Str.WidgetGallery_EditAddColumn.Text(), () => Apply(CuratedEdits.AddColumn)));
        _editTools.Children.Add(Worded(Str.WidgetGallery_EditSave.Text(), Save));
        _editStatus.VerticalAlignment = VerticalAlignment.Center;
        _editTools.Children.Add(_editStatus);
        _editHead.Children.Add(_editTools);
        _editToggle.Click += (_, _) => ToggleEditing();
        _editHead.Children.Add(_editToggle);
        _head.Children.Add(_editHead);

        _editDrop.Stroke = Family.Brush(ColourRole.Accent);
        _editDrop.Fill = HexBrush.Wash(ColourRole.Accent);
        _editOver.Children.Add(_editDrop);
        _editLayer.Children.Add(_editChrome);
        _editLayer.Children.Add(_editPick);
        _editLayer.Children.Add(_editOver);
        _stage.Children.Insert(_stage.Children.IndexOf(_input) + 1, _editLayer);
        SyncEditHead();
    }

    /// <summary>[S-16566] The head's room the editor's toggle (and, on, its tools) takes from the line.</summary>
    private double EditorRoom()
    {
        _editHead.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return Math.Ceiling(_editHead.DesiredSize.Width) + _editHead.Margin.Left;
    }

    /// <summary>[S-16745] Edit gallery: on, the tab shown becomes curated (laid from its automatic bento where it has no file yet) and editable; off, it shows as users see it.</summary>
    internal void ToggleEditing()
    {
        _editOn = !_editOn;
        if (_editOn)
        {
            if (Searching)
            {
                Search(string.Empty);
            }

            if (_unplaced is not null)
            {
                Show(_at);
            }

            if (_base is { } shown && CuratedOf(shown) is null && _laid is { } sheet)
            {
                EditedLayouts[shown.Id] = CuratedLayouts.From(sheet);
                Unsaved.Add(shown.Id);
            }
        }

        // The search is no part of editing: a tab is edited, never what a search finds.
        _search.IsHitTestVisible = !_editOn;
        _search.Opacity = _editOn ? 0.4 : 1;
        if (_base is { } now && _unplaced is null)
        {
            Present(now, Lay(now));
        }

        SyncEditHead();
    }

    private void SyncEditHead()
    {
        _editToggle.Content = _editOn ? Str.WidgetGallery_EditDone.Text() : Fluent.Icon(Glyphs.Edit);
        ToolTipService.SetToolTip(_editToggle, _editOn ? null : Str.WidgetGallery_Edit.Text());
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_editToggle, Str.WidgetGallery_Edit.Text());
        _editTools.Visibility = _editOn ? Visibility.Visible : Visibility.Collapsed;
        _editStatus.Text = Unsaved.Count > 0 ? Str.WidgetGallery_EditUnsaved.Plural(Unsaved.Count) : string.Empty;
        ShareHead();
    }

    // ---- the chrome --------------------------------------------------------------------------------------------------------

    /// <summary>[S-16745] The editor's chrome for the sheet shown, scrolled as it stands; none while not editing.</summary>
    private void PlaceEditor()
    {
        _editChrome.Children.Clear();
        _editDrop.Visibility = Visibility.Collapsed;
        var on = Editing && _laid is not null && _base is not null && CuratedOf(_base) is not null;
        _editLayer.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        if (!on)
        {
            _editPick.Children.Clear();
            return;
        }

        var sheet = _laid!;
        var layout = CuratedOf(_base!)!;
        _editLayer.Clip = new RectangleGeometry { Rect = _clip };
        var band = Dip(new CellBox(0, 0, 1, Sizes.HeadingRows)).Height;
        for (var c = 0; c < sheet.Columns.Count; c++)
        {
            var column = c;
            var box = sheet.Columns[c];
            var dip = Dip(box);
            var outline = new Rectangle
            {
                Stroke = Family.Brush(ColourRole.Accent),
                StrokeThickness = FamilyShape.Hairline,
                StrokeDashArray = [4, 3],
                IsHitTestVisible = false,
            };
            Put(outline, dip);

            var tools = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Fluent.Space1, Height = band };
            tools.Children.Add(Tool(Glyphs.Add, Str.WidgetGallery_EditAddSection.Text(), () => Apply(l => CuratedEdits.AddSection(l, column)), band));
            if (layout.Columns.Count > 1)
            {
                tools.Children.Add(Tool(Glyphs.Delete, Str.WidgetGallery_EditRemoveColumn.Text(), () => Apply(l => CuratedEdits.RemoveColumn(l, column)), band));
            }

            Put(tools, dip with { Height = band }, size: false);

            // [S-16744] The edge to the next column on the same line: dragged, the two share their width anew.
            if (c + 1 < sheet.Columns.Count && sheet.Columns[c + 1] is { } next && next.Y == box.Y && next.X == box.Right + WidgetGallery.Gap)
            {
                var edge = new Border { Background = HexBrush.Wash(ColourRole.Accent), BorderBrush = Family.Brush(ColourRole.Accent), BorderThickness = new Thickness(FamilyShape.Hairline, 0, FamilyShape.Hairline, 0) };
                ToolTipService.SetToolTip(edge, Str.WidgetGallery_EditColumnEdge.Text());
                edge.PointerPressed += (_, e) => BeginEdge(column, box.W, next.W, e);
                Put(edge, Dip(new CellBox(box.Right, box.Y, WidgetGallery.Gap, Math.Max(box.H, next.H))));
            }
        }

        foreach (var heading in sheet.Headings)
        {
            if (heading.Slot is { } slot)
            {
                Put(Heading(slot, CuratedEdits.SectionOf(layout, slot)?.Title, layout.Columns[slot.Column].Sections.Count), Dip(heading.Box));
            }
        }

        PlacePickGrip();
    }

    /// <summary>A section's heading line while editing: its title box and its tools.</summary>
    private Grid Heading(CuratedSlot slot, string? title, int sections)
    {
        var line = new Grid { ColumnSpacing = Fluent.Space1 };
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var box = new TextBox
        {
            Text = title ?? string.Empty,
            PlaceholderText = Str.WidgetGallery_EditTitle.Text(),
            MinHeight = 0,
            VerticalAlignment = VerticalAlignment.Center,
        };
        void Rename()
        {
            if (!string.Equals((box.Text ?? string.Empty).Trim(), title ?? string.Empty, StringComparison.Ordinal))
            {
                Apply(l => CuratedEdits.Rename(l, slot, box.Text));
            }
        }

        box.LostFocus += (_, _) => Rename();
        box.KeyDown += (_, e) =>
        {
            if (e.Key == VirtualKey.Enter)
            {
                e.Handled = true;
                _commands.TakeKeys();
            }
        };
        line.Children.Add(box);
        var tools = new (string Glyph, string Tip, Action Act, bool Shown)[]
        {
            (Glyphs.ChevronUp, Str.WidgetGallery_EditSectionUp.Text(), () => Apply(l => CuratedEdits.MoveSection(l, slot, -1)), slot.Section > 0),
            (Glyphs.ChevronDown, Str.WidgetGallery_EditSectionDown.Text(), () => Apply(l => CuratedEdits.MoveSection(l, slot, 1)), slot.Section < sections - 1),
            (Glyphs.Add, Str.WidgetGallery_EditAddWidget.Text(), () => { }, true),
            (Glyphs.Delete, Str.WidgetGallery_EditRemoveSection.Text(), () => Apply(l => CuratedEdits.RemoveSection(l, slot)), true),
        };
        foreach (var (glyph, tip, act, shown) in tools)
        {
            if (!shown)
            {
                continue;
            }

            var button = Tool(glyph, tip, act);
            if (ReferenceEquals(glyph, Glyphs.Add))
            {
                button.Click += (_, _) => AddMenu(button, slot);
            }

            line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            SetColumn(button, line.ColumnDefinitions.Count - 1);
            line.Children.Add(button);
        }

        return line;
    }

    /// <summary>[S-16745] The picked widget's corner grip and its ×, on its card.</summary>
    private void PlacePickGrip()
    {
        _editPick.Children.Clear();
        if (!Editing || _laid is not { } sheet || _selection.Ids.Count != 1 || CardOf(_selection.Ids[0]) is not { } card
            || !_placed.TryGetValue(card.Model.Id, out var at) || sheet.Tiles[at.Tile].Slot is not { } slot)
        {
            return;
        }

        var cell = GridGeometry.PaintedCell(card.LiveRect, _metrics, _raster);
        var size = Fluent.IconSize + Fluent.Space1;
        var grip = new Border
        {
            Width = size,
            Height = size,
            Background = Family.Brush(ColourRole.Accent),
            CornerRadius = new CornerRadius(FamilyShape.Radius),
        };
        ToolTipService.SetToolTip(grip, Str.WidgetGallery_EditResize.Text());
        grip.PointerPressed += (_, e) => BeginResize(slot, sheet.CardOf(sheet.Tiles[at.Tile]), sheet.Columns[slot.Column].W, e);
        Canvas.SetLeft(grip, cell.X + cell.Width - (size / 2));
        Canvas.SetTop(grip, cell.Y + cell.Height - (size / 2));
        _editPick.Children.Add(grip);

        var remove = Tool(Glyphs.Delete, Str.WidgetGallery_EditRemoveWidget.Text(), () => Apply(l => CuratedEdits.RemoveWidget(l, slot)));
        Canvas.SetLeft(remove, cell.X + cell.Width - size);
        Canvas.SetTop(remove, cell.Y - size);
        _editPick.Children.Add(remove);
    }

    /// <summary>[S-16745] The widgets a section may take: the ones no tab places yet first, then every tab's own.</summary>
    private void AddMenu(FrameworkElement at, CuratedSlot section)
    {
        var menu = new MenuFlyout();
        var placed = CuratedLayouts.Placed(_categories.Select(CuratedOf).OfType<CuratedLayout>());
        var pieces = _categories.SelectMany(c => c.Pieces).Where(p => !p.Least && p.Pick is not null).DistinctBy(p => Curated(p).Identity).ToList();
        var loose = new MenuFlyoutSubItem { Text = Str.WidgetGallery_EditNotPlaced.Text() };
        foreach (var piece in pieces.Where(p => !placed.Contains(Curated(p).Identity)))
        {
            loose.Items.Add(Item(piece));
        }

        if (loose.Items.Count > 0)
        {
            menu.Items.Add(loose);
            menu.Items.Add(new MenuFlyoutSeparator());
        }

        foreach (var category in _categories)
        {
            var sub = new MenuFlyoutSubItem { Text = category.Title.Text() };
            foreach (var piece in category.Pieces.Where(p => !p.Least && p.Pick is not null).DistinctBy(p => Curated(p).Identity))
            {
                sub.Items.Add(Item(piece));
            }

            if (sub.Items.Count > 0)
            {
                menu.Items.Add(sub);
            }
        }

        menu.ShowAt(at);

        MenuFlyoutItem Item(GalleryPiece piece)
        {
            var item = new MenuFlyoutItem { Text = Named(piece) };
            item.Click += (_, _) => Apply(l => CuratedEdits.AddWidget(l, section, Curated(piece)));
            return item;
        }
    }

    /// <summary>A piece's name in the editor's menus: its title, and its style where it shows one.</summary>
    private static string Named(GalleryPiece piece)
        => piece.Style is { } style && piece.Kind is { } kind && WidgetStyles.For(kind).FirstOrDefault(s => s.Value == style) is { Name: not null } look
            ? $"{Title(piece)} · {look.Label.Text()}"
            : Title(piece);

    /// <summary>[S-16742] A piece as a curated widget: its pick, its style's name, the size it asks here.</summary>
    private CuratedWidget Curated(GalleryPiece piece)
    {
        var size = Sizes.Of(piece);
        var style = piece.Style is { } value && piece.Kind is { } kind ? WidgetStyles.For(kind).FirstOrDefault(s => s.Value == value).Name : null;
        return new CuratedWidget { Pick = piece.Pick!.Value.Key, Style = style, W = size.W, H = size.H };
    }

    // ---- gestures ----------------------------------------------------------------------------------------------------------

    /// <summary>[S-16744] The edge after column <paramref name="column"/> pressed: dragged, the two columns share their cells anew (each at least <see cref="CuratedBento.LeastColumn"/>).</summary>
    private void BeginEdge(int column, int left, int right, PointerRoutedEventArgs e)
    {
        if (_base is not { } shown || CuratedOf(shown) is not { } before)
        {
            return;
        }

        var start = e.GetCurrentPoint(_stage).Position.X;
        var total = left + right;
        var least = Math.Min(CuratedBento.LeastColumn, Math.Max(1, total / 2));
        var last = left;
        Begin(e, new EditGesture(
            at =>
            {
                var now = Math.Clamp(left + (int)Math.Round((at.X - start) / Pitch.X), least, total - least);
                if (now != last)
                {
                    last = now;
                    _gesture!.Changed = true;
                    Relay(shown.Id, CuratedEdits.Edge(before, column, now, total - now));
                }
            },
            _ => Done(shown.Id, before)));
    }

    /// <summary>[S-16745] A widget's corner pressed: dragged, its card is made whole cells wide and tall (no wider than its column, within its sizing), its section packing as it goes.</summary>
    private void BeginResize(CuratedSlot slot, CellBox card, int column, PointerRoutedEventArgs e)
    {
        if (_base is not { } shown || CuratedOf(shown) is not { } before || CuratedEdits.Widget(before, slot) is not { } widget)
        {
            return;
        }

        var last = (widget.W, widget.H);
        Begin(e, new EditGesture(
            at =>
            {
                var cell = CellAt(at);
                var asked = widget with { W = Math.Max(1, cell.X - card.X + 1), H = Math.Max(1, cell.Y - card.Y + 1) };
                var held = Sizes.Curated.Size(asked, column);
                if (held != last)
                {
                    last = held;
                    _gesture!.Changed = true;
                    Relay(shown.Id, CuratedEdits.ResizeWidget(before, slot, held.W, held.H));
                }
            },
            _ => Done(shown.Id, before)));
    }

    /// <summary>A gesture of the editor's begun: the pointer captured by the cards' room, so a chrome rebuilt under it never drops it.</summary>
    private void Begin(PointerRoutedEventArgs e, EditGesture gesture)
    {
        _press = null;
        _gesture = gesture;
        _ = _input.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    /// <summary>[S-16745] A live gesture let go: one undo step for all of it.</summary>
    private void Done(AddFolderId tab, CuratedLayout before)
    {
        if (EditedLayouts.TryGetValue(tab, out var after) && !Same(before, after))
        {
            UndoSteps.Add((tab, before));
            RedoSteps.Clear();
            Unsaved.Add(tab);
        }

        SyncEditHead();
    }

    /// <summary>
    /// [S-16745] The pointer moved while editing: a gesture's step, or a pressed widget dragged past the dead zone (it is
    /// moved inside the gallery, never placed). False: the gallery's own (the lasso).
    /// </summary>
    private bool EditMoved(PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(_stage);
        if (_gesture is { } gesture)
        {
            gesture.Step(point.Position);
            e.Handled = true;
            return true;
        }

        if (!Editing || _press is not { Card: { } card } press || !point.Properties.IsLeftButtonPressed)
        {
            return false;
        }

        if (!CardGestures.Travelled(point.Position.X - press.At.X, point.Position.Y - press.At.Y))
        {
            return true;
        }

        if (_laid is not { } sheet || !_placed.TryGetValue(card, out var at) || sheet.Tiles[at.Tile].Slot is not { } slot || CardOf(card) is not { } moving
            || _base is not { } shown || CuratedOf(shown) is not { } before)
        {
            return true;
        }

        _press = null;
        var (left, top) = (Canvas.GetLeft(moving), Canvas.GetTop(moving));
        var from = press.At;
        GalleryArea? target = null;
        _gesture = new EditGesture(
            now =>
            {
                Canvas.SetLeft(moving, left + (now.X - from.X));
                Canvas.SetTop(moving, top + (now.Y - from.Y));
                target = AreaAt(sheet, CellAt(now));
                _editDrop.Visibility = target is null ? Visibility.Collapsed : Visibility.Visible;
                if (target is not null)
                {
                    var box = Dip(target.Box);
                    Canvas.SetLeft(_editDrop, box.X);
                    Canvas.SetTop(_editDrop, box.Y);
                    _editDrop.Width = box.Width;
                    _editDrop.Height = box.Height;
                }
            },
            now =>
            {
                _editDrop.Visibility = Visibility.Collapsed;
                if (target is { } area)
                {
                    var index = CuratedBento.Insertion(sheet, area.Slot, CellAt(now));
                    Apply(l => CuratedEdits.MoveWidget(l, slot, area.Slot with { Widget = index }));
                }
                else
                {
                    Present(shown, Lay(shown));
                }
            });
        _gesture.Step(point.Position);
        e.Handled = true;
        return true;
    }

    /// <summary>[S-16745] The pointer let go while editing: a gesture ends. False: the gallery's own.</summary>
    private bool EditReleased(PointerRoutedEventArgs e)
    {
        if (_gesture is not { } gesture)
        {
            return false;
        }

        _gesture = null;
        _input.ReleasePointerCapture(e.Pointer);
        gesture.End(e.GetCurrentPoint(_stage).Position);
        e.Handled = true;
        return true;
    }

    /// <summary>The pointer's capture went mid-gesture: what it changed stays, as if let go where it was.</summary>
    private void EditLost()
    {
        if (_gesture is { } gesture)
        {
            _gesture = null;
            _editDrop.Visibility = Visibility.Collapsed;
            if (_base is { } shown)
            {
                Present(shown, Lay(shown));
            }

            _ = gesture;
        }
    }

    /// <summary>The section a widget dropped on cell <paramref name="cell"/> goes to: the one there, else the nearest above it in that column, else the column's first.</summary>
    private static GalleryArea? AreaAt(GallerySheet sheet, (int X, int Y) cell)
    {
        var column = sheet.Areas.Where(a => cell.X >= a.Box.X && cell.X < a.Box.Right).ToList();
        return column.FirstOrDefault(a => cell.Y >= a.Box.Y && cell.Y < a.Box.Bottom)
            ?? column.Where(a => a.Box.Y <= cell.Y).OrderByDescending(a => a.Box.Y).FirstOrDefault()
            ?? column.OrderBy(a => a.Box.Y).FirstOrDefault();
    }

    // ---- keys --------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// [S-16745] A key while editing: Ctrl+Z and Ctrl+Y step the session's edits, Delete removes the picked widgets, Enter
    /// places nothing; in a title box the keys are its own (Escape leaves it). Null: the gallery's own.
    /// </summary>
    private bool? EditKey(VirtualKey key, bool ctrl)
    {
        if (!Editing)
        {
            return null;
        }

        if (XamlRoot is { } root && FocusManager.GetFocusedElement(root) is TextBox)
        {
            if (key == VirtualKey.Escape)
            {
                _commands.TakeKeys();
                return true;
            }

            return ctrl && key is VirtualKey.Z or VirtualKey.Y;
        }

        switch (key)
        {
            case VirtualKey.Z when ctrl:
                Undo();
                return true;
            case VirtualKey.Y when ctrl:
                Redo();
                return true;
            case VirtualKey.Delete when _laid is { } sheet:
                var slots = _selection.Ids.Select(id => _placed.TryGetValue(id, out var at) ? sheet.Tiles[at.Tile].Slot : null).OfType<CuratedSlot>()
                    .Distinct().OrderByDescending(s => s.Column).ThenByDescending(s => s.Section).ThenByDescending(s => s.Widget).ToList();
                Apply(l => slots.Aggregate(l, CuratedEdits.RemoveWidget));
                return true;
            case VirtualKey.Enter:
                return true;
            default:
                return null;
        }
    }

    // ---- steps -------------------------------------------------------------------------------------------------------------

    /// <summary>[S-16745] One edit of the tab shown: an undo step, the tab laid again and drawn at once.</summary>
    private void Apply(Func<CuratedLayout, CuratedLayout> edit)
    {
        if (_base is not { } shown || CuratedOf(shown) is not { } before)
        {
            return;
        }

        var after = edit(before);
        if (Same(before, after))
        {
            return;
        }

        UndoSteps.Add((shown.Id, before));
        RedoSteps.Clear();
        Unsaved.Add(shown.Id);
        EditedLayouts[shown.Id] = after;
        Present(shown, Lay(shown));
        SyncEditHead();
    }

    private void Undo() => Step(UndoSteps, RedoSteps);

    private void Redo() => Step(RedoSteps, UndoSteps);

    /// <summary>The last step of <paramref name="from"/> taken back, the tab's layout before it kept in <paramref name="to"/>; its tab shown.</summary>
    private void Step(List<(AddFolderId Tab, CuratedLayout Layout)> from, List<(AddFolderId Tab, CuratedLayout Layout)> to)
    {
        if (from.Count == 0)
        {
            return;
        }

        var (tab, layout) = from[^1];
        from.RemoveAt(from.Count - 1);
        var index = _categories.ToList().FindIndex(c => c.Id == tab);
        if (index < 0 || CuratedOf(_categories[index]) is not { } now)
        {
            return;
        }

        to.Add((tab, now));
        EditedLayouts[tab] = layout;
        Unsaved.Add(tab);
        if (index != _at || _base is null || Searching)
        {
            Show(index);
        }
        else
        {
            Present(_base, Lay(_base));
        }

        SyncEditHead();
    }

    /// <summary>
    /// [S-16745] A live step (an edge or a corner dragged): the tab laid again, its cards moved and sized where they are the
    /// same pieces (nothing built again), else drawn afresh.
    /// </summary>
    private void Relay(AddFolderId tab, CuratedLayout layout)
    {
        EditedLayouts[tab] = layout;
        if (_base is not { } shown || _laid is not { } old)
        {
            return;
        }

        var sheet = Lay(shown);
        if (sheet.Tiles.Count != old.Tiles.Count || !sheet.Tiles.Select(t => t.Piece.Key).SequenceEqual(old.Tiles.Select(t => t.Piece.Key)))
        {
            Present(shown, sheet);
            return;
        }

        _laid = sheet;
        foreach (var (id, at) in _placed.ToList())
        {
            if (CardOf(id) is not { } card)
            {
                continue;
            }

            var held = Held(card.Model, sheet.CardOf(sheet.Tiles[at.Tile]));
            _placed[id] = (at.Tile, held);
            var rect = RectOf(held);
            card.Model.Rect = rect;
            card.SetLiveRect(rect);
            var cell = GridGeometry.PaintedCell(rect, _metrics, _raster);
            card.Width = cell.Width;
            card.Height = cell.Height;
            Canvas.SetLeft(card, cell.X);
            Canvas.SetTop(card, cell.Y);
            card.FollowMove(cell);
        }

        foreach (var (label, _) in _labels)
        {
            _cardLayer.Children.Remove(label);
        }

        _labels.Clear();
        if (sheet.Title > 0)
        {
            foreach (var tile in sheet.Tiles)
            {
                _labels.Add((Label(Title(tile.Piece), FamilyType.Caption, "TextFillColorSecondaryBrush"), tile.Box with { H = sheet.Title }));
            }
        }

        foreach (var (label, box) in _labels)
        {
            PlaceLabel(label, box);
            _cardLayer.Children.Add(label);
        }

        WidgetCard.Join(_cards, _metrics, _raster, editMode: false, _ => 0);
        _scroll = Math.Clamp(_scroll, 0, MostScroll);
        ShowScroll();
        Sync();
        PlaceEditor();
    }

    /// <summary>[S-16745] The edited tabs written into the repo the build was made from, each its file; a tab that cannot be written stays unsaved (logged).</summary>
    private void Save()
    {
        var folder = SourceFolder();
        if (!Directory.Exists(folder))
        {
            Log.Warn(Scope, $"the gallery's layouts cannot be saved: the source tree '{folder}' is not on this PC");
            _editStatus.Text = Str.WidgetGallery_EditNoSource.Text();
            return;
        }

        foreach (var tab in Unsaved.ToList())
        {
            var index = _categories.ToList().FindIndex(c => c.Id == tab);
            if (index < 0 || CuratedOf(_categories[index]) is not { } layout)
            {
                continue;
            }

            var path = System.IO.Path.Combine(folder, CuratedLayouts.FileName(tab));
            var temp = path + ".tmp";
            try
            {
                File.WriteAllText(temp, CuratedLayouts.Write(layout));
                File.Move(temp, path, overwrite: true);
                Unsaved.Remove(tab);
                Log.Info(Scope, $"the gallery's layout {CuratedLayouts.FileName(tab)} saved into the source tree");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log.Error(Scope, $"the gallery's layout {CuratedLayouts.FileName(tab)} could not be saved; it stays unsaved", ex);
            }
        }

        SyncEditHead();
        if (Unsaved.Count == 0)
        {
            _editStatus.Text = Str.WidgetGallery_EditSaved.Text();
        }
    }

    /// <summary>[S-16746] The layouts' folder in the repo this build was compiled from (the compiler's path of this file, Debug's alone).</summary>
    private static string SourceFolder([CallerFilePath] string here = "")
        => System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(here) ?? ".", "..", "..", "..", "..", CuratedLayouts.Folder));

    // ---- helpers -----------------------------------------------------------------------------------------------------------

    private static bool Same(CuratedLayout a, CuratedLayout b) => ReferenceEquals(a, b) || string.Equals(CuratedLayouts.Write(a), CuratedLayouts.Write(b), StringComparison.Ordinal);

    private (double X, double Y) Pitch
    {
        get
        {
            var (cols, rows) = GridGeometry.GridDims(_metrics);
            return (_metrics.Width / cols, _metrics.Height / rows);
        }
    }

    /// <summary>The sheet's cell under a point of the surface (DIPs), as the sheet stands scrolled.</summary>
    private (int X, int Y) CellAt(Point at)
    {
        var (cols, rows) = GridGeometry.GridDims(_metrics);
        return ((int)Math.Floor(at.X / _metrics.Width * cols) - _view.X, (int)Math.Floor(at.Y / _metrics.Height * rows) - _view.Y + _scroll);
    }

    /// <summary>A box of the sheet on the surface (DIPs), as it stands scrolled.</summary>
    private DipRect Dip(CellBox box) => GridGeometry.PaintedCell(RectOf(box), _metrics, _raster);

    private void Put(FrameworkElement element, DipRect box, bool size = true)
    {
        Canvas.SetLeft(element, box.X);
        Canvas.SetTop(element, box.Y);
        if (size)
        {
            element.Width = Math.Max(0, box.Width);
        }

        element.Height = Math.Max(0, box.Height);
        _editChrome.Children.Add(element);
    }

    private static Button Tool(string glyph, string tip, Action act, double height = double.NaN)
    {
        var button = new Button { Content = Fluent.Icon(glyph), Padding = new Thickness(Fluent.Space1), MinWidth = 0, MinHeight = 0, Height = height, VerticalAlignment = VerticalAlignment.Center };
        ToolTipService.SetToolTip(button, tip);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, tip);
        button.Click += (_, _) => act();
        return button;
    }

    private static Button Worded(string words, Action act)
    {
        var button = new Button { Content = words, VerticalAlignment = VerticalAlignment.Center };
        button.Click += (_, _) => act();
        return button;
    }
}
#endif
