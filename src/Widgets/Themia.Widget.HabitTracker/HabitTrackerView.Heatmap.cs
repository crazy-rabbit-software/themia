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
/// [S-6409] [S-11532] The heatmap style: the last weeks as a calendar grid, per habit (its name over its
/// own grid, the current period's cell ticking it as its box does; the config's rows of habits) or one
/// grid for the whole list shaded by how many were done, as the config says (<see cref="HabitHeatmap.Plan"/>);
/// a wider card holds more weeks.
/// The cells are rebuilt only when the grid's shape changes; a tick or a rollover recolours them.
/// </summary>
internal sealed partial class HabitTrackerView
{
    private readonly Canvas _heat = new() { Visibility = Visibility.Collapsed };
    private readonly List<Border> _heatCells = [];
    private readonly List<TextBlock> _heatNames = [];
    private (bool PerHabit, int Weeks, double Cell, int Habits) _heatShape;

    private bool IsHeatmap => _config.Style == HabitTrackerStyle.Heatmap;

    /// <summary>The heatmap's canvas joins the rows' cell of the grid; built once with the view.</summary>
    private void BuildHeatmap()
    {
        SetRow(_heat, 0);
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

        // The grids stand in the card's inset, from the view's top to the card's bottom inset.
        var inset = FamilySpace.Inset;
        var nameStep = FamilyType.Body;
        var plan = HabitHeatmap.Plan(_area.Width - (2 * inset), _config.PerHabit);
        var habits = Habits;
        var cadence = _config.Cadence;
        var firstDay = LocaleFormat.FirstDayOfWeek(Strings.Current);
        var periods = HabitHeatmap.Cells(_current, cadence, TimeZoneInfo.Local, plan.Weeks, firstDay);
        var logs = habits.Select(h => HabitRules.Log(h.Log)).ToList();

        var grids = plan.PerHabit ? Math.Min(count, HabitSizing.ClampRows(_config.Rows)) : 1;
        var shape = (plan.PerHabit, plan.Weeks, plan.Cell, grids);
        if (shape != _heatShape)
        {
            BuildHeatCells(plan, grids, nameStep);
            _heatShape = shape;
        }

        // The grids sit at the top left, as the rows do; a grid narrower than the tile leaves the rest empty.
        var gridWidth = (plan.Weeks * plan.Pitch) - HabitHeatmap.Gap;
        var y = 0d;

        for (var g = 0; g < grids; g++)
        {
            if (plan.PerHabit)
            {
                var name = _heatNames[g];
                name.Text = habits[g].Name;
                name.Width = gridWidth;
                Canvas.SetLeft(name, inset);
                Canvas.SetTop(name, y);
                ToolTipService.SetToolTip(name, habits[g].Name);
                y += nameStep.LineHeight + HabitHeatmap.NameGap;
            }

            for (var i = 0; i < periods.Count; i++)
            {
                var cell = _heatCells[(g * periods.Count) + i];
                Canvas.SetLeft(cell, inset + ((i / HabitHeatmap.Rows) * plan.Pitch));
                Canvas.SetTop(cell, y + ((i % HabitHeatmap.Rows) * plan.Pitch));

                if (periods[i] is not { } period)
                {
                    cell.Visibility = Visibility.Collapsed;
                    ForgetDay(cell);
                    continue;
                }

                cell.Visibility = Visibility.Visible;
                MarkDay(cell, period, cadence.Period); // [S-11924]
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

            y += plan.GridHeight + HabitHeatmap.BlockGap;
        }
    }

    /// <summary>
    /// [S-6409] A cell is a column of the Chart block ([S-6717]): the track, filled from its foot in
    /// the accent to <paramref name="fill"/> (a habit's own cell is empty or full; a shared one is the
    /// share of the list done), never an opacity per share.
    /// </summary>
    private static void Shade(Border cell, double fill)
    {
        if (cell.Child is not Border level)
        {
            return;
        }

        // A full cell is the accent's whole: its top corners round as the cell's are. A low level is
        // round at its own height, never more ([S-6704]: 4 or round).
        level.Height = cell.Height * fill;
        var radius = Math.Min(cell.CornerRadius.BottomLeft, level.Height / 2);
        level.CornerRadius = fill >= 1 ? new CornerRadius(radius) : new CornerRadius(0, 0, radius, radius);
        level.Visibility = fill > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BuildHeatCells(HabitHeatmapPlan plan, int grids, TypeStep nameStep)
    {
        ClearHeatmap();

        for (var g = 0; g < grids; g++)
        {
            if (plan.PerHabit)
            {
                var name = Family.Text(nameStep, ColourRole.TextPrimary);
                name.TextTrimming = TextTrimming.CharacterEllipsis;
                name.TextWrapping = TextWrapping.NoWrap;
                _heatNames.Add(name);
                _heat.Children.Add(name);
            }

            for (var i = 0; i < plan.Weeks * HabitHeatmap.Rows; i++)
            {
                var radius = HabitHeatmap.Radius(plan.Cell);
                var cell = new Border
                {
                    Width = plan.Cell,
                    Height = plan.Cell,
                    CornerRadius = new CornerRadius(radius),
                    Background = Family.Brush(ColourRole.Track),

                    // The level fills from the foot, its lower corners the cell's.
                    Child = new Border
                    {
                        VerticalAlignment = VerticalAlignment.Bottom,
                        Background = Family.Brush(ColourRole.Accent),
                        CornerRadius = new CornerRadius(0, 0, radius, radius),
                    },
                };

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
