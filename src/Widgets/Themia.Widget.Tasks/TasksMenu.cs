using System.Runtime.CompilerServices;
using Themia.App.Services.Tasks;
using Themia.App.Shell;
using Themia.App.Widgets;
using Themia.Core.Accounts;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Tasks;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;

namespace Themia.Widget.Tasks;

/// <summary>
/// [S-7516] The Tasks widget's options, all on its properties menu ([S-3719]): Appearance
/// ([S-3723]); where the list comes from (an account, the first account with tasks, or this PC) and
/// which list (an account's lists, its Today view, or a list kept on this PC, made, renamed and
/// deleted here); then, each group after a divider, what each task shows and which tasks the list
/// keeps ([S-611], [S-7516]). It has no settings page.
/// <para>
/// Accounts are signed in from ⚙ › Accounts, not here: the menu lists the accounts that can hold
/// tasks and opens that page. Lists on this PC, by contrast, are made, renamed and deleted right
/// here, because the widget is the only place they exist.
/// </para>
/// </summary>
internal static class TasksMenu
{
    private const string Scope = "tasks-menu";

    /// <summary>[S-3746] Each task service's account lists as the menus last read them, by account.</summary>
    private static readonly ConditionalWeakTable<ITaskSource, MenuLoads<TaskListing>> Listings = new();

    /// <summary>[S-592] An account's lists as last read, or why they could not be.</summary>
    internal sealed record TaskListing(IReadOnlyList<TaskListInfo>? Lists, string? Problem)
    {
        public static bool Same(TaskListing a, TaskListing b)
            => a.Problem == b.Problem && (a.Lists ?? []).SequenceEqual(b.Lists ?? []);
    }

