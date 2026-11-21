using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.App.Widgets.Meters;
using Themia.Core.Diagnostics;
using Themia.Core.Model;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI.Kit;

namespace Themia.App.Widgets;

/// <summary>
/// [S-6300] The meter's styles, each the reading and one part of the Chart block's under it
/// (<see cref="MeterLook"/>): the history, a gauge, the items as columns, rows, rings or graphs, the
/// battery, a ring, the signal bars, or two readings. The reading is the same Readout on the same
/// ladder in every look; only the part changes, and what one look builds or watches is released when
/// another is chosen ([S-6005]). Each look is one <see cref="IMeterLook"/>: it builds its part, shows a reading and the
/// items on it, lays it in the content box and lets it go; the view holds the one it draws. Where each goes is
/// <see cref="MeterPlan"/>'s.
/// </summary>
public sealed partial class MeterView
{
    private MeterLook _look = (MeterLook)(-1);

    /// <summary>The look the view draws now.</summary>
    private IMeterLook _shown = new Bare();

    private MeterItemsWatch? _items;
    private IReadOnlyList<MeterItem> _itemList = [];

    /// <summary>
    /// [S-6300] One of the meter's looks: the part it draws beside the reading, and where the reading stands in it.
    /// </summary>
    private interface IMeterLook
    {
        /// <summary>Whether it draws the kind's items (cores, engines), which the view watches for it.</summary>
        bool WantsItems => false;

        /// <summary>[S-12814] The block the reading stands in, scaled as one (the Number looks); null for the others.</summary>
        ReadingFit? Fit => null;

        /// <summary>[S-12833] The parts its block states (the Number look's: its glyph and unit give way first); null: all drawn.</summary>
        FaceParts? Face => null;

        /// <summary>Build its part into the view, the reading moved where it stands in it.</summary>
        void Build();

        /// <summary>[S-6330] A reading on its own part: a sweep, a fill, the bars lit, a second figure.</summary>
        void Show(MeterReading reading, ColourRole role)
        {
        }

        /// <summary>[S-6330] The items on their parts, at once; a different count is a different layout.</summary>
        void ShowItems(IReadOnlyList<MeterItem> items, bool point)
        {
        }

        /// <summary>[S-11000] Its part laid in the content box (<paramref name="width"/> × <paramref name="height"/>, the insets' inside).</summary>
        void Lay(double width, double height)
        {
        }

        /// <summary>
        /// [S-12410] The height of the block the reading and its part make in that content box, centred in it; 0: it fills
        /// the box (a dial, rows, a scaled reading), none to centre.
        /// </summary>
        double Block(double width, double height) => 0;

        /// <summary>Take its part down, the reading back in its row.</summary>
        void Release();
    }

    /// <summary>Use <paramref name="look"/>: build its part (once), let go of the previous one's, and watch its items.</summary>
    private void UseLook(MeterLook look)
    {
        if (look != _look)
        {
            _look = look;
            Release();
            _shown = Make(look);
            _band.Visibility = Visibility.Collapsed;
            _shown.Build();
        }

        // [S-12833] The Number look's parts: in a smaller card its glyph and unit give way before its figures. The Numbers
        // look's two readings never go under their own size ([S-6264]): every part is drawn.
        if (_shown.Fit is { } fit)
        {
            fit.Face = _shown.Face;
        }

        WatchItems();
    }

    /// <summary>The look <paramref name="look"/> draws.</summary>
    private IMeterLook Make(MeterLook look) => look switch
    {
        MeterLook.Gauge or MeterLook.Ring => new Dial(this, half: look == MeterLook.Gauge),
        MeterLook.Number => new Number(this),
        MeterLook.Numbers => new Numbers(this),
        MeterLook.Columns => new Columns(this),
        MeterLook.Rows => new Rows(this),
        MeterLook.Tiles => new Tiles(this),
        MeterLook.Graphs => new Graphs(this),
        MeterLook.Battery => new Battery(this),
        MeterLook.Signal => new Signal(this),
        _ => new Bare(),
    };

