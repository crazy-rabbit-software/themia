#if DEBUG
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.App.Widgets;
using Themia.Core.Geometry;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Settings;
using Themia.Core.Shell;
using Themia.Core.Widgets;
using Themia.Sdk;
using Windows.Foundation;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-3548] The edit bar's View and Display menus open under their buttons on the stand-in display,
/// over the busy wallpaper, as the surface places them (<see cref="DropDownPlacement"/>, the list
/// capped at the work area under the bar): <c>views-&lt;theme&gt;.png</c> (five views, the one shown
/// here with the dot), <c>-view</c> (a view's own submenu), <c>-icon</c> (its icon picker),
/// <c>-colour</c>, <c>-rename</c> and <c>-delete</c> (what each asks beside the menu, with its
/// <c>-popup0</c>), <c>-drag</c> (a view's line dragged, its drop line), <c>-many</c> (40 views: the
/// list scrolls inside the work area) and <c>-many-view</c>; <c>display-&lt;theme&gt;.png</c>, <c>-viewbar</c>,
/// <c>-position</c>, <c>-bar-views</c>, <c>-new</c> and <c>-showon</c> (three displays, one switched
/// off). Stand-in hosts: nothing reaches Windows, the displays or the user's state.
/// </summary>
internal static partial class EditBarRender
{
    private static async Task BarMenuShots(Grid screen, string outDir, string theme)
    {
        var work = "view:work";
        (string Name, int Views, string[] Open, string? Click)[] views =
        [
            (string.Empty, 5, [], null),
            ("-view", 5, [work], null),
            ("-icon", 5, [work, work + "/icon"], null),
            ("-colour", 5, [work, work + "/colour"], null),
            ("-rename", 5, [work], work + "/rename"),
            ("-delete", 5, [work], work + "/delete"),
            ("-many", 40, [], null),
            ("-many-view", 40, ["view:v37"], null),
        ];

        ConfirmFlyout.RenderInWindow = true;
        foreach (var (name, count, open, click) in views)
        {
            var host = new RenderViewsHost(count);
            await MenuShot(screen, System.IO.Path.Combine(outDir, $"views-{theme}{name}.png"), BarMenu.View, host.Menu, open, menu =>
            {
                if (click is not null && !menu.DebugClickKey(click))
                {
                    Console.WriteLine($"editbar: no line {click} to click");
                }
            }).ConfigureAwait(true);
        }

        // [S-3732] A view's line held by the pointer, its drop line two places down.
        await MenuShot(screen, System.IO.Path.Combine(outDir, $"views-{theme}-drag.png"), BarMenu.View, new RenderViewsHost(5).Menu, [], menu =>
        {
            if (!menu.DebugDrag(0, 3))
            {
                Console.WriteLine("editbar: the view's line could not be dragged");
            }
        }).ConfigureAwait(true);

        const string bar = DisplayMenu.Keys.ViewBar;
        (string Name, string[] Open)[] display =
        [
            (string.Empty, []),
            ("-viewbar", [bar]),
            ("-position", [bar, bar + "/position"]),
            ("-bar-views", [bar, bar + "/views"]),
            ("-new", [DisplayMenu.Keys.NewDisplays]),
            ("-showon", [DisplayMenu.Keys.ShowOn]),
        ];

        foreach (var (name, open) in display)
        {
            await MenuShot(screen, System.IO.Path.Combine(outDir, $"display-{theme}{name}.png"), BarMenu.Display, new RenderDisplayHost().Menu, open, null).ConfigureAwait(true);
        }
    }

    /// <summary>
    /// One picture of one of the bar's menus: the real bar centred 8 DIPs under the stand-in display's
    /// top, the menu under its button, placed and capped as the surface does it ([S-3522], [S-3540]),
    /// its submenus opened along <paramref name="open"/> (the lines' keys), then <paramref name="then"/>.
    /// </summary>
    private static async Task MenuShot(Grid screen, string path, BarMenu which, Func<BarMenuView> make, string[] open, Action<BarMenuView>? then)
    {
        screen.Children.Clear();
        screen.Height = OpenHeight;
        var flat = screen.Background;
        screen.Background = Wallpaper();

        var canvas = new Canvas { Clip = new RectangleGeometry { Rect = new Rect(0, 0, ScreenWidth, screen.Height) } };
        screen.Children.Add(canvas);
        Blobs(canvas);

        // Laid out before it is measured: the first picture's bar was measured before its words were.
        var editBar = Bar(snapOn: true);
        canvas.Children.Add(editBar);
        screen.UpdateLayout();
        editBar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var barRect = new PixelRect(Math.Round((ScreenWidth - editBar.DesiredSize.Width) / 2), Fluent.Space2, editBar.DesiredSize.Width, editBar.DesiredSize.Height);
        Canvas.SetLeft(editBar, barRect.X);
        Canvas.SetTop(editBar, barRect.Y);

        var menu = make();
        UiLanguage.ApplyFlow(menu);
        menu.Visibility = Visibility.Visible;
        canvas.Children.Add(menu);
        menu.Show();
        editBar.MenuOpen(which);
        screen.UpdateLayout();

        // [S-3522] Under its button, as the surface places it; [S-3540] the list capped under the bar.
        var area = new PixelRect(0, 0, ScreenWidth, screen.Height);
        var button = editBar.MenuButtonBox(which, canvas) ?? barRect;
        menu.MaxListHeight = DropDownPlacement.Room(barRect, area);
        menu.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var (x, y) = DropDownPlacement.Place(button, barRect, menu.DesiredSize.Width, menu.DesiredSize.Height, area, [barRect], UiLanguage.Flow == FlowDirection.RightToLeft);
        Canvas.SetLeft(menu, Math.Round(x));
        Canvas.SetTop(menu, Math.Round(y));
        menu.Room = new MenuRoom(area, null, [barRect]);
        screen.UpdateLayout();

        if (open.Length > 0 && !menu.Open(open, focus: false))
        {
            Console.WriteLine($"editbar: no submenu {string.Join('/', open)} for {path}");
        }

        then?.Invoke(menu);

        await Task.Delay(800).ConfigureAwait(true);
        await RenderPng.SaveAsync(screen, path).ConfigureAwait(true);
        Console.WriteLine($"editbar: wrote {path}");
        screen.Background = flat;
        await SavePopups(screen, path).ConfigureAwait(true);
    }

