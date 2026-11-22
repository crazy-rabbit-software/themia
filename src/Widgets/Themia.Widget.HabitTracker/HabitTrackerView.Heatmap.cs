using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Themia.App.Shell;
using Themia.Core.Habits;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.HabitTracker;

namespace Themia.App.Widgets;

/// <summary>
/// [S-6409] [S-11532] [S-15770] The heatmap style: the last weeks as a calendar grid, per habit (its name over its
/// own grid, the current period's cell ticking it as its box does; the config's rows of habits) or one
/// grid for the whole list shaded by how many were done, as the config says (<see cref="HabitHeatmap.Plan"/>).
/// Every cell is a box of one fixed size from the card's top edge, touching, the newest week on its right edge; a wider
/// card holds more weeks, a taller one more habits' grids, never larger cells.
/// The cells are rebuilt only when the grid's shape changes; a tick or a rollover recolours them.
/// </summary>
internal sealed partial class HabitTrackerView
{
    private readonly Canvas _heat = new() { Visibility = Visibility.Collapsed, Margin = ListEdge.Margin };
    private readonly List<Border> _heatCells = [];
    private readonly List<TextBlock> _heatNames = [];
    private (bool PerHabit, int Weeks, double Box, int Habits) _heatShape;

    private bool IsHeatmap => _config.Style == HabitTrackerStyle.Heatmap;

    /// <summary>The heatmap's canvas joins the rows' cell of the grid; built once with the view.</summary>
    private void BuildHeatmap()
    {
        SetRow(_heat, 0);
        SetRowSpan(_heat, 3);
        Children.Add(_heat);
    }

    /// <summary>Show the heatmap (or not) and draw it at the tile's size.</summary>
    private void LayHeatmap(int count)
    {
        var show = IsHeatmap && count > 0 && _area.IsMeasured;
        _heat.Visibility = show ? Visibility.Visible : Visibility.Collapsed;

        if (!show)
        {
            if (_heatCells.Count > 0)
            {
                ClearHeatmap();
            }

            return;
        }

        // [S-15770] The canvas stands on the card's top edge (ListEdge): its room is the card's whole box, clipped there.
        var width = Math.Max(0, _area.Width);
        var height = Math.Max(0, _area.Height + FamilySpace.Inset);
        _heat.Width = width;
        _heat.Height = height;
        _heat.Clip = new RectangleGeometry { Rect = new Windows.Foundation.Rect(0, 0, width, height) };

        var grid = ItemGrids.Of(this);
        var plan = HabitHeatmap.Plan(width, _config.PerHabit, grid);
        var habits = Habits;
        var cadence = _config.Cadence;
        var firstDay = LocaleFormat.FirstDayOfWeek(Strings.Current);
        // [S-15787] The history's whole weeks, then the next week's periods still to come in a column the right edge cuts.
        var periods = (IReadOnlyList<HabitBucket?>)[.. HabitHeatmap.Cells(_current, cadence, TimeZoneInfo.Local, plan.History, firstDay), .. Enumerable.Repeat<HabitBucket?>(null, (plan.Weeks - plan.History) * HabitHeatmap.Rows)];
        var logs = habits.Select(h => HabitRules.Log(h.Log)).ToList();

        // [S-15770] A grid per habit: every habit's block the card reaches, touching, the last cut by its foot.
        var grids = plan.PerHabit ? HabitHeatmap.Blocks(height, count, plan) : 1;
        var shape = (plan.PerHabit, plan.Weeks, plan.Box, grids);
        if (shape != _heatShape)
        {
            BuildHeatCells(plan, grids);
            _heatShape = shape;
        }

        // [S-11532] [S-15770] [S-15787] The weeks from the card's left edge, touching, the last cut by its right edge (WR-3,
        // WR-4); the rows from the top edge down, each box on a whole pixel.
        var scale = grid.Raster;
        var y = 0.0;
        for (var g = 0; g < grids; g++)
        {
            if (plan.PerHabit)
            {
                var name = _heatNames[g];
                name.Text = habits[g].Name;
                name.Width = Math.Max(0, width - (2 * FamilySpace.Inset));
                Canvas.SetLeft(name, FamilySpace.Inset);
                Canvas.SetTop(name, ItemSize.OnPixels(y + ((plan.Name - FamilyType.Body.LineHeight) / 2), scale));
                ToolTipService.SetToolTip(name, habits[g].Name);
                y += plan.Name;
            }

            for (var i = 0; i < periods.Count; i++)
            {
                var cell = _heatCells[(g * periods.Count) + i];
                Canvas.SetLeft(cell, ItemSize.OnPixels((i / HabitHeatmap.Rows) * plan.Box, scale));
                Canvas.SetTop(cell, ItemSize.OnPixels(y + ((i % HabitHeatmap.Rows) * plan.Box), scale));

                cell.Visibility = Visibility.Visible;
                if (periods[i] is not { } period)
                {
                    // [S-15787] A period still to come: an empty track, saying nothing and ticking nothing.
                    Shade(cell, 0);
                    ToolTipService.SetToolTip(cell, null);
                    AutomationProperties.SetName(cell, string.Empty);
                    cell.Tag = null;
                    ForgetDay(cell);
                    continue;
                }

                MarkDay(cell, period, cadence.Period, plan.PerHabit); // [S-11924]
                var when = HabitPeriods.Describe(period, cadence);

                if (plan.PerHabit)
                {
                    var done = HabitRules.IsDone(logs[g], period);
                    Shade(cell, done ? 1 : 0);
                    var label = (done ? HabitTrackerStr.WidgetsB_Habits_DotDone : HabitTrackerStr.WidgetsB_Habits_DotNotDone).Format(("period", when));
                    ToolTipService.SetToolTip(cell, label);
                    AutomationProperties.SetName(cell, label);

                    // The current period's cell is the habit's box: a tap ticks it ([S-1421]).
                    cell.Tag = period.Key == _current.Key && Writable ? habits[g].Id : null;
                }
                else
                {
                    var done = logs.Count(log => HabitRules.IsDone(log, period));
                    Shade(cell, HabitHeatmap.Fill(done, count));
                    var label = HabitTrackerStr.Styles_Habits_Cell.Format(("period", when), ("done", done), ("count", count));
                    ToolTipService.SetToolTip(cell, label);
                    AutomationProperties.SetName(cell, label);
                    cell.Tag = null;
                }
            }

            y += plan.GridHeight;
        }
    }