    /// <summary>Take down the last look's part, and put the reading back in its row.</summary>
    private void Release()
    {
        _items?.Dispose();
        _items = null;
        _itemList = [];
        _shown.Release();
        _shown = new Bare();

        if (!Children.Contains(_reading))
        {
            Children.Add(_reading);
        }

        _reading.HorizontalAlignment = HorizontalAlignment.Left;
        _reading.VerticalAlignment = VerticalAlignment.Top;
        _reading.Margin = Family.SideInset;
        SetRow(_reading, 0);
    }

    /// <summary>[S-6300] The items of a columns, rows, rings or graphs look, from the hub; nothing for the others.</summary>
    private void WatchItems()
    {
        if (!_shown.WantsItems || _detached || _frozen)
        {
            _items?.Dispose();
            _items = null;
            return;
        }

        if (_items is not null)
        {
            return;
        }

        if (_host.System is not { } hub)
        {
            Log.Warn(Scope, $"no system hub for {_kind}'s items");
            return;
        }

        _items = _part.WatchItems(hub, OnItems);

        if (_items?.Current is { } current)
        {
            // [S-2506] What is read so far, and (the core graphs) every item's history with it.
            ShowItems(current, point: false);
            (_shown as Graphs)?.Load();
        }
    }

    private void OnItems(IReadOnlyList<MeterItem> items)
    {
        if (!_detached)
        {
            ShowItems(items, point: true);
        }
    }

    /// <summary>
    /// [S-6330] Put the items on their parts, at once; a different count is a different layout.
    /// <paramref name="point"/>: a new read, which the core graphs add to their lines.
    /// </summary>
    private void ShowItems(IReadOnlyList<MeterItem> items, bool point)
    {
        var recount = items.Count != _itemList.Count;
        _itemList = items;
        _shown.ShowItems(items, point);
        if (recount)
        {
            Lay();
        }
    }

    /// <summary>[S-11000] Place the atom's parts for the current area.</summary>
    private void Lay()
    {
        if (_area.IsMeasured)
        {
            LaySized();
        }
    }

    /// <summary>
    /// [S-11000] An atom's face in the box it is given, the same parts at every size ([S-2438]): a
    /// reading at 28 in its drawing, the history edge to edge, or the items in the card's inset. A
    /// larger card is a longer chart, a larger dial or wider rows, never more parts.
    /// </summary>
    private void LaySized()
    {
        var atom = _part.Atom;
        _reading.Visibility = atom == MeterAtom.Reading ? Visibility.Visible : Visibility.Collapsed;
        _reading.Use(FamilyType.Big28);

        Padding = default;

        // The reading's content box: the side insets and the foot's, joined or alone ([S-12483]).
        var width = Math.Max(0, _area.Width - (2 * FamilySpace.Inset));
        var height = Math.Max(0, _area.Height - FamilySpace.Inset);
        _shown.Lay(width, height);
        Padding = new Thickness(0, atom == MeterAtom.Reading ? Centring(width, height) : 0, 0, 0);
    }

    /// <summary>
    /// [S-12410] [S-12808] A reading's block is drawn at its natural size (its battery or bars up to their most): what a
    /// larger box has beyond it stands alike over and under it (the one block rule, <see cref="BlockLayout.Fill(double, double, double, double, LineInk, double, double)"/>,
    /// at its own size), the foot's inset kept under it, never huddled at the top, joined or alone ([S-12483]); a whole
    /// device pixel down. At its natural box this is none; a dial, rows or a scaled reading fill the box and take none. Padding on
    /// the view itself, never its alignment: the card measures the view's own box as its area ([S-663]).
    /// </summary>
    private double Centring(double width, double height)
    {
        var block = _shown.Block(width, height);
        if (!(block > 0))
        {
            return 0;
        }

        // A whole device pixel down (the one rounding token, LayoutMath.Pixel), so the picture's edges stand on pixels.
        var pixel = LayoutMath.Pixel(XamlRoot?.RasterizationScale ?? 0);
        var top = BlockLayout.Fill(width, height, 0, block, LineInk.None, least: 1, most: 1).Top;
        return Math.Floor((top / pixel) + 1e-9) * pixel;
    }

    /// <summary>[S-6330] A reading on the look's own part.</summary>
    private void PresentLook(MeterReading reading, ColourRole role) => _shown.Show(reading, role);

    /// <summary>The reading into a look's box: out of the view's own row first.</summary>
    private void Move(FrameworkElement element, Panel to)
    {
        Children.Remove(element);
        to.Children.Add(element);
    }

