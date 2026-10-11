using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Interop;
using Themia.App.Services;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Settings;
using Themia.Core.Storage;
using Themia.Core.Widgets;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-1832] The Displays page: one group per connected display, switched off or not ([S-853]),
/// each with whether Themia is shown there, which view it shows, how much it magnifies widgets,
/// and its view bar ([S-916]). Its resolution is the group's subtitle, not a row of its own ([S-1709]).
/// </summary>
internal static partial class ConfigPages
{
    private static FrameworkElement BuildDisplays(PageContext context)
    {
        var host = context.Host;
        var displays = host.Displays;
        context.Expansion.Retain(SettingsList.Displays, displays.Select(d => d.Id));

        if (displays.Count == 0)
        {
            return Page(Str.App_Displays.Text(), [Note(Str.Shell_Displays_None.Text())]);
        }

        var rows = new List<FrameworkElement>();
        foreach (var display in displays)
        {
            var id = display.Id;
            var size = Str.Shell_Display_Size.Format(("width", LocaleFormat.Integer(display.Bounds.Width)), ("height", LocaleFormat.Integer(display.Bounds.Height)));

            rows.Add(FluentLayout.Expander(
                Glyphs.Display,
                display.ShortName,
                host.State.DisabledMonitors.Contains(id) ? Str.Shell_Displays_Off.Format(("size", size)) : size,
                null,
                DisplayRows(host, display),
                expanded: context.Expansion.IsOpen(SettingsList.Displays, id, displays.Count),
                item: id,
                expandedChanged: open => context.Expansion.Set(SettingsList.Displays, id, open)));
        }

        return Page(Str.App_Displays.Text(), rows);
    }

    /// <summary>[S-853] One display's rows: Show Themia here, Shows, Widget scale, then its view bar.</summary>
    private static List<Border> DisplayRows(AppHost host, MonitorTarget display)
    {
        var monitorId = display.Id;
        var state = host.State;
        var enabled = !state.DisabledMonitors.Contains(monitorId);
        var connected = host.Displays.Select(d => d.Info).ToList();

        // [S-845] The last display Themia is on cannot be switched off: with no surface there is no
        // edit mode and no way back here.
        var canToggle = !enabled || DisplayPlan.CanDisable(connected, state.DisabledMonitors, monitorId);
        ToggleSwitch? show = null;
        var reverting = false;
        show = FluentLayout.Toggle(enabled, on =>
        {
            // Refused (logged by the host): put the switch back rather than show a state that isn't so.
            if (!reverting && !host.SetDisplayEnabled(monitorId, on) && show is not null)
            {
                reverting = true;
                show.IsOn = !on;
                reverting = false;
            }
        });
        show.IsEnabled = canToggle;

        var rows = new List<Border>
        {
            FluentLayout.Row(Glyphs.Display, Str.Shell_Display_Show.Text(),
                canToggle
                    ? Str.Shell_Display_ShowDesc.Text()
                    : Str.Shell_Display_ShowLast.Text(),
                show, "enable", "disable", "hide", "monitor"),

            FluentLayout.Row(Glyphs.View, Str.Shell_Display_Shows.Text(), Str.Shell_Display_ShowsDesc.Text(), ViewChoice(host, monitorId), "view"),

            FluentLayout.Row(Glyphs.Scale, Str.Display_WidgetScale_Label.Text(),
                Str.Shell_Display_ScaleDesc.Text(),
                FluentLayout.Slider(
                    (state.MonitorScales.TryGetValue(monitorId, out var scale) ? scale : 1.0) * 100,
                    AppStateStore.MinMonitorScale * 100,
                    AppStateStore.MaxMonitorScale * 100,
                    5,
                    "%",
                    value => host.SetDisplayScale(monitorId, value / 100)),
                "zoom", "size", "text size", "magnify"),
        };

        rows.AddRange(ViewBarRows(host, monitorId));
        return rows;
    }