    /// <summary>A stand-in for the View menu's host: views of its own (Work shown here), every change drawn at once.</summary>
    private sealed class RenderViewsHost : IViewsMenuHost
    {
        private BarMenuView? _menu;

        public RenderViewsHost(int count)
        {
            State = AppState.CreateDefault();
            State.Views.Clear();
            (string Id, string Name, string? Icon, string? Colour, int Widgets)[] first =
            [
                ("desktop", "Desktop", null, null, 4),
                ("work", "Work", Glyphs.Mail, "#0099BC", 6),
                ("evening", "Evening and weekend at home", Glyphs.Moon, null, 2),
                ("focus", "Focus", null, "#E74856", 0),
                ("travel", "Travel", Glyphs.Sun, "#FFB900", 1),
            ];
            string[] more = ["Gaming", "Morning news", "Home office", "Studio", "Reading", "Family", "Music", "Finance", "Fitness", "Night"];

            for (var i = 0; i < count; i++)
            {
                var (id, name, icon, colour, widgets) = i < first.Length ? first[i] : ("v" + i, more[i % more.Length] + " " + LocaleFormat.Integer(i), i % 3 == 0 ? Glyphs.Calendar : null, i % 4 == 0 ? "#8764B8" : null, i % 3);
                var view = ViewState.Create(name);
                view.Id = id;
                view.Icon = icon;
                view.Color = colour;
                for (var w = 0; w < widgets; w++)
                {
                    view.Widgets.Add(WidgetInstance.Create(new WidgetId("clock"), new FracRect(0.05 + (0.1 * w), 0.1, 0.08, 0.08)));
                }

                State.Views.Add(view);
            }

            State.ActiveViewId = "work";
            State.MonitorViews[DisplayId] = "work";
        }

        public Func<BarMenuView> Menu => () => _menu = new BarMenuView(nameof(BarMenu.View), () => ViewsMenu.Root(this), () => { });

        public AppState State { get; }

        public string DisplayId => "render-display";

        public void Mutate(Action<AppState> change, bool relayout = true)
        {
            change(State);
            _menu?.Show();
        }

        public void Show(string viewId) => Mutate(s => s.ShowViewOn(DisplayId, viewId));

        public void Add()
        {
            // ignore: a render adds no view.
        }

        public void Duplicate(string viewId)
        {
            // ignore: a render copies no view.
        }

        public void Delete(string viewId)
        {
            // ignore: a render deletes no view.
        }

        public void Reorder(string viewId, string targetId) => Mutate(s => ViewOps.Reorder(s.Views, viewId, targetId));

        public void Confirm(SettingsConfirm confirm) => _menu?.Confirm(confirm);
    }

    /// <summary>A stand-in for the Display menu's host: three displays (the third switched off), this one the first.</summary>
    private sealed class RenderDisplayHost : IDisplayMenuHost
    {
        private BarMenuView? _menu;

        public RenderDisplayHost()
        {
            State = AppState.CreateDefault();
            var second = ViewState.Create("Office");
            second.Icon = Glyphs.Mail;
            second.Color = "#0099BC";
            State.Views.Add(second);
            State.Views.Add(ViewState.Create("Evening"));
            State.MonitorScales[DisplayId] = 1.25;
            State.DisabledMonitors.Add("render-3");
            State.DefaultViewForNewDisplays = second.Id;
            ViewBarRules.Update(State, DisplayId, b =>
            {
                b.Anchor = ViewBarAnchor.BottomCenter;
                b.VisibleViews = [State.Views[0].Id, second.Id];
            });
        }

        public Func<BarMenuView> Menu => () => _menu = new BarMenuView(nameof(BarMenu.Display), () => DisplayMenu.Root(this), () => { });

        public AppState State { get; }

        public string DisplayId => "render-1";

        public IReadOnlyList<MenuDisplay> Displays { get; } =
        [
            Display("render-1", "DELL U2723QE (primary)", "DELL U2723QE — 3840 × 2160 (primary)", 0, 3840, 2160, primary: true),
            Display("render-2", "LG HDR WQHD", "LG HDR WQHD — 2560 × 1440", 3840, 2560, 1440, primary: false),
            Display("render-3", "Right display", "Right display — 1920 × 1080", 6400, 1920, 1080, primary: false),
        ];

        public void Mutate(Action<AppState> change, bool relayout = true)
        {
            change(State);
            _menu?.Show();
        }

        public bool SetEnabled(string displayId, bool on)
        {
            // ignore: a render switches no display.
            return false;
        }

        public void SetScale(double scale) => Mutate(s => s.MonitorScales[DisplayId] = scale);

        private static MenuDisplay Display(string id, string name, string description, int x, int width, int height, bool primary)
            => new(id, name, description, new DisplayInfo(id, true, new PixelRect(x, 0, width, height), new PixelRect(x, 0, width, height - 48), primary));
    }
}
#endif