    /// <summary>A look's part in the row under the reading.</summary>
    private void Body(FrameworkElement body)
    {
        SetRow(body, 1);
        Children.Add(body);
    }

    /// <summary>
    /// [S-11005] Items alone, from the view's top, which is the card's ([S-11600]: under a header or at the inset). Rows carry
    /// their own side inset; the cores' bars, rings and graphs stand in the card's inset on the sides and the foot.
    /// [S-14818] Every look's items stand on the card's top edge and run to its sides and foot: the rows, and the columns', the
    /// rings' and the graphs' cells, their padding inside them.
    /// </summary>
    private void ItemsAlone(FrameworkElement body)
    {
        if (_part.Atom == MeterAtom.Items)
        {
            SetRow(body, 0);
            SetRowSpan(body, 2);
            body.Margin = ListEdge.Margin;
        }
    }

    /// <summary>[S-14818] A core's column: its bar from the inset under the card's top edge to the inset over its foot.</summary>
    private static Thickness ColumnPadding => new(0, FamilySpace.Inset, 0, FamilySpace.Inset);

    /// <summary>
    /// [S-14818] Keep one part per item, each in its item's cell (<see cref="ItemMark"/>: the box the checks judge), the
    /// part centred in it with <paramref name="padding"/> round it; the existing ones kept.
    /// </summary>
    private static void ResizeCells<T>(List<T> parts, int count, Panel panel, Func<T> make, Thickness padding)
        where T : FrameworkElement
    {
        while (parts.Count > count)
        {
            panel.Children.Remove(Cell(parts[^1]));
            parts.RemoveAt(parts.Count - 1);
        }

        while (parts.Count < count)
        {
            var part = make();
            part.HorizontalAlignment = HorizontalAlignment.Center;
            part.VerticalAlignment = part is ChartBar ? VerticalAlignment.Stretch : VerticalAlignment.Center;
            var cell = new Border { Child = part, Padding = padding, Background = Family.Brush(ColourRole.Clear) };
            ItemMark.Mark(cell);
            parts.Add(part);
            panel.Children.Add(cell);
        }
    }

    /// <summary>The item cell a part stands in.</summary>
    private static FrameworkElement Cell(FrameworkElement part) => part.Parent as FrameworkElement ?? part;

