using Themia.App.Shell;
using Themia.Core.Desktop;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Shell;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Settings;

/// <summary>
/// [S-3541] What the edit bar's Views page reads and writes on one display: the state (every write one
/// change, one undo step), the connected displays (where each view is shown), and the host's view
/// commands, which keep the licence, the display assignments and the undo notice as they are everywhere else.
/// </summary>
public interface IViewsMenuHost
{
    /// <summary>The live state, read as it is now.</summary>
    AppState State { get; }

    /// <summary>[S-842] The display the page is open on: "this display".</summary>
    string DisplayId { get; }

    /// <summary>[S-2313] Every connected display, in Windows' order: where a view is shown, said under its name.</summary>
    IReadOnlyList<MenuDisplay> Displays { get; }

    /// <summary>One change of the state: saved, one undo step.</summary>
    void Mutate(Action<AppState> change, bool relayout = true);

    /// <summary>[S-1065] A new, empty view at the end, shown here; refused past the free limit, with a notice saying why.</summary>
    void Add();

    /// <summary>[S-900] A copy of a view right after it, shown here; within the licence.</summary>
    void Duplicate(string viewId);

    /// <summary>[S-902] Delete a view (never the last), repairing what named it; the undo notice offers it back ([S-881]).</summary>
    void Delete(string viewId);

    /// <summary>[S-901] Move a view into another's place in the order, one undo step.</summary>
    void Reorder(string viewId, string targetId);

    /// <summary>[S-2313] [S-19] Show a view on this display, as a click on its chip in the view bar does.</summary>
    void Show(string viewId);

    /// <summary>[S-3727] Ask on a page of its own; only the answer runs it.</summary>
    void Confirm(SettingsConfirm confirm);

    /// <summary>[S-5529] The desktop's items as last read ([S-5510]): the lines of a Chosen view's list.</summary>
    IReadOnlyList<DesktopItem> DesktopItems { get; }

    /// <summary>[S-5528] Set which desktop icons a view shows, one undo step; set to All, what it lacks is laid in at the first free spots.</summary>
    void SetDesktopIcons(string viewId, ViewDesktopIcons value);

    /// <summary>[S-5527] Show a desktop item on a view (at its first free spot) or hide it from that view, one undo step.</summary>
    void ShowDesktopItem(string viewId, string key, bool shown);
}

/// <summary>
/// [S-2313] The edit bar's Views page as data, after Karpo's Layouts (karpo-app, Karpo.Core/BarLooks.cs
/// <c>LayoutsRows</c>, docs/widgets.md §585, §595): under "Your views" every view in the user's order, each a card
/// that opens (a thing): its mark in its colour, its name, where it is shown and how many widgets it holds under
/// it, Duplicate and Delete at its head's end, dragged or Ctrl+Up / Down into order; opened, its Name, Icon and
/// Colour and Show on this display. Then "Add a view". Nothing here acts on "the selected view": every line is
/// one view's. Which view a display shows is that display's (the Display page, [S-2314]); Show on this display
/// is a command, as the view bar's chip is. Keys hold the views' ids, so a rename, a language switch or a move
/// keeps what is open open.
/// </summary>
public static class ViewsMenu
{
    /// <summary>[S-2313] The page's lines: the heading, every view's card, then Add a view.</summary>
    public static IReadOnlyList<SettingsLine> Root(IViewsMenuHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        var views = host.State.Views;
        var lines = new List<SettingsLine>(views.Count + 3)
        {
            new SettingsHeading(Keys.Heading, Str.SettingsPages_Views_Yours.Text()),
        };

        // [S-3732] One move for the whole list: the page drags a card among the cards of the same move.
        Action<int, int> move = (from, to) => Move(host, from, to);
        for (var i = 0; i < views.Count; i++)
        {
            lines.Add(View(host, views[i], new SettingsOrder(i, views.Count, move)));
        }

        lines.Add(new SettingsSeparator("-" + Keys.Add));

        // [S-1065] An empty view at the end, shown here; past the free limit refused with a notice saying why.
        lines.Add(new SettingsCommand(Keys.Add, Str.SettingsPages_Views_Add.Text(), Glyphs.Add, host.Add));
        return lines;
    }

