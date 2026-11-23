#if DEBUG
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Widgets;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Settings;
using Themia.Core.Shell;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Windows.Foundation;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// The edit-mode parts a render composes on a stand-in display, shared by the edit bar's pictures
/// ([S-2328]) and a store scene's ([S-1077]): the real bar, the add menu hung under Add widget, and a
/// page's flyout hung from its entry (<see cref="FlyoutPlacement"/>).
/// </summary>
internal static partial class EditBarRender
{
    /// <summary>[S-2341] The bar's selection entry for a selection: the kind and its name, "2 × Clock", "3 widgets"; none for none.</summary>
    internal static BarSelection? SelectionEntry(IReadOnlyList<WidgetInstance> selected)
    {
        if (selected.Count == 0)
        {
            return null;
        }

        var view = new ViewState { Id = "render", Name = "render", Widgets = [.. selected] };
        var selection = new WidgetSelection();
        selection.Replace(view.Id, [.. selected.Select(w => w.Id)]);
        return SelectionEntry(view, selection);
    }

    /// <summary>[S-2341] [S-11754] The bar's selection entry for <paramref name="selection"/> on <paramref name="view"/>.</summary>
    internal static BarSelection? SelectionEntry(ViewState view, WidgetSelection selection)
    {
        var model = SelectionPage.Of(new AppState { Views = [view] }, selection, view.Id);
        return model is not null && SelectionPage.Words(model, id => ModuleCatalog.Of(id).Name) is { } words
            ? new BarSelection(words, SelectionPage.Glyph(model))
            : null;
    }

    /// <summary>[S-2320] The bar as a render shows it: <paramref name="selection"/>'s entry, its flyout open or not.</summary>
    internal static EditBar SelectionBar(BarSelection? selection, bool open = false)
        => Bar(new EditBarState(selection, 0, null, open && selection is not null, ScreenWidth - (2 * ShellMetricsBarTop)));

    private const double ShellMetricsBarTop = Themia.Sdk.Kit.ShellMetrics.Bar.Top;

    /// <summary>The bar on <paramref name="canvas"/>, centred 8 DIPs under the display's top: where it stands.</summary>
    internal static DipRect PlaceBar(Canvas canvas, EditBar bar, double screenWidth)
    {
        canvas.Children.Add(bar);
        bar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var rect = new DipRect(Math.Round((screenWidth - bar.DesiredSize.Width) / 2), ShellMetricsBarTop, bar.DesiredSize.Width, bar.DesiredSize.Height);
        Canvas.SetLeft(bar, rect.X);
        Canvas.SetTop(bar, rect.Y);
        return rect;
    }

    /// <summary>
    /// [S-2340] The selection's page over <paramref name="view"/>'s <paramref name="selected"/> widgets, its
    /// lines the modules' own (or <paramref name="lines"/>' stand-in), its commands doing nothing.
    /// </summary>
    internal static PageView SelectionPageOf(ViewState view, IReadOnlyList<string> selected, Action<WidgetMenuLines>? lines = null)
    {
        var selection = new WidgetSelection();
        selection.Replace(view.Id, selected);
        return SelectionPageOf(view, selection, lines);
    }

    /// <summary>
    /// [S-2340] [S-11754] The selection's page over <paramref name="selection"/> of <paramref name="view"/> (several
    /// or one), its lines the modules' own (or <paramref name="lines"/>'
    /// stand-in for the one widget), its shared lines built as the surface builds them, its commands doing nothing.
    /// </summary>
    internal static PageView SelectionPageOf(ViewState view, WidgetSelection selection, Action<WidgetMenuLines>? lines = null)
    {
        var state = new AppState { Views = [view] };
        WidgetMenuLines? Over(WidgetId kind, WidgetConfig config, Action save)
            => Services.ModuleHost.MenuLines(kind, config, save, openSetting: _ => { });

        var commands = new SelectionCommands(_ => { }, _ => { }, _ => { }, _ => { }, _ => { },
            Cut: () => { },
            Copy: () => { },
            Delete: () => { },
            WidgetLines: id => lines is not null ? Stand(lines) : view.Find(id) is { } found ? Over(found.Kind, found.Config, () => { }) : null)
        {
            SharedLines = row => SharedRowLines.Of(row.Setting, row.Mixed, [.. view.Widgets.Where(w => row.Holders.Contains(w.Id)).Select(w => (w.Kind, w.Config))], Over, _ => { }),
        };

        var kinds = view.Widgets.Where(w => selection.Contains(w.Id)).Select(w => WidgetPick.Of(w.Config)).Distinct().ToList();
        var page = new PageView(() => SelectionPage.Of(state, selection, view.Id) is { } model
            ? SelectionRows.Of(model, commands, kinds)
            : new PageModel("selection", []));
        UiLanguage.ApplyFlow(page);
        return page;
    }

    /// <summary>
    /// [S-2325] A page's flyout on <paramref name="canvas"/>, hung from the entry of <paramref name="page"/>
    /// (null: the selection's) on <paramref name="bar"/>, inside <paramref name="area"/>, as the surface hangs it.
    /// </summary>
    internal static PageFlyout HangFlyout(Canvas canvas, PageView view, EditBar bar, DipRect barRect, SettingsPageId? page, DipRect area)
    {
        var flyout = new PageFlyout(view);
        canvas.Children.Add(flyout);
        canvas.UpdateLayout();
        var entry = bar.EntryBox(page, canvas) ?? barRect;
        flyout.Place(FlyoutPlacement.Place(entry, barRect, area, flyout.ContentHeight));
        canvas.UpdateLayout();
        return flyout;
    }

    /// <summary>A stand-in widget's lines.</summary>
    private static WidgetMenuLines Stand(Action<WidgetMenuLines> build)
    {
        var menu = new WidgetMenuLines(() => { });
        build(menu);
        return menu;
    }
}
#endif