    /// <summary>
    /// [S-14818] A grid of ring or graph cells, row by row from the top left, the plan's columns and rows sharing the card
    /// from its left edge to its right and its top edge to its foot, touching (every core is shown: the cells are the
    /// card's, as the month's days are); none when the plan shows none.
    /// </summary>
    private void LayGrid(Grid grid, MeterGrid plan, IReadOnlyList<FrameworkElement> cells)
    {
        grid.Visibility = plan.Shown ? Visibility.Visible : Visibility.Collapsed;
        if (!plan.Shown)
        {
            return;
        }

        var lattice = _area.Grid(XamlRoot?.RasterizationScale ?? 1);
        var widths = ItemSize.Columns(_area.Width, plan.Columns, lattice);
        var heights = ItemSize.Columns(_area.Height + FamilySpace.Inset, plan.Rows, lattice);
        grid.ColumnDefinitions.Clear();
        grid.RowDefinitions.Clear();
        foreach (var w in widths)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(w) });
        }

        foreach (var h in heights)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(h) });
        }

        for (var i = 0; i < cells.Count; i++)
        {
            SetColumn(cells[i], i % plan.Columns);
            SetRow(cells[i], i / plan.Columns);
        }
    }

    /// <summary>No look yet (before the first config): the reading in its row, nothing beside it.</summary>
    private sealed class Bare : IMeterLook
    {
        public void Build()
        {
        }

        public void Release()
        {
        }
    }

    /// <summary>
    /// [S-6302] [S-6266] The gauge or the ring: the dial and the reading in one box, the reading inside it. [S-14324] The dial
    /// grows until one axis stops it and stands centred on the other (owner, 2026-11-20, of a reading that fills one axis:
    /// "Centred is fine"): across in the content's middle, down by the block rule (<see cref="Block"/>).
    /// </summary>
    private sealed class Dial(MeterView view, bool half) : IMeterLook
    {
        private readonly ChartRing _dial = new(half: half);
        private readonly Grid _box = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = Family.SideInset };

        public void Build()
        {
            SetRowSpan(_box, 2);
            view.Move(view._reading, _box);
            view._reading.HorizontalAlignment = HorizontalAlignment.Center;
            view._reading.VerticalAlignment = half ? VerticalAlignment.Bottom : VerticalAlignment.Center;
            view._reading.Margin = default;
            _box.Children.Insert(0, _dial);
            view.Children.Add(_box);
        }

        public void Show(MeterReading reading, ColourRole role)
        {
            // [S-6302] The reading's own share, in the colour the history would have; [S-1941] the middle for a thing not here.
            _dial.Role(role);
            _dial.Show(Unavailable.Fraction(reading) ?? 0);
        }

        /// <summary>[S-11003] The gauge or the ring as large as the content box takes, the reading at 28 in it.</summary>
        public void Lay(double width, double height)
        {
            var diameter = half ? MeterSizing.GaugeIn(width, height) : MeterSizing.RingIn(width, height);
            _dial.Visibility = Visibility.Visible;
            _box.Width = diameter;
            _box.Height = half ? ChartRules.GaugeHeight(diameter) : diameter;
            view._reading.HorizontalAlignment = HorizontalAlignment.Center;
            view._reading.VerticalAlignment = half ? VerticalAlignment.Bottom : VerticalAlignment.Center;
        }

        public double Block(double width, double height)
        {
            var diameter = half ? MeterSizing.GaugeIn(width, height) : MeterSizing.RingIn(width, height);
            return half ? ChartRules.GaugeHeight(diameter) : diameter;
        }

        public void Release()
        {
            view.Children.Remove(_box);
            _box.Children.Remove(view._reading);
        }
    }

    /// <summary>
    /// [S-12814] [S-11001] The reading alone, scaled up as one to fill the content (its first ink on the top): the largest
    /// size its width and height take (the fit keeps the sides' and the foot's insets).
    /// </summary>
    private sealed class Number(MeterView view) : IMeterLook
    {
        public ReadingFit Fit { get; } = new();

        public FaceParts? Face => MeterSizing.Face(view.Worst);

        public void Build()
        {
            SetRowSpan(Fit, 2);
            view.Children.Remove(view._reading);
            view._reading.Margin = default;
            Fit.Parts.Add(view._reading);
            view.Children.Add(Fit);
        }

        public void Release()
        {
            view.Children.Remove(Fit);
            Fit.Parts.Remove(view._reading);
        }
    }

    /// <summary>
    /// [S-6264] [S-14241] Download then upload on the one line, NumbersGap apart, both at 28: one block in the fit, as the
    /// Number look's one is (WR-7, owner 2026-11-19: "All together, scaled"), scaled up as one until their ink fills the
    /// height or the width stops them.
    /// </summary>
    private sealed class Numbers(MeterView view) : IMeterLook
    {
        private readonly ReadoutBlock _second = new() { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        private readonly Grid _box = new() { ColumnSpacing = MeterSizing.NumbersGap, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };

        public ReadingFit Fit { get; } = new();

        public void Build()
        {
            view.Move(view._reading, _box);
            view._reading.Margin = default;
            _box.Children.Add(_second);
            SetRowSpan(Fit, 2);
            Fit.Parts.Add(_box);
            view.Children.Add(Fit);
        }

        public void Show(MeterReading reading, ColourRole role)
        {
            var up = reading.Available && reading.Second is { } second && double.IsFinite(second) ? Formatting.Rate(second) : "--";
            ShowReading(_second, up, reading.Available, Glyphs.Upload, role);
        }

        public void Lay(double width, double height)
        {
            _box.RowDefinitions.Clear();
            _box.ColumnDefinitions.Clear();

            // The download's column holds its widest, so the upload never moves as the figures change.
            _box.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(MeterSizing.ReadingWidth(view.Worst)) });
            _box.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            SetColumn(view._reading, 0);
            SetColumn(_second, 1);
            SetRow(_second, 0);
            _second.Use(FamilyType.Big28);
        }

        public void Release()
        {
            view.Children.Remove(Fit);
            _box.Children.Remove(view._reading);
            SetColumn(view._reading, 0);
        }
    }

    /// <summary>[S-6303] [S-1113] [S-14818] A vertical bar per item, the first core on the left in every language, each core's column an item sharing the card's width from its left edge to its right, touching, its bar standing in its middle.</summary>
    private sealed class Columns(MeterView view) : IMeterLook
    {
        private readonly Grid _body = new() { FlowDirection = FlowDirection.LeftToRight };
        private readonly List<ChartBar> _columns = [];

        public bool WantsItems => true;

        public void Build() => view.Body(_body);

        public void ShowItems(IReadOnlyList<MeterItem> items, bool point)
        {
            if (items.Count != _columns.Count)
            {
                ResizeCells(_columns, items.Count, _body, () => new ChartBar(vertical: true), ColumnPadding);
                LayColumns();
            }

            for (var i = 0; i < items.Count && i < _columns.Count; i++)
            {
                var known = items[i].Fraction is { } f && double.IsFinite(f);
                _columns[i].Role(known ? MeterPlan.Role(items[i].Pressure, absent: false) : ColourRole.TextDisabled);
                _columns[i].Show(items[i].Fraction ?? 0);
            }
        }

        public void Lay(double width, double height)
        {
            view.ItemsAlone(_body);
            _body.Visibility = Visibility.Visible;
            LayColumns();
        }

        /// <summary>[S-14818] The columns share the card's width from its left edge to its right, whole device pixels each.</summary>
        private void LayColumns()
        {
            var widths = ItemSize.Columns(view._area.Width, _columns.Count, view._area.Grid(view.XamlRoot?.RasterizationScale ?? 1));
            _body.ColumnDefinitions.Clear();
            for (var i = 0; i < _columns.Count; i++)
            {
                _body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(widths[i]) });
                SetColumn(Cell(_columns[i]), i);
            }
        }

        public void Release() => view.Children.Remove(_body);
    }

    /// <summary>
    /// [S-6304] [S-12807] [S-14810] Rows of items under the reading at their one size, the last the card's foot reaches cut by
    /// it. [S-14324] The reading's first ink stands on the content's top (WR-1: its figures, not its line's leading) and the
    /// first row right under its line (WR-2: no space over the first item).
    /// </summary>
    private sealed class Rows(MeterView view) : IMeterLook
    {
        private readonly SpreadStack _body = new();
        private readonly List<RowBlock> _rows = [];

        public bool WantsItems => true;

        public void Build() => view.Body(_body);

        public void ShowItems(IReadOnlyList<MeterItem> items, bool point)
        {
            while (_rows.Count > items.Count)
            {
                _body.Children.Remove(_rows[^1]);
                _rows.RemoveAt(_rows.Count - 1);
            }

            while (_rows.Count < items.Count)
            {
                var row = new RowBlock(RowKind.Fact, lead: 0);
                var made = new ChartBar();
                row.Meter(made, double.NaN);
                row.Tag = made;
                _rows.Add(row);
                _body.Children.Add(row);
            }

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var known = item.Fraction is { } f && double.IsFinite(f);
                _rows[i].Show(item.Label ?? string.Empty, item.Value);
                _rows[i].Available = known;
                if (_rows[i].Tag is ChartBar bar)
                {
                    bar.Role(known ? MeterPlan.Role(item.Pressure, absent: false) : ColourRole.TextDisabled);
                    bar.Show(item.Fraction ?? 0);
                }
            }
        }

        public void Lay(double width, double height)
        {
            view.ItemsAlone(_body);
            Stand();
            foreach (var row in _rows)
            {
                row.Visibility = Visibility.Visible;
                row.NameText.MaxWidth = width / 2;
            }
        }

        /// <summary>A reading shown: its ink may stand higher or lower (a placeholder's dashes), so it is stood again.</summary>
        public void Show(MeterReading reading, ColourRole role) => Stand();

        /// <summary>[S-14324] The reading's first ink, as drawn, on the content's top.</summary>
        private void Stand()
        {
            var margin = Family.SideInset with { Top = -view._reading.Ink.Over };
            if (view._reading.Margin != margin)
            {
                view._reading.Margin = margin;
            }
        }

        public void Release() => view.Children.Remove(_body);
    }

    /// <summary>[S-6305] [S-14818] A ring per item, the cores' cells sharing the card from edge to edge, touching, the ring in the middle of each.</summary>
    private sealed class Tiles(MeterView view) : IMeterLook
    {
        private readonly Grid _body = new() { FlowDirection = FlowDirection.LeftToRight };
        private readonly List<ChartRing> _rings = [];

        public bool WantsItems => true;

        public void Build() => view.Body(_body);

        public void ShowItems(IReadOnlyList<MeterItem> items, bool point)
        {
            if (items.Count != _rings.Count)
            {
                ResizeCells(_rings, items.Count, _body, () => new ChartRing(), default);
            }

            for (var i = 0; i < items.Count && i < _rings.Count; i++)
            {
                var known = items[i].Fraction is { } f && double.IsFinite(f);
                _rings[i].Role(known ? MeterPlan.Role(items[i].Pressure, absent: false) : ColourRole.TextDisabled);
                _rings[i].Show(items[i].Fraction ?? 0);
            }
        }

        public void Lay(double width, double height)
        {
            view.ItemsAlone(_body);

            // The rings at the plan's size, each in the middle of its cell, the cell's room round it its padding.
            var rings = MeterPlan.Rings(width, height, _rings.Count);
            view.LayGrid(_body, rings, [.. _rings.Select(r => Cell(r))]);
            foreach (var ring in _rings)
            {
                ring.Width = rings.CellWidth;
                ring.Height = rings.CellHeight;
            }
        }

        public void Release() => view.Children.Remove(_body);
    }

    /// <summary>[S-6317] [S-14818] A cell per item, each the item's own history and figure, the cells sharing the card from edge to edge.</summary>
    private sealed class Graphs(MeterView view) : IMeterLook
    {
        private readonly Grid _body = new() { FlowDirection = FlowDirection.LeftToRight };

        public bool WantsItems => true;

        /// <summary>The cores' cells, first to last.</summary>
        public List<GraphCell> Cells { get; } = [];

        public void Build() => view.Body(_body);

        public void ShowItems(IReadOnlyList<MeterItem> items, bool point)
        {
            if (items.Count != Cells.Count)
            {
                while (Cells.Count > items.Count)
                {
                    _body.Children.Remove(Cells[^1].Item);
                    Cells.RemoveAt(Cells.Count - 1);
                }

                while (Cells.Count < items.Count)
                {
                    var cell = new GraphCell(view._band.Capacity);
                    if (view._config is IChartLooks looks)
                    {
                        cell.Band.Looks(ChartLooks.Effective(looks), ChartDetail.Simple, TimeSpan.FromSeconds(1), null);
                    }

                    Cells.Add(cell);
                    _body.Children.Add(cell.Item);
                }

                // New cells start from the cores' history, which already holds this read.
                Load();
                point = false;
            }

            for (var i = 0; i < items.Count && i < Cells.Count; i++)
            {
                Cells[i].Show(items[i].Fraction, point, MeterPlan.Role(items[i].Pressure, absent: false));
            }
        }

        /// <summary>[S-6318] Every core's history as the hub keeps it, onto its cell.</summary>
        public void Load()
        {
            if (view._items is not { } items)
            {
                return;
            }

            for (var i = 0; i < Cells.Count; i++)
            {
                Cells[i].Band.Load(items.History(i, Cells[i].Band.Holds), items.Added);
            }
        }

        public void Lay(double width, double height)
        {
            view.ItemsAlone(_body);
            view.LayGrid(_body, MeterPlan.Graphs(width, height, Cells.Count), [.. Cells.Select(c => (FrameworkElement)c.Item)]);
            foreach (var cell in Cells)
            {
                cell.Overlay();
            }
        }

        public void Release() => view.Children.Remove(_body);
    }

    /// <summary>
    /// [S-6262] [S-14324] A reading over its picture (the battery, the signal bars), one block (WR-7, owner 2026-11-19: "All
    /// together, scaled"): the picture drawn at its natural size under the reading, 8 between them, and the two scaled up as
    /// one until their ink fills the content's height or the width stops them, standing centred on the other axis (owner,
    /// 2026-11-20: "Centred is fine"): the one block layout (<see cref="ReadingFit"/>), never a centring of the look's own.
    /// </summary>
    private abstract class Pictured : IMeterLook
    {
        protected Pictured(MeterView view, FrameworkElement picture)
        {
            View = view;
            Picture = picture;
            Picture.Margin = new Thickness(0, FamilySpace.S2, 0, 0);
        }

        protected MeterView View { get; }

        protected FrameworkElement Picture { get; }

        public ReadingFit Fit { get; } = new();

        public void Build()
        {
            SetRowSpan(Fit, 2);
            View.Children.Remove(View._reading);
            View._reading.Margin = default;
            Fit.Parts.Add(View._reading);
            Fit.Parts.Add(Picture);
            View.Children.Add(Fit);
        }

        public abstract void Show(MeterReading reading, ColourRole role);

        public void Release()
        {
            View.Children.Remove(Fit);
            Fit.Parts.Remove(View._reading);
            Fit.Parts.Remove(Picture);
        }
    }

    /// <summary>[S-14324] A picture in a block (the battery, the signal bars): it inks its whole box, its outline and its bars.</summary>
    private sealed partial class PictureBox : Grid, IInked
    {
        public LineInk Ink => LineInk.None;

        public bool IsRow => false;
    }

    /// <summary>[S-6262] The battery: its body (an outline of 2 in the secondary role, the one radius), the fill 4 inside the outline, the bolt, the cap.</summary>
    private sealed class Battery : Pictured
    {
        private readonly Border _fill;
        private readonly FontIcon _bolt;
        private readonly Grid _bodyBox;
        private readonly Border _cap;
        private readonly BatteryPicture _picture = MeterPlan.Picture(MeterSizing.BodyNatural);
        private double _charge;

        public Battery(MeterView view)
            : this(view, new Border { HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(FamilySpace.S1) }, Family.Glyph(Glyphs.ChargingBolt, FamilyShape.Icon, ColourRole.TextPrimary), new Border { Width = MeterPlan.Cap, VerticalAlignment = VerticalAlignment.Center })
        {
        }

        private Battery(MeterView view, Border fill, FontIcon bolt, Border cap)
            : base(view, Body(fill, bolt, cap, out var box))
        {
            (_fill, _bolt, _cap, _bodyBox) = (fill, bolt, cap, box);

            // The picture at its natural size: the block scales it with the reading.
            _bodyBox.Width = _picture.BodyWidth;
            _bodyBox.Height = _picture.Height;
            _cap.Height = _picture.CapHeight;
            _cap.CornerRadius = Family.Round(MeterPlan.Cap, _picture.CapHeight);
            if (_picture.Bolt > 0)
            {
                _bolt.FontSize = _picture.Bolt;
                _bolt.Width = _picture.Bolt;
                _bolt.Height = _picture.Bolt;
            }
        }

        /// <summary>[S-1113] A battery fills from the left in every language, as Windows draws it: its body, then its cap.</summary>
        private static PictureBox Body(Border fill, FontIcon bolt, Border cap, out Grid box)
        {
            bolt.HorizontalAlignment = HorizontalAlignment.Center;
            bolt.Visibility = Visibility.Collapsed;
            box = new Grid
            {
                BorderThickness = new Thickness(FamilyShape.Stroke),
                CornerRadius = Family.Radius,
                Children = { fill, bolt },
            };
            SetColumn(cap, 1);
            return new PictureBox
            {
                FlowDirection = FlowDirection.LeftToRight,
                ColumnDefinitions = { new ColumnDefinition { Width = GridLength.Auto }, new ColumnDefinition { Width = GridLength.Auto } },
                Children = { box, cap },
            };
        }

        /// <summary>[S-6262] The charge at once in its role; no reading is an empty battery, no battery at all stands at the middle ([S-1941]).</summary>
        public override void Show(MeterReading reading, ColourRole role)
        {
            var outline = Family.Brush(reading.Absent ? ColourRole.TextDisabled : ColourRole.TextSecondary);
            _bodyBox.BorderBrush = outline;
            _cap.Background = outline;
            _fill.Background = Family.Brush(role);
            _bolt.Visibility = reading.Available && reading.Charging && _picture.Bolt > 0 ? Visibility.Visible : Visibility.Collapsed;

            var charge = Unavailable.Fraction(reading) ?? 0;
            if (!charge.Equals(_charge) || _fill.Width is not > 0 && charge > 0)
            {
                _charge = charge;
                _fill.Width = _picture.FillWidth(charge);
            }
        }
    }

    /// <summary>[S-6263] Four bars rising left to right, 4 apart, standing on one line, lit by the reading's latency.</summary>
    private sealed class Signal : Pictured
    {
        private readonly List<Border> _bars;
        private int _signal;
        private ColourRole _role = ColourRole.Accent;

        public Signal(MeterView view)
            : this(view, [])
        {
        }

        private Signal(MeterView view, List<Border> bars)
            : base(view, Bars(bars))
        {
            _bars = bars;
        }

        /// <summary>The bars at their natural size: the block scales them with the reading.</summary>
        private static PictureBox Bars(List<Border> bars)
        {
            var grid = new PictureBox { FlowDirection = FlowDirection.LeftToRight, ColumnSpacing = FamilySpace.S1 };
            var sizes = MeterPlan.SignalBars(double.PositiveInfinity, MeterSizing.SignalNatural);
            for (var i = 0; i < PingSignal.BarCount && i < sizes.Count; i++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var bar = new Border
                {
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Background = Family.Brush(ColourRole.Track),
                    Width = sizes[i].Width,
                    Height = sizes[i].Height,
                    CornerRadius = sizes[i].Width >= 2 * FamilyShape.Radius ? Family.Radius : Family.Round(sizes[i].Width, sizes[i].Height),
                };
                SetColumn(bar, i);
                grid.Children.Add(bar);
                bars.Add(bar);
            }

            grid.Height = sizes.Count > 0 ? sizes[^1].Height : 0;
            return grid;
        }

        /// <summary>[S-6263] The bars the latency earns in the status role, the rest the track.</summary>
        public override void Show(MeterReading reading, ColourRole role)
        {
            _signal = PingSignal.Level(reading.Available ? reading.Value : null);
            _role = role;
            for (var i = 0; i < _bars.Count; i++)
            {
                _bars[i].Background = Family.Brush(i < _signal ? _role : ColourRole.Track);
            }
        }
    }

    /// <summary>
    /// [S-6317] One core's graph: its load in Caption (primary) over its history, 4 under it, the history
    /// in the load's role, stepping as the main graph does ([S-6331]).
    /// </summary>
    private sealed class GraphCell
    {
        private readonly TextBlock _figure = Family.Text(FamilyType.Caption);
        private string _shown = string.Empty;

        public GraphCell(int capacity)
        {
            Band = new SparkBand(capacity, ChartScale.Share) { Captions = false };
            SetRow(Band, 1);
            Root = new Grid
            {
                RowSpacing = FamilySpace.S1,
                RowDefinitions =
                {
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                },
                Children = { _figure, Band },
            };

            // [S-14818] The core's item: its cell, the graph filling it ([S-14324]: the plot edge to edge, owner 2026-11-19, "Plot
            // edge to edge"; its figure stands inside it, over the plot).
            Item = new Border { Child = Root, Background = Family.Brush(ColourRole.Clear) };
            ItemMark.Mark(Item);
        }

        public Grid Root { get; }

        /// <summary>[S-14818] The cell the graph stands in: the item the checks judge, its share of the card.</summary>
        public Border Item { get; }

        public SparkBand Band { get; }

        /// <summary>[S-11005] The figure over the graph's top corner, the graph the whole cell: one face at every cell size.</summary>
        public void Overlay()
        {
            if (Root.Children[^1] != _figure)
            {
                Root.Children.Remove(_figure);
                Root.Children.Add(_figure);
                SetRow(Band, 0);
                SetRowSpan(Band, 2);
                Root.RowSpacing = 0;
                _figure.Margin = new Thickness(FamilySpace.S2, FamilySpace.S1, 0, 0);
            }

            _figure.Visibility = Visibility.Visible;
        }

        /// <summary>One read of a core: a point on its history when <paramref name="point"/>, its figure and its role.</summary>
        public void Show(double? load, bool point, ColourRole role)
        {
            var known = load is { } l && double.IsFinite(l);
            if (point && known)
            {
                Band.Push(load!.Value);
            }

            Band.Role(known ? role : ColourRole.TextDisabled);

            // [S-6330] A whole percent, at once, as the reading.
            var text = known ? Formatting.Percent(Math.Clamp(load!.Value, 0, 1)) : "--";
            if (text != _shown)
            {
                _shown = text;
                _figure.Text = text;
                _figure.Foreground = Family.Brush(known ? ColourRole.TextPrimary : ColourRole.TextDisabled);
            }
        }
    }
}
