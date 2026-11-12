#if DEBUG
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Widgets;
using Themia.Core.Geometry;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Windows.Foundation;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// The edit-mode parts a render composes on a stand-in display, shared by the edit bar's pictures
/// ([S-3506], [S-3713]) and a store scene's ([S-1077]): the real bar with its trail, the add menu
/// hung under Add widget, and the real properties menu placed by <see cref="StripPlacement"/>.
/// </summary>
internal static partial class EditBarRender
{
    /// <summary>[S-3507] The bar's widget crumb for a selection: the one widget's title or kind, or how many; none for none.</summary>
    internal static BarCrumb? SelectionCrumb(IReadOnlyList<WidgetInstance> selected) => selected.Count switch
    {
        0 => null,
        1 => new BarCrumb(selected[0].Label ?? ModuleCatalog.Of(ModuleCatalog.IdOf(selected[0].Config)).Name, ModuleCatalog.Of(ModuleCatalog.IdOf(selected[0].Config)).Glyph),
        _ => new BarCrumb(Str.Shell_Views_WidgetCount.Plural(selected.Count), Glyphs.Apps),
    };

    /// <summary>[S-3500] The bar as a render shows it with <paramref name="widget"/> as its last crumb.</summary>
    internal static EditBar SelectionBar(BarCrumb? widget) => Bar(Plain(widget: widget));

    /// <summary>The bar on <paramref name="canvas"/>, centred 8 DIPs under the display's top: where it stands.</summary>
    internal static PixelRect PlaceBar(Canvas canvas, EditBar bar, double screenWidth)
    {
        canvas.Children.Add(bar);
        bar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var rect = new PixelRect(Math.Round((screenWidth - bar.DesiredSize.Width) / 2), Fluent.Space2, bar.DesiredSize.Width, bar.DesiredSize.Height);
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
    /// [S-3713] The real properties menu over <paramref name="view"/>'s <paramref name="selected"/>
    /// widgets, its lines the modules' own (or <paramref name="lines"/>' stand-in), its one rectangle
    /// no taller than <paramref name="roomHeight"/>.
    /// </summary>
    internal static PropertiesStrip Properties(ViewState view, IReadOnlyList<string> selected, double roomHeight, Action<WidgetMenuLines>? lines = null)
    {
        var state = new AppState { Views = [view] };
        var selection = new WidgetSelection();
        selection.Replace(view.Id, selected);

        var strip = new PropertiesStrip(new PropertiesCommands(_ => { }, _ => { }, (_, _) => { }, () => { }, _ => { }, _ => { }, _ => { }, _ => { },
            Cut: () => { }, Copy: () => { }, Delete: () => { },
            WidgetLines: (id, ask, confirm) => lines is not null ? Stand(lines, ask, confirm) : view.Find(id) is { } found ? Services.ModuleHost.MenuLines(found, () => { }, ask, confirm, openSetting: _ => { }) : null));
        UiLanguage.ApplyFlow(strip);
        strip.RoomHeight = roomHeight;
        strip.ShowSelection(QuickSet.For(state, selection, view.Id));
        strip.Visibility = Visibility.Visible;
        return strip;
    }

    /// <summary>
    /// [S-3708] The menu on <paramref name="canvas"/> beside the selection's painted box, inside
    /// <paramref name="area"/> and clear of the bar, as the surface places it; nowhere for no selection.
    /// </summary>
    internal static void PlaceProperties(Canvas canvas, PropertiesStrip strip, IEnumerable<PixelRect> painted, PixelRect area, PixelRect barRect)
    {
        canvas.Children.Add(strip);
        strip.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        if (StripPlacement.Bounds(painted) is { } box)
        {
            var spot = StripPlacement.Place(box, strip.DesiredSize.Width, strip.DesiredSize.Height, area, [barRect], UiLanguage.Flow == FlowDirection.RightToLeft);
            Canvas.SetLeft(strip, Math.Round(spot.X));
            Canvas.SetTop(strip, Math.Round(spot.Y));
        }
    }
}
#endif
