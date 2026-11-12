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
/// wallpaper, hung as the surface hangs them: <c>page-&lt;theme&gt;-views.png</c> (five views, the shown one's
/// rows, every view as an item), <c>-views-icon</c> (its icon picker), <c>-views-colour</c>, <c>-views-rename</c> (a
/// view's Rename tool: its page of its own), <c>-views-delete</c> (its "are you sure?" page), <c>-views-many</c>
/// (40 views: the page scrolls), <c>-views-many-end</c>; <c>page-&lt;theme&gt;-display.png</c>, <c>-display-type</c>,
/// <c>-display-position</c>, <c>-display-bar-views</c>, <c>-display-new</c> and <c>-display-end</c> (three displays,
/// one switched off). Stand-in hosts: nothing reaches Windows, the displays or the user's state.
/// </summary>
internal static partial class EditBarRender
{
    private static async Task BarMenuShots(Grid screen, string outDir, string theme)
    {
        var work = ViewsMenu.Keys.View("work");
        (string Name, int Views, string? Key, string? Click)[] views =
        [
            (string.Empty, 5, null, null),
            ("-icon", 5, ViewsMenu.Keys.Icon, null),
            ("-colour", 5, ViewsMenu.Keys.Colour, null),
            ("-rename", 5, null, "<tool>" + work + "|0"),
            ("-delete", 5, null, ViewsMenu.Keys.Delete),
            ("-many", 40, ViewsMenu.Keys.Manage, null),
            ("-many-end", 40, null, "<end>"),
        ];

        foreach (var (name, count, key, click) in views)
        {
            var host = new RenderViewsHost(count);
            var view = new PageView(() => PageRows.Of(SettingsPages.Views(host), edit => host.Page?.Ask(edit)));
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

        const string bar = DisplayMenu.Keys.ViewBar;
        (string Name, string? Key)[] display =
        [
            (string.Empty, null),
            ("-type", bar + "/type"),
            ("-position", bar + "/position"),
            ("-bar-views", bar + "/views"),
            ("-new", DisplayMenu.Keys.NewDisplays),
            ("-end", null),
        ];

        foreach (var (name, key) in display)
        {
            var host = new RenderDisplayHost();
            var view = new PageView(() => PageRows.Of(SettingsPages.Display(host), _ => { }));
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

            State.ActiveViewId = "work";
            State.MonitorViews[DisplayId] = "work";
        }

        public PageView? Page { get; set; }

        public AppState State { get; }

        public string DisplayId => "render-display";

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

        public void Confirm(SettingsConfirm confirm) => Page?.Confirm(new PageQuestion(confirm.Question, confirm.Accept, confirm.Done, confirm.Danger, confirm.Detail));
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

        public void SetScale(double scale) => Mutate(s => s.MonitorScales[DisplayId] = scale);

        public void Show(string viewId) => Mutate(s => s.ShowViewOn(DisplayId, viewId));

        private static MenuDisplay Display(string id, string name, string description, int x, int width, int height, bool primary)
            => new(id, name, description, new DisplayInfo(id, true, new PixelRect(x, 0, width, height), new PixelRect(x, 0, width, height - 48), primary));
    }
}
#endif
