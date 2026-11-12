#if DEBUG
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Widgets;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Settings;
using Themia.Core.Shell;
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
        var offer = QuickSet.For(new AppState { Views = [view] }, selection, view.Id);
        var kind = offer.TitleKind ?? offer.CommonKind;
        return SelectionPage.Words(offer, id => ModuleCatalog.Of(id).Name) is { } words
            ? new BarSelection(words, kind is { } k ? ModuleCatalog.Of(k).Glyph : Glyphs.Apps)
            : null;
    }

    /// <summary>[S-2320] The bar as a render shows it: <paramref name="selection"/>'s entry, its flyout open or not.</summary>
    internal static EditBar SelectionBar(BarSelection? selection, bool open = false)
        => Bar(new EditBarState(selection, 0, null, open && selection is not null, ScreenWidth - (2 * ShellMetricsBarTop)));

    private const double ShellMetricsBarTop = Themia.Sdk.Kit.ShellMetrics.Bar.Top;

    /// <summary>The bar on <paramref name="canvas"/>, centred 8 DIPs under the display's top: where it stands.</summary>
    internal static PixelRect PlaceBar(Canvas canvas, EditBar bar, double screenWidth)
    {
        canvas.Children.Add(bar);
        bar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var rect = new PixelRect(Math.Round((screenWidth - bar.DesiredSize.Width) / 2), ShellMetricsBarTop, bar.DesiredSize.Width, bar.DesiredSize.Height);
        Canvas.SetLeft(bar, rect.X);
        Canvas.SetTop(bar, rect.Y);
        return rect;
    }

    /// <summary>[S-3506] The add menu as the flyout under Add widget presents it.</summary>
    internal static FrameworkElement AddMenu()
    {
        var menu = PickerRender.Presenter(new WidgetPicker(_ => { }, () => { }, new PixelRect(0, 0, PickerRender.WorkWidth, PickerRender.WorkHeight)));
        UiLanguage.ApplyFlow(menu);
        return menu;
    }

    /// <summary>
    /// [S-3506] <paramref name="menu"/> on <paramref name="canvas"/>, centred under the bar's Add
    /// widget button (where it starts is only known once the bar is laid out), a gap below the bar.
    /// </summary>
    internal static void HangAddMenu(Canvas canvas, FrameworkElement menu, FrameworkElement bar, double barX, double barY, double screenWidth)
    {
        canvas.Children.Add(menu);
        menu.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        canvas.UpdateLayout();
        var addWidth = bar is EditBar edit ? edit.AddWidth : 0;
        var addCentre = barX + (bar is EditBar hung ? hung.AddOffset : 0) + (addWidth / 2);
        Canvas.SetLeft(menu, Math.Clamp(addCentre - (menu.DesiredSize.Width / 2), Fluent.Space2, screenWidth - menu.DesiredSize.Width - Fluent.Space2));
        Canvas.SetTop(menu, barY + bar.DesiredSize.Height + Fluent.Space1);
    }

    /// <summary>
    /// [S-2340] The selection's page over <paramref name="view"/>'s <paramref name="selected"/> widgets, its
    /// lines the modules' own (or <paramref name="lines"/>' stand-in), its commands doing nothing.
    /// </summary>
    internal static PageView SelectionPageOf(ViewState view, IReadOnlyList<string> selected, Action<WidgetMenuLines>? lines = null)
    {
        var state = new AppState { Views = [view] };
        var selection = new WidgetSelection();
        selection.Replace(view.Id, selected);
        var offer = QuickSet.For(state, selection, view.Id);
        PageView? page = null;
        var commands = new SelectionCommands(_ => { }, _ => { }, (_, _) => { }, _ => { }, _ => { }, _ => { }, _ => { }, Cut: () => { }, Copy: () => { }, Delete: () => { });
        WidgetMenuLines? Lines()
        {
            if (offer.TitleWidgetId is not { } id)
            {
                return null;
            }

            void Ask(WidgetTextAsk ask) => page?.Ask(new PageEdit(ask.Placeholder, ask.Initial ?? string.Empty, ask.Done) { Title = ask.Title, Accept = ask.Accept });
            void Confirm(WidgetConfirm confirm) => page?.Confirm(new PageQuestion(confirm.Question, confirm.Accept, confirm.Done, confirm.Danger, confirm.Detail));
            return lines is not null ? Stand(lines, Ask, Confirm) : view.Find(id) is { } found ? Services.ModuleHost.MenuLines(found, () => { }, Ask, Confirm, openSetting: _ => { }) : null;
        }

        var kinds = view.Widgets.Where(w => selected.Contains(w.Id)).Select(w => w.Kind).Distinct().ToList();
        page = new PageView(() => SelectionRows.Of(offer, commands, Lines(), kinds));
        UiLanguage.ApplyFlow(page);
        return page;
    }

    /// <summary>
    /// [S-2325] A page's flyout on <paramref name="canvas"/>, hung from the entry of <paramref name="page"/>
    /// (null: the selection's) on <paramref name="bar"/>, inside <paramref name="area"/>, as the surface hangs it.
    /// </summary>
    internal static PageFlyout HangFlyout(Canvas canvas, PageView view, EditBar bar, PixelRect barRect, SettingsPageId? page, PixelRect area)
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
    private static WidgetMenuLines Stand(Action<WidgetMenuLines> build, Action<WidgetTextAsk> ask, Action<WidgetConfirm> confirm)
    {
        var menu = new WidgetMenuLines(() => { }, ask, confirm);
        build(menu);
        return menu;
    }
}
#endif
