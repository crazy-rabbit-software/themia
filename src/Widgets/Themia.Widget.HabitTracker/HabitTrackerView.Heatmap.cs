using Microsoft.UI;
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
using Themia.Sdk.WinUI;
using Themia.Widget.HabitTracker;

namespace Themia.App.Widgets;

/// <summary>
/// [S-6409] The heatmap style: the last weeks as a calendar grid, per habit (its name over its own
/// grid, the current period's cell ticking it as its box does) or one grid for the whole list
/// shaded by how many were done, whichever the tile has room for (<see cref="HabitHeatmap.Plan"/>).
/// The cells are rebuilt only when the grid's shape changes; a tick or a rollover recolours them.
/// </summary>
internal sealed partial class HabitTrackerView
{
    private readonly Canvas _heat = new() { Visibility = Visibility.Collapsed };
    private readonly List<Border> _heatCells = [];
    private readonly List<TextBlock> _heatNames = [];
    private readonly Brush _heatFill = ThemeBrush.Of("AccentFillColorDefaultBrush", Colors.DodgerBlue);
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

        var inset = _area.WidthClass == SizeClass.Compact ? Fluent.Space2 : Fluent.Space4;
        var box = new WidgetArea(Math.Max(0, _area.Width - (2 * inset)), Math.Max(0, _area.Height - (2 * Fluent.Space2)));
        var nameStep = FluentType.Body;
        var plan = HabitHeatmap.Plan(box, count, nameStep.LineHeight);
        var habits = Habits;
        var cadence = _config.Cadence;
        var firstDay = LocaleFormat.FirstDayOfWeek(Strings.Current);
        var periods = HabitHeatmap.Cells(_current, cadence, TimeZoneInfo.Local, plan.Weeks, firstDay);
        var logs = habits.Select(h => HabitRules.Log(h.Log)).ToList();

        var grids = plan.PerHabit ? count : 1;
        var shape = (plan.PerHabit, plan.Weeks, plan.Cell, grids);
        if (shape != _heatShape)
        {
            BuildHeatCells(plan, grids, nameStep);
            _heatShape = shape;
        }

        // The grids sit at the top left, as the rows do; a grid narrower than the tile leaves the rest empty.
        var gridWidth = (plan.Weeks * plan.Pitch) - HabitHeatmap.Gap;
        var y = Fluent.Space2;

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
                    continue;
                }

                cell.Visibility = Visibility.Visible;
                var when = HabitPeriods.Describe(period, cadence);

                if (plan.PerHabit)
                {
                    var done = HabitRules.IsDone(logs[g], period);
                    Shade(cell, done ? HabitHeatmap.Levels : 0);
                    var label = (done ? HabitTrackerStr.WidgetsB_Habits_DotDone : HabitTrackerStr.WidgetsB_Habits_DotNotDone).Format(("period", when));
                    ToolTipService.SetToolTip(cell, label);
                    AutomationProperties.SetName(cell, label);

                    // The current period's cell is the habit's box: a tap ticks it ([S-1421]).
                    cell.Tag = period.Key == _current.Key && Writable ? habits[g].Id : null;
                }
                else
                {
                    var done = logs.Count(log => HabitRules.IsDone(log, period));
                    Shade(cell, HabitHeatmap.Level(done, count));
                    var label = HabitTrackerStr.Styles_Habits_Cell.Format(("period", when), ("done", done), ("count", count));
                    ToolTipService.SetToolTip(cell, label);
                    AutomationProperties.SetName(cell, label);
                    cell.Tag = null;
                }
            }

            y += plan.GridHeight + HabitHeatmap.BlockGap;
        }
    }

    /// <summary>Accent at the level's share of full strength; none done is the empty slot the dots use.</summary>
    private void Shade(Border cell, int level)
    {
        // [S-6513] An empty slot is the item layer's track.
        if (level == 0)
        {
            ItemFill.Paint(cell, ItemRole.Track);
        }
        else
        {
            cell.Background = _heatFill;
        }

        cell.Opacity = level == 0 ? 1 : (double)level / HabitHeatmap.Levels;
    }

    private void BuildHeatCells(HabitHeatmapPlan plan, int grids, TypeStep nameStep)
    {
        ClearHeatmap();

        for (var g = 0; g < grids; g++)
        {
            if (plan.PerHabit)
            {
                var name = Fluent.Text(nameStep);
                name.TextTrimming = TextTrimming.CharacterEllipsis;
                name.TextWrapping = TextWrapping.NoWrap;
                _heatNames.Add(name);
                _heat.Children.Add(name);
            }

            for (var i = 0; i < plan.Weeks * HabitHeatmap.Rows; i++)
            {
                var cell = new Border
                {
                    Width = plan.Cell,
                    Height = plan.Cell,
                    CornerRadius = new CornerRadius(Math.Min(Fluent.ControlRadius, plan.Cell / 4)),
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
