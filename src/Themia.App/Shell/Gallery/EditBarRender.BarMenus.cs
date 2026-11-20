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
/// [S-2328] The Views and Display pages open from their entries on the stand-in display, over the busy
/// wallpaper, hung as the surface hangs them: <c>page-&lt;theme&gt;-views.png</c> (five views, every one a card),
/// <c>-views-open</c> (Work's card opened), <c>-views-icon</c> (scrolled to its icon picker), <c>-views-colour</c>,
/// <c>-views-icons</c> (its Desktop icons choice open: Work shows Chosen ones), <c>-views-chosen</c> (Work's list of the
/// desktop's items, three of seven on, [S-5529]), <c>-views-delete</c> (its Delete's "are you sure?" in place in its card, [S-2351]), <c>-views-many</c> (40 views: the page scrolls),
/// <c>-views-many-end</c>; <c>page-&lt;theme&gt;-display.png</c> (three displays, one switched off, every one a card),
/// <c>-display-open</c> (this display's card opened), <c>-display-bar</c> (scrolled to its view bar),
/// <c>-display-second</c>, <c>-display-off</c>, <c>-display-new</c> and <c>-display-end</c>. Stand-in hosts:
/// nothing reaches Windows, the displays or the user's state.
/// </summary>
internal static partial class EditBarRender
{
    private static async Task BarMenuShots(Grid screen, string outDir, string theme)
    {
        var work = ViewsMenu.Keys.View("work");
        (string Name, int Views, string? Key, string? Click)[] views =
        [
            (string.Empty, 5, null, null),
            ("-open", 5, work, null),
            ("-icon", 5, ViewsMenu.Keys.Icon("work"), null),
            ("-colour", 5, ViewsMenu.Keys.Colour("work"), null),
            ("-icons", 5, ViewsMenu.Keys.DesktopIcons("work"), null),
            ("-chosen", 5, ViewsMenu.Keys.ChosenIcons("work"), null),
            ("-delete", 5, null, "<tool>" + work + "|1"),
            ("-many", 40, null, null),
            ("-many-end", 40, null, "<end>"),
        ];

        foreach (var (name, count, key, click) in views)
        {
            var host = new RenderViewsHost(count);
            var view = new PageView(() => PageRows.Of(SettingsPages.Views(host)));
            host.Page = view;
            await PageShot(screen, System.IO.Path.Combine(outDir, $"page-{theme}-views{name}.png"), SettingsPageId.Views, view, () => SettingsPages.Views(host), key, page =>
            {
                if (click is not null && click.StartsWith("<tool>", StringComparison.Ordinal))
                {
                    // [S-3549] A view's Rename or Delete at the end of its row.
                    var (row, tool) = (click[6..].Split('|')[0], int.Parse(click.Split('|')[1], System.Globalization.CultureInfo.InvariantCulture));
                    if (!page.DebugTool(row, tool))
                    {
                        Console.WriteLine($"editbar: no tool {tool} on {row}");
                    }
                }
                else if (click is not null and not "<end>")
                {
                    Click(page, click);
                }
            }, click == "<end>" ? f => f.Scroll(Windows.System.VirtualKey.End) : null).ConfigureAwait(true);
        }

        await DisplayShots(screen, outDir, theme).ConfigureAwait(true);
    }

    /// <summary>
    /// [S-2328] The Display page's shots; [S-13406] <c>-display-type</c> its view bar's Type (the styles) open.
    /// <c>--editbar-png --only display</c> draws these alone.
    /// </summary>
    private static async Task DisplayShots(Grid screen, string outDir, string theme)
    {
        (string Name, string? Key)[] display =
        [
            (string.Empty, null),
            ("-open", DisplayMenu.Keys.Display("render-1")),
            ("-bar", DisplayMenu.Keys.ViewBar("render-1") + "/position"),
            ("-type", DisplayMenu.Keys.ViewBar("render-1") + "/type"),
            ("-second", DisplayMenu.Keys.Display("render-2")),
            ("-off", DisplayMenu.Keys.Display("render-3")),
            ("-new", DisplayMenu.Keys.NewDisplays),
            ("-end", null),
        ];

        foreach (var (name, key) in display)
        {
            var host = new RenderDisplayHost();
            var view = new PageView(() => PageRows.Of(SettingsPages.Display(host)));
            host.Page = view;
            await PageShot(screen, System.IO.Path.Combine(outDir, $"page-{theme}-display{name}.png"), SettingsPageId.Display, view, () => SettingsPages.Display(host), key,
                after: name == "-end" ? f => f.Scroll(Windows.System.VirtualKey.End) : null).ConfigureAwait(true);
        }
    }

    /// <summary>A stand-in for the View menu's host: views of its own (Work shown here), every change drawn at once.</summary>
    private sealed class RenderViewsHost : IViewsMenuHost
    {
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