    /// <param name="service">The host's task service (<see cref="SettingsHost.Tasks"/>), null before it has started.</param>
    public static void Lines(WidgetMenuLines menu, TasksConfig config, ITaskSource? service)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);

        menu.PlaceCard();
        if (service is null)
        {
            menu.Lines.Add(WidgetMenuLines.Note(TasksStr.WidgetsC_Tasks_Settings_NotStarted.Text()));
        }
        else
        {
            menu.Lines.Add(Source(menu, config, service));
            if (List(menu, config, service) is { } list)
            {
                menu.Lines.Add(list);
            }
        }

        // [S-7516] What each task shows.
        menu.Lines.Add(ItemMenuEntry.Separator);

        // [S-6074] Due dates exist only on an account's tasks.
        if (config.Offers(TasksSetting.DueDate))
        {
            menu.Lines.Add(menu.Toggle(Glyphs.Calendar, TasksStr.WidgetsC_Tasks_Settings_ShowDue_Label.Text(), config.ShowDueDate, on => config.ShowDueDate = on));
        }

        menu.Lines.Add(menu.Number(Glyphs.AlignLeft, TasksStr.Todo_TitleLines_Label.Text(), config.EffectiveTitleLines, TaskRules.TitleLinesMin, TaskRules.TitleLinesMax, 1,
            v => LocaleFormat.Integer(v), value => config.TitleLines = value));

        // [S-7516] Which tasks the list keeps, and where a new one lands.
        menu.Lines.Add(ItemMenuEntry.Separator);
        menu.Lines.Add(menu.Toggle(Glyphs.Task, TasksStr.Todo_ShowCompleted_Label.Text(), config.ShowCompleted, on => config.ShowCompleted = on));
        menu.Lines.Add(WidgetMenuLines.Submenu(Glyphs.Search, TasksStr.WidgetsC_Tasks_Settings_Filter.Text(), Filters(menu, config, Importance(config, service))));

        // [S-610] Where a new task lands; the stored order ([S-598]) is kept either way.
        menu.Lines.Add(menu.Toggle(Glyphs.Sort, TasksStr.WidgetsC_Tasks_Settings_NewOnTop_Label.Text(), config.NewOnTop, on => config.NewOnTop = on));
    }

    /// <summary>[S-7516] Whether the account the widget reads has importance (before the service has started, assumed so).</summary>
    private static bool Importance(TasksConfig config, ITaskSource? service)
    {
        if (service is null || config.Source == TaskSource.Local)
        {
            return true;
        }

        var account = string.IsNullOrEmpty(config.AccountId) ? service.Accounts.FirstOrDefault() : service.Find(config.AccountId);
        return TasksConfig.HasImportance(account?.Provider);
    }

    /// <summary>
    /// [S-7516] Show tasks from: the first account with tasks (now or later), each account (a chosen one
    /// since signed out stays listed by its id, never swapped, [S-590]), or this PC; then the line that
    /// opens ⚙ › Accounts ([S-3745]). Another account starts from its default list and order.
    /// </summary>
    private static ItemMenuEntry Source(WidgetMenuLines menu, TasksConfig config, ITaskSource service)
    {
        var text = TasksStr.WidgetsC_Tasks_Settings_ShowFrom_Label.Text();
        var accounts = service.Accounts;
        var onAccount = config.Source == TaskSource.Account;

        ItemMenuEntry Account(string id, string words) => new(null, words, () =>
        {
            if (onAccount && config.AccountId == id)
            {
                return;
            }

            // A different account's lists, and a different list's order.
            config.Source = TaskSource.Account;
            config.AccountId = id;
            config.ListId = string.Empty;
            config.ListName = string.Empty;
            config.TaskOrder = [];
            menu.Save();
        }, Checked: onAccount && config.AccountId == id, Group: text);

        var lines = new List<ItemMenuEntry>
        {
            Account(string.Empty, accounts.Count > 0
                ? TasksStr.WidgetsC_Tasks_Settings_FirstAccountNamed.Format(("account", accounts[0].Label))
                : TasksStr.WidgetsC_Tasks_Settings_FirstAccount.Text()),
        };

        foreach (var account in accounts)
        {
            lines.Add(Account(account.Id, account.Problem is null ? account.Label : TasksStr.WidgetsC_OneDrive_Settings_AccountSignInAgain.Format(("account", account.Label)))
                with { Value = account.Problem is null ? ProviderCatalog.Of(account.Provider).Label.Text() : null });
        }

        if (config.AccountId.Length > 0 && accounts.All(a => a.Id != config.AccountId))
        {
            lines.Add(Account(config.AccountId, config.AccountId));
        }

        lines.Add(new(null, TasksStr.WidgetsC_Tasks_Settings_ThisPc.Text(), () =>
        {
            if (config.Source == TaskSource.Local)
            {
                return;
            }

            // [S-604] The list it had if that still exists, else the first, else a new one.
            config.Source = TaskSource.Local;
            config.LocalListId = service.Local.Ensure(config.LocalListId, TasksStr.Todo_LocalLists_DefaultName.Text());
            menu.Save();
        }, Checked: !onAccount, Group: text));

        lines.Add(ItemMenuEntry.Separator);
        if (accounts.Count == 0)
        {
            lines.Add(WidgetMenuLines.Note(TasksStr.WidgetsC_Tasks_Settings_NoAccount.Text()));
        }

        lines.Add(menu.AccountsLine(Glyphs.Contact, (accounts.Count == 0 ? Str.WidgetsA_Office_AddAccount : Str.WidgetsA_Office_ManageAccounts).Text()));

        var shown = !onAccount ? TasksStr.WidgetsC_Tasks_Settings_ThisPc.Text()
            : config.AccountId.Length == 0 ? accounts.FirstOrDefault()?.Label
            : service.Find(config.AccountId)?.Label ?? config.AccountId;
        return WidgetMenuLines.Submenu(Glyphs.Contact, text, lines) with { Value = shown };
    }

    /// <summary>Which list: the account's, or this PC's. None while the account is gone.</summary>
    private static ItemMenuEntry? List(WidgetMenuLines menu, TasksConfig config, ITaskSource service)
    {
        if (config.Source == TaskSource.Local)
        {
            return LocalLists(menu, config, service.Local);
        }

        var account = string.IsNullOrEmpty(config.AccountId) ? service.Accounts.FirstOrDefault() : service.Find(config.AccountId);
        return account is null ? null : AccountLists(menu, config, service, account);
    }

    /// <summary>
    /// [S-592]/[S-596] The account's lists: its default first (what an unchosen widget shows), then
    /// Today (every list's tasks due today or overdue), then the lists themselves, read through the
    /// service's cache ([S-3746]). A list that has gone since it was picked falls back to the default
    /// line's dot rather than none on a setting that means something.
    /// </summary>
    private static ItemMenuEntry AccountLists(WidgetMenuLines menu, TasksConfig config, ITaskSource service, TaskAccount account)
    {
        var text = TasksStr.WidgetsC_Tasks_Settings_List_Label.Text();
        var loads = Listings.GetValue(service, _ => new MenuLoads<TaskListing>(TaskListing.Same));
        var listing = loads.Read(account.Id, menu, () => ListAsync(service, account.Id));

        if (listing.Value is not { Lists: { } lists })
        {
            var note = listing.Value?.Problem ?? (listing.Failed ? TasksStr.WidgetsC_Tasks_Settings_List_Failed.Text() : TasksStr.Todo_LoadingLists.Text());
            return WidgetMenuLines.Submenu(Glyphs.Task, text, [WidgetMenuLines.Note(note)]) with { Value = config.ListName.Length > 0 ? config.ListName : null };
        }

        var fallback = TaskRules.DefaultList(lists);
        var options = new List<(string Id, string Name, string Words)>
        {
            (string.Empty, string.Empty, fallback is null ? TasksStr.WidgetsC_Tasks_Settings_List_Default.Text() : TasksStr.WidgetsC_Tasks_Settings_List_DefaultNamed.Format(("name", fallback.Name))),
            (TaskRules.TodayListId, string.Empty, TasksStr.WidgetsC_Tasks_Settings_List_Today.Text()),
        };
        options.AddRange(lists.Select(l => (l.Id, l.Name, string.IsNullOrWhiteSpace(l.Name) ? TasksStr.WidgetsC_Tasks_Settings_List_Unnamed.Text() : l.Name)));

        var current = options.Any(o => o.Id == config.ListId) ? config.ListId : string.Empty;
        var lines = options.Select(o => new ItemMenuEntry(null, o.Words, () =>
        {
            if (o.Id == config.ListId)
            {
                return;
            }

            config.ListId = o.Id;
            config.ListName = o.Name;
            config.TaskOrder = [];
            menu.Save();
        }, Checked: o.Id == current, Group: text)).ToList();

        return WidgetMenuLines.Submenu(Glyphs.Task, text, lines) with { Value = options.First(o => o.Id == current).Words };
    }

    /// <summary>[S-592] An account's lists through the service's cache, or why they could not be read.</summary>
    private static async Task<TaskListing> ListAsync(ITaskSource service, string accountId)
    {
        try
        {
            return new TaskListing(await service.ListsAsync(accountId).ConfigureAwait(true), null);
        }
        catch (AccountException ex)
        {
            Log.Warn(Scope, $"could not read the lists of {accountId}", ex);
            return new TaskListing(null, (ex.Error == AccountError.SignInRequired ? TasksStr.WidgetsC_Tasks_Settings_List_SignInAgain : TasksStr.WidgetsC_Tasks_Settings_List_Failed).Text());
        }
    }

    /// <summary>
    /// [S-604] The lists kept on this PC: pick one (its task count at the end), add one, rename the one
    /// shown, delete it after asking. A pool every Tasks widget shares, so a rename retitles each
    /// widget showing it.
    /// </summary>
    private static ItemMenuEntry LocalLists(WidgetMenuLines menu, TasksConfig config, ILocalTaskLists pool)
    {
        var text = TasksStr.WidgetsC_Tasks_Settings_List_Label.Text();
        var lines = new List<ItemMenuEntry>();
        if (pool.Problem is { } problem)
        {
            lines.Add(WidgetMenuLines.Note(problem));
        }

        foreach (var list in pool.Lists)
        {
            var listId = list.Id;
            lines.Add(new(null, Display(list.Name), () =>
            {
                if (config.LocalListId != listId)
                {
                    config.LocalListId = listId;
                    menu.Save();
                }
            }, Checked: list.Id == config.LocalListId, Group: text, Value: TasksStr.WidgetsC_Tasks_Settings_TaskCount.Plural(list.Tasks.Count)));
        }

        if (pool.Lists.Count == 0)
        {
            lines.Add(WidgetMenuLines.Note(TasksStr.WidgetsC_Tasks_Settings_NoLocalLists.Text()));
        }

        lines.Add(ItemMenuEntry.Separator);
        lines.Add(menu.Command(Glyphs.Add, TasksStr.WidgetsC_Tasks_Settings_AddList.Text(), () => config.LocalListId = pool.Create(LocalTaskLists.NewListName), disabled: !pool.Writable));

        var shown = pool.Find(config.LocalListId);
        if (shown is not null)
        {
            var shownId = shown.Id;
            lines.Add(new(Glyphs.Rename, TasksStr.WidgetsC_Tasks_RenameListEllipsis.Text(), () => menu.Ask(new WidgetTextAsk(
                TasksStr.WidgetsC_Tasks_RenameListEllipsis.Text().TrimEnd('…'),
                TasksStr.Todo_LocalLists_NamePlaceholder.Text(),
                Str.Common_Rename.Text(),
                _ => null,
                typed =>
                {
                    // The pool saves itself; the lines are built again to say the new name.
                    pool.Rename(shownId, typed.Trim());
                    menu.Outdate();
                },
                Initial: shown.Name,
                AllowEmpty: true)), Disabled: !pool.Writable));

            // [S-604] Always asked, empty or not: it takes every task in it.
            var detail = shown.Tasks.Count == 0
                ? TasksStr.WidgetsC_Tasks_Settings_DeleteListEmpty.Format(("name", Display(shown.Name)))
                : TasksStr.WidgetsC_Tasks_Settings_DeleteListWithTasks.Plural(shown.Tasks.Count, ("name", Display(shown.Name)));
            lines.Add(menu.Confirm(Glyphs.Delete, TasksStr.WidgetsC_Tasks_Settings_DeleteList.Text() + "…", TasksStr.WidgetsC_Tasks_Settings_DeleteListConfirm.Text(), Str.Common_Delete.Text(), () =>
            {
                pool.Delete(shownId);

                // The widget says "pick or start a list" rather than being handed another list's tasks.
                if (config.LocalListId == shownId)
                {
                    config.LocalListId = string.Empty;
                }
            }, disabled: !pool.Writable, detail: detail));
        }

        return WidgetMenuLines.Submenu(Glyphs.Task, text, lines) with { Value = shown is null ? null : Display(shown.Name) };
    }

    /// <summary>
    /// [S-611] Filters: the due and importance filters (an account's tasks only, [S-6074]; importance
    /// only where the account has it, [S-7516]), and the words the titles must hold, asked for as
    /// text (at most <see cref="TasksConfig.TextFilterMax"/> characters; an empty box clears it).
    /// </summary>
    private static List<ItemMenuEntry> Filters(WidgetMenuLines menu, TasksConfig config, bool importance)
    {
        var lines = new List<ItemMenuEntry>();
        if (config.Offers(TasksSetting.DueFilter))
        {
            lines.Add(menu.Choice(null, TasksStr.WidgetsC_Tasks_Settings_DueFilter_Label.Text(),
                [
                    (TaskDueFilter.Any, TasksStr.WidgetsC_Tasks_Settings_DueFilter_Any.Text()),
                    (TaskDueFilter.Overdue, Str.WidgetsC_Tasks_Overdue.Text()),
                    (TaskDueFilter.Today, Str.Common_Today.Text()),
                    (TaskDueFilter.ThisWeek, TasksStr.WidgetsC_Tasks_Settings_DueFilter_ThisWeek.Text()),
                    (TaskDueFilter.NoDate, TasksStr.WidgetsC_Tasks_Settings_DueFilter_NoDate.Text()),
                ],
                config.DueFilter, value => config.DueFilter = value));
        }

        if (config.Offers(TasksSetting.ImportantOnly) && importance)
        {
            lines.Add(menu.Toggle(null, TasksStr.WidgetsC_Tasks_Settings_ImportantOnly_Label.Text(), config.ImportantOnly, on => config.ImportantOnly = on));
        }

        var label = TasksStr.WidgetsC_Tasks_Settings_TextFilter_Label.Text();
        lines.Add(new(null, label + "…", () => menu.Ask(new WidgetTextAsk(
            label,
            TasksStr.WidgetsC_Tasks_Settings_TextFilter_Placeholder.Text(),
            Str.Common_Save.Text(),
            TextRefusal,
            typed =>
            {
                var words = typed.Trim();
                if (words != config.TextFilter)
                {
                    config.TextFilter = words;
                    menu.Save();
                }
            },
            Initial: config.TextFilter,
            AllowEmpty: true)), Value: config.TextFilter.Length > 0 ? config.TextFilter : null));
        return lines;
    }

    /// <summary>[S-611] Why typed words cannot be the filter (more than the filter holds), or null.</summary>
    internal static string? TextRefusal(string typed)
        => typed.Trim().Length > TasksConfig.TextFilterMax
            ? TasksStr.WidgetsC_Tasks_Menu_TextFilterLong.Format(("max", LocaleFormat.Integer(TasksConfig.TextFilterMax)))
            : null;

    private static string Display(string name) => string.IsNullOrWhiteSpace(name) ? TasksStr.WidgetsC_Tasks_UntitledList.Text() : name;
}