    /// <summary>
    /// [S-6409] A cell's mark is a column of the Chart block ([S-6717]): the track, filled from its foot in
    /// the accent to <paramref name="fill"/> (a habit's own cell is empty or full; a shared one is the
    /// share of the list done), never an opacity per share. [S-15770] The mark stands in its box's padding.
    /// </summary>
    private static void Shade(Border cell, double fill)
    {
        if (cell.Child is not Border { Child: Border level } mark)
        {
            return;
        }

        // A full mark is the accent's whole: its top corners round as the mark's are. A low level is
        // round at its own height, never more ([S-6704]: 4 or round).
        level.Height = mark.Height * fill;
        var radius = Math.Min(mark.CornerRadius.BottomLeft, level.Height / 2);
        level.CornerRadius = fill >= 1 ? new CornerRadius(radius) : new CornerRadius(0, 0, radius, radius);
        level.Visibility = fill > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BuildHeatCells(HabitHeatmapPlan plan, int grids)
    {
        ClearHeatmap();

        for (var g = 0; g < grids; g++)
        {
            if (plan.PerHabit)
            {
                var name = Family.Text(FamilyType.Body, ColourRole.TextPrimary);
                name.TextTrimming = TextTrimming.CharacterEllipsis;
                name.TextWrapping = TextWrapping.NoWrap;
                _heatNames.Add(name);
                _heat.Children.Add(name);
            }

            for (var i = 0; i < plan.Weeks * HabitHeatmap.Rows; i++)
            {
                var radius = HabitHeatmap.Radius(plan.Cell);

                // [S-15770] The box is the item (the pointer's, touching the next); the mark stands in its padding.
                var cell = new Border
                {
                    Width = plan.Box,
                    Height = plan.Box,
                    Padding = new Thickness(plan.Pad),
                    Background = Family.Brush(ColourRole.Clear),
                    Child = new Border
                    {
                        Width = plan.Cell,
                        Height = plan.Cell,
                        CornerRadius = new CornerRadius(radius),
                        Background = Family.Brush(ColourRole.Track),

                        // The level fills from the foot, its lower corners the mark's.
                        Child = new Border
                        {
                            VerticalAlignment = VerticalAlignment.Bottom,
                            Background = Family.Brush(ColourRole.Accent),
                            CornerRadius = new CornerRadius(0, 0, radius, radius),
                        },
                    },
                };

                ItemMark.Mark(cell);
                cell.Tapped += OnHeatCellTapped;
                _heatCells.Add(cell);
                _heat.Children.Add(cell);
            }
        }
    }

    private void ClearHeatmap()
    {
        foreach (var cell in _heatCells)
        {
            cell.Tapped -= OnHeatCellTapped;
            ForgetDay(cell);
        }

        _heatCells.Clear();
        _heatNames.Clear();
        _heat.Children.Clear();
        _heatShape = default;
    }

    /// <summary>[S-6409] A tap on a habit's current-period cell ticks it, as its box would; every other cell only says what it was.</summary>
    private void OnHeatCellTapped(object sender, TappedRoutedEventArgs args)
    {
        if (sender is Border { Tag: string habitId })
        {
            args.Handled = true;
            Toggle(habitId);
        }
    }
}