    /// <summary>
    /// [S-2313] One view's card: its head (mark, name, where it is shown and its widgets, Duplicate and Delete), and
    /// opened its Name, Icon, Colour and Show on this display.
    /// </summary>
    private static SettingsSubmenu View(IViewsMenuHost host, ViewState view, SettingsOrder order)
    {
        var id = view.Id;
        var key = Keys.View(id);
        var state = host.State;
        var widgets = Str.Shell_Views_WidgetCount.Plural(view.Widgets.Count);
        var here = ShownOn(state, host.DisplayId) == id;
        var last = state.Views.Count <= 1;

        List<SettingsLine> lines =
        [
            // [S-1707] Its name, kept trimmed as it is typed in its box.
            new SettingsAsk(Keys.Name(id), Str.View_Name_Label.Text(), Glyphs.Rename, Str.BarMenus_View_RenameTitle.Text(), Str.View_Name_Label.Text(),
                Str.Common_Rename.Text(), view.Name, _ => null, name => host.Mutate(s => ViewOps.Rename(s, id, name))),

            // [S-903] Its mark: a glyph, or its initials.
            new SettingsIcon(Keys.Icon(id), Str.View_Icon_Label.Text(), Mark(view),
                string.IsNullOrEmpty(view.Icon) ? null : view.Icon, ViewListRules.Initials(view.Name), Str.Shell_View_Initials.Text(),
                glyph => host.Mutate(s =>
                {
                    if (s.FindView(id) is { } target)
                    {
                        target.Icon = glyph;
                    }
                }))
            {
                GlyphColour = view.Color,
            },

            // [S-3810] The colour its mark is drawn in.
            new SettingsColour(Keys.Colour(id), Str.Shell_View_Colour.Text(), view.Color ?? string.Empty,
                value => host.Mutate(s => ViewOps.SetColor(s, id, value), relayout: false), Str.Shell_View_ColourDefault.Text(), ColorPresets.Windows),
            new SettingsSeparator(key + "/-icons"),
        ];

        // [S-5529] Which desktop icons it shows, and while Chosen the list to tick them on.
        lines.AddRange(DesktopIcons(host, view));
        lines.AddRange(
        [
            new SettingsSeparator(key + "/-show"),

            // [S-19] A command, as the view bar's chip: the Display page holds which view each display shows.
            new SettingsCommand(Keys.Show(id), Str.SettingsPages_Views_ShowHere.Text(), Glyphs.Display, () => host.Show(id))
            {
                Disabled = here,
            },
        ]);

        return new SettingsSubmenu(key, view.Name, Mark(view), lines)
        {
            Card = true,
            GlyphColour = view.Color,
            Caption = string.Join(" · ", Where(host, id), widgets),
            Order = order,
            Tools =
            [
                // [S-900] A copy of it, shown here.
                new SettingsTool(key + "/duplicate", Glyphs.Duplicate, Str.App_DuplicateView.Text(), SettingsToolKey.None) { Run = () => host.Duplicate(id) },

                // [S-902] [S-12] Asked first with what goes with it; greyed on the last view.
                new SettingsTool(key + "/delete", Glyphs.Delete, Str.App_DeleteView.Text(), SettingsToolKey.Delete)
                {
                    Run = () => host.Confirm(new SettingsConfirm(
                        Str.BarMenus_View_DeleteQuestion.Format(("name", view.Name)),
                        Str.Common_Delete.Text(),
                        () => host.Delete(id),
                        Str.BarMenus_View_DeleteDetail.Format(("widgets", widgets)),
                        Danger: true)),
                    Disabled = last,
                },
            ],
        };
    }

    /// <summary>
    /// [S-5529] A view's Desktop icons: a choice of All, Chosen and None; while Chosen, under it the list of the desktop's
    /// items (a page of its own behind a back head: the card's one level is the choice's), each a switch that shows it on
    /// this view or hides it, in <see cref="DesktopItemOrder"/>, with how many are on at its end.
    /// </summary>
    private static IEnumerable<SettingsLine> DesktopIcons(IViewsMenuHost host, ViewState view)
    {
        var id = view.Id;
        SettingsRadio Option(ViewDesktopIcons value, Str text)
            => new(Keys.DesktopIcons(id) + "/" + value.ToString().ToLowerInvariant(), text.Text(), view.DesktopIcons == value, () => host.SetDesktopIcons(id, value));

        yield return new SettingsChoice(Keys.DesktopIcons(id), Str.Desktop_ViewIcons_Label.Text(), Glyphs.Apps,
        [
            Option(ViewDesktopIcons.All, Str.Desktop_ViewIcons_All),
            Option(ViewDesktopIcons.Chosen, Str.Desktop_ViewIcons_Chosen),
            Option(ViewDesktopIcons.None, Str.Desktop_ViewIcons_None),
        ]);

        if (view.DesktopIcons != ViewDesktopIcons.Chosen)
        {
            yield break;
        }

        // [S-13001] The free desktop's choice alone: an item in a fence of this view is shown with its fence, never listed here.
        var held = DesktopFences.Held(view);
        var items = DesktopItemOrder.Sort(host.DesktopItems.Where(item => !held.Contains(item.Key)).DistinctBy(item => item.Key, DesktopItemKeys.Comparer));
        var shown = DesktopViews.Free(view);
        List<SettingsLine> ticks = items.Count == 0
            ? [new SettingsNote(Keys.ChosenIcons(id) + "/none", Str.Desktop_ViewIcons_Empty.Text())]
            : [.. items.Select(item => new SettingsToggle(Keys.ChosenIcon(id, item.Key), item.Name, ItemGlyph(item.Kind), shown.Contains(item.Key),
                on => host.ShowDesktopItem(id, item.Key, on)))];
        yield return new SettingsSubmenu(Keys.ChosenIcons(id), Str.Desktop_ViewIcons_List.Text(), Glyphs.Checklist, ticks)
        {
            Page = true,
            Value = Str.Desktop_ViewIcons_Count.Format(("shown", LocaleFormat.Integer(items.Count(item => shown.Contains(item.Key)))), ("all", LocaleFormat.Integer(items.Count))),
        };
    }