            // [S-5529] Work shows Chosen desktop icons (three of the seven), Travel none.
            if (State.FindView("work") is { } chosen)
            {
                chosen.DesktopIcons = ViewDesktopIcons.Chosen;
                foreach (var item in DesktopItems.Where(item => item.Name is "Projects" or "Budget 2026.xlsx" or "This PC"))
                {
                    var icon = WidgetInstance.Create(new WidgetId("desktopItem"), new FracRect(0.01, 0.01, 0.05, 0.08));
                    if (icon.Config is IDesktopItemConfig config)
                    {
                        config.Item = item.Key;
                    }

                    chosen.Widgets.Add(icon);
                }
            }

            if (State.FindView("travel") is { } bare)
            {
                bare.DesktopIcons = ViewDesktopIcons.None;
            }

            State.ActiveViewId = "work";
            State.MonitorViews[DisplayId] = "work";
            State.MonitorViews["render-2"] = "desktop";
        }

        public PageView? Page { get; set; }

        public AppState State { get; }

        public string DisplayId => "render-display";

        public IReadOnlyList<MenuDisplay> Displays { get; } =
        [
            new("render-display", "DELL U2723QE (primary)", "DELL U2723QE — 3840 × 2160 (primary)", new DisplayInfo("render-display", true, new PixelRect(0, 0, 3840, 2160), new PixelRect(0, 0, 3840, 2112), true)),
            new("render-2", "LG HDR WQHD", "LG HDR WQHD — 2560 × 1440", new DisplayInfo("render-2", true, new PixelRect(3840, 0, 2560, 1440), new PixelRect(3840, 0, 2560, 1392), false)),
        ];

        public void Show(string viewId) => Mutate(s => s.ShowViewOn(DisplayId, viewId));

        public void Mutate(Action<AppState> change, bool relayout = true)
        {
            change(State);
            Page?.Show();
        }

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

        /// <summary>[S-5529] A stand-in desktop: two shell items, a folder, a shortcut and three files.</summary>
        public IReadOnlyList<DesktopItem> DesktopItems { get; } =
        [
            new("::{20D04FE0-3AEA-1069-A2D8-08002B30309D}", "This PC", DesktopItemKind.ShellItem),
            new("::{645FF040-5081-101B-9F08-00AA002F954E}", "Recycle Bin", DesktopItemKind.ShellItem),
            new(@"C:\Users\a\Desktop\Projects", "Projects", DesktopItemKind.Folder),
            new(@"C:\Users\a\Desktop\Spotify.lnk", "Spotify", DesktopItemKind.Shortcut),
            new(@"C:\Users\a\Desktop\Budget 2026.xlsx", "Budget 2026.xlsx", DesktopItemKind.File),
            new(@"C:\Users\a\Desktop\Notes.txt", "Notes.txt", DesktopItemKind.File),
            new(@"C:\Users\a\Desktop\Holiday.jpg", "Holiday.jpg", DesktopItemKind.File),
        ];

        public void SetDesktopIcons(string viewId, ViewDesktopIcons value)
            => Mutate(s => _ = s.FindView(viewId) is { } view && Themia.Core.Desktop.DesktopViews.Set(view, value));

        public void ShowDesktopItem(string viewId, string key, bool shown)
        {
            // ignore: a render ticks no item.
        }
    }

    /// <summary>A stand-in for the Display menu's host: three displays (the third switched off), this one the first.</summary>
    private sealed class RenderDisplayHost : IDisplayMenuHost
    {
        public RenderDisplayHost()
        {
            State = AppState.CreateDefault();

            // [S-3543] The view this display shows is the bar's View crumb's: Work, in its colour.
            var work = State.Views.FirstOrDefault(v => v.Name == "Work");
            if (work is null)
            {
                work = ViewState.Create("Work");
                State.Views.Add(work);
            }

            work.Icon = Glyphs.Mail;
            work.Color = "#0099BC";
            State.ShowViewOn(DisplayId, work.Id);
            var second = ViewState.Create("Office");
            second.Icon = Glyphs.Calendar;
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

        public PageView? Page { get; set; }

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
            Page?.Show();
        }

        public bool SetEnabled(string displayId, bool on)
        {
            // ignore: a render switches no display.
            return false;
        }

        public void SetScale(string displayId, double scale) => Mutate(s => s.MonitorScales[displayId] = scale);

        public void Show(string displayId, string viewId) => Mutate(s => s.ShowViewOn(displayId, viewId));

        private static MenuDisplay Display(string id, string name, string description, int x, int width, int height, bool primary)
            => new(id, name, description, new DisplayInfo(id, true, new PixelRect(x, 0, width, height), new PixelRect(x, 0, width, height - 48), primary));
    }
}
#endif
