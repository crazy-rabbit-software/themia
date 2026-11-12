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

    /// <summary>[S-2341] [S-11754] The bar's selection entry for <paramref name="selection"/> on <paramref name="view"/>: a group's name and glyph too.</summary>
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
    internal static PixelRect PlaceBar(Canvas canvas, EditBar bar, double screenWidth)
    {
        canvas.Children.Add(bar);
        bar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var rect = new PixelRect(Math.Round((screenWidth - bar.DesiredSize.Width) / 2), ShellMetricsBarTop, bar.DesiredSize.Width, bar.DesiredSize.Height);
        Canvas.SetLeft(bar, rect.X);
        Canvas.SetTop(bar, rect.Y);
        return rect;
    }

    /// <summary>
    /// [S-11707] The add page as the surface builds it: the whole catalogue and every ready-made group, its
    /// picks doing nothing, no tile over the free plan (a developer build is unrestricted).
    /// </summary>
    internal static AddPageFlyout AddMenu()
    {
        var page = AddPage.Of([.. ModuleCatalog.Widgets.Select(d => d.Id)], ReadyMadeGroups.Offered());
        var flyout = new AddPageFlyout(page, new AddPageCommands(_ => { }, () => { }, _ => false));
        UiLanguage.ApplyFlow(flyout);
        return flyout;
    }

    /// <summary>
    /// [S-11707] The add page on <paramref name="canvas"/>, hung flush under <paramref name="bar"/> at
    /// (<paramref name="barX"/>, <paramref name="barY"/>), as wide, inside a work area of
    /// <paramref name="screenWidth"/> × <paramref name="screenHeight"/>, as the surface hangs it.
    /// </summary>
    internal static void HangAddMenu(Canvas canvas, AddPageFlyout menu, FrameworkElement bar, double barX, double barY, double screenWidth, double screenHeight = OpenHeight)
    {
        canvas.Children.Add(menu);
        bar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        canvas.UpdateLayout();
        menu.Place(new PixelRect(barX, barY, bar.DesiredSize.Width, bar.DesiredSize.Height), new PixelRect(0, 0, screenWidth, screenHeight));
        menu.View.Drawn += () => menu.Place(new PixelRect(barX, barY, bar.DesiredSize.Width, bar.DesiredSize.Height), new PixelRect(0, 0, screenWidth, screenHeight));
        canvas.UpdateLayout();
    }

    /// <summary>
    /// [S-11712] One picture of the add page open under the real bar (Add marked) on a stand-in display of
    /// <paramref name="width"/> × <paramref name="height"/> over the busy wallpaper: <paramref name="tab"/>
    /// chosen or <paramref name="search"/> typed, every picture the page asked for drawn before the shot.
    /// </summary>
    internal static async Task AddPageShot(Grid screen, string path, double width, double height, AddFolderId? tab, string? search)
    {
        screen.Children.Clear();
        screen.Width = width;
        screen.Height = height;
        var flat = screen.Background;
        screen.Background = Wallpaper();
        var canvas = new Canvas { Clip = new Microsoft.UI.Xaml.Media.RectangleGeometry { Rect = new Rect(0, 0, width, height) } };
        screen.Children.Add(canvas);
        Blobs(canvas);

        var bar = Bar(new EditBarState(null, 0, null, false, width - (2 * ShellMetricsBarTop)) { AddOpen = true });
        var barRect = PlaceBar(canvas, bar, width);
        var page = AddMenu();
        HangAddMenu(canvas, page, bar, barRect.X, barRect.Y, width, height);
        if (tab is { } chosen)
        {
            page.View.ShowTab(chosen);
        }

        if (search is not null)
        {
            page.View.SetQuery(search);
        }

        canvas.UpdateLayout();
        await Task.Delay(300).ConfigureAwait(true);
        // A render is bounded: a picture that never finishes is said, and the shot shows its glyph.
        if (await Task.WhenAny(page.View.PicturesDrawn, Task.Delay(TimeSpan.FromMinutes(1))).ConfigureAwait(true) != page.View.PicturesDrawn)
        {
            Console.WriteLine($"editbar: {System.IO.Path.GetFileName(path)}: the pictures did not finish within a minute");
        }
        await Task.Delay(300).ConfigureAwait(true);

        await RenderPng.SaveAsync(screen, path).ConfigureAwait(true);
        Console.WriteLine($"editbar: wrote {path}");
        screen.Background = flat;
        screen.Width = ScreenWidth;
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
    /// [S-2340] [S-11754] The selection's page over <paramref name="selection"/> of <paramref name="view"/> (a
    /// group whole, an entered member, several, one), its lines the modules' own (or <paramref name="lines"/>'
    /// stand-in for the one widget), its shared lines built as the surface builds them, its commands doing nothing.
    /// </summary>
    internal static PageView SelectionPageOf(ViewState view, WidgetSelection selection, Action<WidgetMenuLines>? lines = null)
    {
        var state = new AppState { Views = [view] };
        PageView? page = null;
        void Ask(WidgetTextAsk ask) => page?.Ask(new PageEdit(ask.Placeholder, ask.Initial ?? string.Empty, ask.Done) { Title = ask.Title, Accept = ask.Accept });
        void Confirm(WidgetConfirm confirm) => page?.Confirm(new PageQuestion(confirm.Question, confirm.Accept, confirm.Done, confirm.Danger, confirm.Detail));
        WidgetMenuLines? Over(WidgetId kind, WidgetConfig config, Action save, Action<WidgetTextAsk> ask, Action<WidgetConfirm> confirm)
            => Services.ModuleHost.MenuLines(kind, config, save, ask, confirm, openSetting: _ => { });

        SelectionCommands Commands(bool whole) => new(_ => { }, _ => { }, (_, _) => { }, _ => { }, _ => { }, _ => { }, _ => { },
            Cut: whole ? () => { } : null,
            Copy: whole ? () => { } : null,
            Delete: whole ? () => { } : null,
            WidgetLines: id => lines is not null ? Stand(lines, Ask, Confirm) : view.Find(id) is { } found ? Over(found.Kind, found.Config, () => { }, Ask, Confirm) : null)
        {
            SharedLines = row => SharedRowLines.Of(row, [.. view.Widgets.Where(w => row.Holders.Contains(w.Id))], Over, Ask, Confirm, _ => { }),
            RenameGroup = whole ? (_, _) => { } : null,
            Ungroup = whole ? () => { } : null,
            RemoveFromGroup = whole ? _ => { } : null,
            OpenGroup = whole ? () => { } : null,
            Member = whole ? Member : null,
        };

        (SelectionModel, SelectionCommands)? Member(string id)
        {
            var alone = new WidgetSelection();
            alone.Replace(view.Id, [id]);
            return SelectionPage.Of(state, alone, view.Id) is { } model ? (model, Commands(whole: false)) : null;
        }

        var kinds = view.Widgets.Where(w => selection.Contains(w.Id)).Select(w => w.Kind).Distinct().ToList();
        page = new PageView(() => SelectionPage.Of(state, selection, view.Id) is { } model
            ? SelectionRows.Of(model, Commands(whole: true), kinds)
            : new PageModel("selection", []));
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