    /// <summary>[S-5529] A desktop item's line glyph by its kind.</summary>
    private static string ItemGlyph(DesktopItemKind kind) => kind switch
    {
        DesktopItemKind.Folder => Glyphs.Folder,
        DesktopItemKind.Shortcut => Glyphs.Link,
        DesktopItemKind.ShellItem => Glyphs.Apps,
        _ => Glyphs.File,
    };

    /// <summary>
    /// [S-2313] Where a view is shown, under its name: "Shown on this display, DELL U2720Q" (the page's own display
    /// named so, the others by their names, in Windows' order; a display Themia is off on not counted), or that no
    /// display shows it.
    /// </summary>
    public static string Where(IViewsMenuHost host, string viewId)
    {
        ArgumentNullException.ThrowIfNull(host);
        var state = host.State;
        var names = host.Displays
            .Where(d => !state.DisabledMonitors.Contains(d.Id) && ShownOn(state, d.Id) == viewId)
            .Select(d => d.Id == host.DisplayId ? Str.BarMenus_Display_This.Text().ToLowerInvariant() : d.Name)
            .ToList();
        return names.Count == 0
            ? Str.SettingsPages_Views_NotShown.Text()
            : Str.SettingsPages_Views_ShownOn.Format(("displays", string.Join(", ", names)));
    }

    /// <summary>[S-3810] A view's mark: its own icon, or the shared view glyph where it has none.</summary>
    internal static string Mark(ViewState view) => string.IsNullOrEmpty(view.Icon) ? Glyphs.View : view.Icon;

    /// <summary>[S-3732] A drop or Ctrl+arrow: the view at <paramref name="from"/> takes the place of the one at <paramref name="to"/>, as the state holds them now.</summary>
    private static void Move(IViewsMenuHost host, int from, int to)
    {
        var views = host.State.Views;
        if (from == to || from < 0 || to < 0 || from >= views.Count || to >= views.Count)
        {
            return;
        }

        host.Reorder(views[from].Id, views[to].Id);
    }

    /// <summary>
    /// [S-1706] The view a display shows: its own assignment, or the active view when it has none yet
    /// ([S-18]). Read only, unlike <see cref="AppState.ViewForMonitor"/>, which repairs.
    /// </summary>
    public static string ShownOn(AppState state, string displayId)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.MonitorViews.TryGetValue(displayId, out var assigned) && state.FindView(assigned) is not null
            ? assigned
            : state.ActiveView().Id;
    }

    /// <summary>The page's keys that something outside it opens (a deep link, a render, a test).</summary>
    public static class Keys
    {
        /// <summary>"Your views": the section every view's card stands in.</summary>
        public const string Heading = "views";

        public const string Add = "views/add";

        /// <summary>[S-2313] A view's card, by its id: the same in every language and through a rename.</summary>
        public static string View(string viewId) => "view:" + viewId;

        public static string Name(string viewId) => View(viewId) + "/name";

        public static string Icon(string viewId) => View(viewId) + "/icon";

        public static string Colour(string viewId) => View(viewId) + "/colour";

        public static string Show(string viewId) => View(viewId) + "/show";

        /// <summary>[S-5529] A view's Desktop icons choice.</summary>
        public static string DesktopIcons(string viewId) => View(viewId) + "/desktop-icons";

        /// <summary>[S-5529] A Chosen view's list of the desktop's items.</summary>
        public static string ChosenIcons(string viewId) => View(viewId) + "/chosen-icons";

        /// <summary>[S-5529] One item's switch in that list, by its key.</summary>
        public static string ChosenIcon(string viewId, string itemKey) => ChosenIcons(viewId) + "/" + itemKey;
    }
}