    /// <summary>
    /// [S-916] This display's view bar: shown or not, where, how big, names, and which views it
    /// offers. Inside the display's group, so each title says it is the view bar's.
    /// </summary>
    private static List<Border> ViewBarRows(AppHost host, string monitorId)
    {
        var bar = ViewBarRules.For(host.State, monitorId);
        void Change(Action<ViewBarSettings> change) => host.Mutate(s => ViewBarRules.Update(s, monitorId, change));

        var rows = new List<Border>
        {
            FluentLayout.Row(Glyphs.View, Str.Shell_ViewBar_Show.Text(),
                Str.Shell_ViewBar_ShowDesc.Text(),
                FluentLayout.Toggle(!bar.Hidden, on => Change(b => b.Hidden = !on)),
                "views", "switcher", "chips"),

            FluentLayout.Row(Glyphs.Display, Str.Shell_ViewBar_Position.Text(),
                Str.Shell_ViewBar_PositionDesc.Text(),
                FluentLayout.Choice<ViewBarAnchor>(
                [
                    (ViewBarAnchor.TopLeft, Str.Shell_ViewBar_TopLeft.Text()),
                    (ViewBarAnchor.TopCenter, Str.Shell_ViewBar_TopCentre.Text()),
                    (ViewBarAnchor.TopRight, Str.Shell_ViewBar_TopRight.Text()),
                    (ViewBarAnchor.LeftCenter, Str.Shell_ViewBar_LeftVertical.Text()),
                    (ViewBarAnchor.RightCenter, Str.Shell_ViewBar_RightVertical.Text()),
                    (ViewBarAnchor.BottomLeft, Str.Shell_ViewBar_BottomLeft.Text()),
                    (ViewBarAnchor.BottomCenter, Str.Shell_ViewBar_BottomCentre.Text()),
                    (ViewBarAnchor.BottomRight, Str.Shell_ViewBar_BottomRight.Text()),
                ], bar.Anchor, value => Change(b => b.Anchor = value)),
                "anchor", "corner", "vertical"),

            FluentLayout.Row(Glyphs.Scale, Str.Shell_ViewBar_Size.Text(), null,
                FluentLayout.Choice<ViewBarSize>(
                [
                    (ViewBarSize.Medium, Str.Shell_Size_Medium.Text()),
                    (ViewBarSize.Large, Str.Shell_Size_Large.Text()),
                    (ViewBarSize.ExtraLarge, Str.Shell_Size_ExtraLarge.Text()),
                ], bar.Size, value => Change(b => b.Size = value)),
                "bigger", "smaller"),

            FluentLayout.Row(Glyphs.Rename, Str.Shell_ViewBar_ShowNames.Text(),
                Str.Shell_ViewBar_ShowNamesDesc.Text(),
                FluentLayout.Toggle(!bar.IconOnly, on => Change(b => b.IconOnly = !on)),
                "labels", "icons only", "initials"),
        };

        // [S-1708] One drop-down button, however many views there are: a group cannot fold another
        // group into itself, and a column of check boxes on the row grew it a line per view. A
        // change can be refused (the last view cannot be switched off), so the boxes and the
        // button's text are always read back from what was stored.
        var views = FluentLayout.MultiChoice(
            Str.Shell_ViewBar_AllViews.Text(),
            () => [.. host.State.Views.Select(v => new MultiChoiceItem(v.Id, v.Name,
                () => new IconLook(Glyphs.View, ViewListRules.Initials(v.Name)).For(string.IsNullOrEmpty(v.Icon) ? null : v.Icon)))],
            () =>
            {
                var settings = ViewBarRules.For(host.State, monitorId);
                var offer = ViewBarRules.Offer(host.State.Views, settings);
                var picked = ViewBarRules.VisibleViews(host.State.Views, settings).Select(v => v.Id).ToHashSet(StringComparer.Ordinal);
                var summary = offer.All
                    ? Str.Shell_ViewBar_AllViews.Text()
                    : offer.Single ?? Str.Shell_ViewBar_SomeViews.Format(("count", LocaleFormat.Integer(offer.Count)), ("total", LocaleFormat.Integer(offer.Total)));
                return new MultiChoiceState(offer.All, picked, summary);
            },
            on => host.Mutate(s => ViewBarRules.Update(s, monitorId, b => b.VisibleViews = ViewBarRules.SetAllVisible(s.Views, on, ShownViewId(s, monitorId)))),
            (id, on) => host.Mutate(s => ViewBarRules.Update(s, monitorId, b => b.VisibleViews = ViewBarRules.ToggleVisible(s.Views, b, id, on))));

        rows.Add(FluentLayout.Row(Glyphs.View, Str.Shell_ViewBar_Views.Text(), Str.Shell_ViewBar_ViewsDesc.Text(), views,
            [.. host.State.Views.Select(v => v.Name)]));

        return rows;
    }

    /// <summary>
    /// [S-1706] The view drop-down: one of the existing views, nothing else. A display is never
    /// given a view of its own ([S-1705]), and there is no "follow the active view": every display
    /// holds an assignment, so that choice would only last until the next display pass.
    /// </summary>
    private static ComboBox ViewChoice(AppHost host, string monitorId)
    {
        var views = host.State.Views.Select(v => (v.Id, v.Name)).ToList();
        var shown = ShownViewId(host.State, monitorId);

        var combo = new ComboBox { MinWidth = 220 };
        foreach (var (id, name) in views)
        {
            combo.Items.Add(new ComboBoxItem { Content = name, Tag = id });
        }

        combo.SelectedIndex = Math.Max(0, views.FindIndex(v => v.Id == shown));
        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedItem is ComboBoxItem { Tag: string id })
            {
                // [S-19] The one way a display is given a view.
                host.ShowView(monitorId, id);
            }
        };

        return combo;
    }
}
