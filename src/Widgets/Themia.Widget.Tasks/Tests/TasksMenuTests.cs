using Themia.App.Services.Tasks;
using Themia.App.Widgets;
using Themia.Core.Accounts;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Tasks;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.Tasks.Tests;

/// <summary>[S-7516] The Tasks widget's options on its properties menu ([S-3719]): where the list comes from, which list, and what the rows keep.</summary>
public sealed class TasksMenuTests
{
    private sealed class Service(IReadOnlyList<TaskAccount> accounts, ILocalTaskLists local, Func<string, Task<IReadOnlyList<TaskListInfo>>> lists) : ITaskSource
    {
        public event Action? AccountsChanged;

        public IReadOnlyList<TaskAccount> Accounts => accounts;

        public ILocalTaskLists Local => local;

        public TaskAccount? Find(string? accountId) => accounts.FirstOrDefault(a => a.Id == accountId);

        public Task<IReadOnlyList<TaskListInfo>> ListsAsync(string accountId, bool fresh = false) => lists(accountId);

        public ITaskFeed Acquire(string accountId, string listId, bool showCompleted) => throw new NotSupportedException();

        public void Release(ITaskFeed feed) => AccountsChanged?.Invoke();

        public void Refresh(ITaskFeed feed, TimeSpan interval, bool force) => throw new NotSupportedException();

        public Task SetCompletedAsync(ITaskFeed feed, TaskItem task, bool completed) => throw new NotSupportedException();

        public Task RenameAsync(ITaskFeed feed, TaskItem task, string title) => throw new NotSupportedException();

        public Task RenameListAsync(string accountId, TaskListInfo list, string name) => throw new NotSupportedException();

        public Task DeleteAsync(ITaskFeed feed, TaskItem task) => throw new NotSupportedException();

        public Task<TaskItem?> CreateAsync(ITaskFeed feed, string title) => throw new NotSupportedException();
    }

    private int _saves;

    private WidgetTextAsk? _asked;

    private WidgetConfirm? _confirm;

    private static readonly IReadOnlyList<TaskAccount> Work = [new("a", HostProviders.Microsoft, "Work", null)];

    private static readonly IReadOnlyList<TaskListInfo> WorkLists = [new("l1", "Tasks", true), new("l2", "Shopping", false)];

    private static Service Accounts(Func<string, Task<IReadOnlyList<TaskListInfo>>>? lists = null)
        => new(Work, new MemoryTaskLists([]), lists ?? (_ => Task.FromResult(WorkLists)));

    private WidgetMenuLines Build(TasksConfig config, ITaskSource? service)
    {
        var lines = new WidgetMenuLines(() => _saves++, ask => _asked = ask, confirm => _confirm = confirm);
        TasksMenu.Lines(lines, config, service);
        return lines;
    }

    private static ItemMenuEntry Line(IEnumerable<ItemMenuEntry> lines, string text) => Assert.Single(lines, l => l.Text == text);

    private static string From => TasksStr.WidgetsC_Tasks_Settings_ShowFrom_Label.Text();

    private static string ListLabel => TasksStr.WidgetsC_Tasks_Settings_List_Label.Text();

    [Fact]
    public void S7516_an_account_list_offers_the_source_the_list_the_row_lines_and_the_filters()
    {
        var lines = Build(new TasksConfig(), Accounts());

        Assert.Equal(0, lines.CardAt);
        Assert.Equal(
            [
                From, ListLabel, TasksStr.Todo_ShowCompleted_Label.Text(), TasksStr.WidgetsC_Tasks_Settings_ShowDue_Label.Text(),
                TasksStr.Todo_TitleLines_Label.Text(), TasksStr.WidgetsC_Tasks_Settings_NewOnTop_Label.Text(), TasksStr.WidgetsC_Tasks_Settings_Filter.Text(),
            ],
            lines.Lines.Where(l => !l.IsSeparator).Select(l => l.Text));
    }

    [Fact]
    public void S7516_a_list_on_this_pc_has_no_due_dates_or_importance()
    {
        var lines = Build(new TasksConfig { Source = TaskSource.Local }, Accounts());

        Assert.DoesNotContain(lines.Lines, l => l.Text == TasksStr.WidgetsC_Tasks_Settings_ShowDue_Label.Text());
        Assert.Equal(
            [TasksStr.WidgetsC_Tasks_Settings_TextFilter_Label.Text() + "…"],
            Line(lines.Lines, TasksStr.WidgetsC_Tasks_Settings_Filter.Text()).Children!.Select(c => c.Text));
    }

    [Fact]
    public void S7516_before_the_service_has_started_it_says_so()
    {
        var lines = Build(new TasksConfig(), service: null);

        Assert.True(lines.Lines[0].Note);
        Assert.Equal(TasksStr.WidgetsC_Tasks_Settings_NotStarted.Text(), lines.Lines[0].Text);
    }

    [Fact]
    public void S7516_the_source_is_the_first_account_each_account_and_this_pc_then_manage_accounts()
    {
        var source = Line(Build(new TasksConfig(), Accounts()).Lines, From).Children!.Where(l => !l.IsSeparator).ToList();

        Assert.Equal(
            [TasksStr.WidgetsC_Tasks_Settings_FirstAccountNamed.Format(("account", "Work")), "Work", TasksStr.WidgetsC_Tasks_Settings_ThisPc.Text(), Str.WidgetsA_Office_ManageAccounts.Text()],
            source.Select(l => l.Text));
        Assert.True(source[0].Checked);
    }

    [Fact]
    public void S7516_another_account_starts_from_its_default_list_and_order()
    {
        var config = new TasksConfig { ListId = "l2", ListName = "Shopping", TaskOrder = ["t1"] };
        Line(Line(Build(config, Accounts()).Lines, From).Children!, "Work").Action!();

        Assert.Equal("a", config.AccountId);
        Assert.Empty(config.ListId);
        Assert.Empty(config.TaskOrder);
        Assert.Equal(1, _saves);
    }

    [Fact]
    public void S7516_a_signed_out_account_stays_listed_by_its_id()
    {
        var source = Line(Build(new TasksConfig { AccountId = "gone" }, Accounts()).Lines, From);

        Assert.True(Line(source.Children!, "gone").Checked);
        Assert.Equal("gone", source.Value);
    }

    [Fact]
    public void S7516_this_pc_takes_a_list_kept_here_making_one_when_there_is_none()
    {
        var service = Accounts();
        var config = new TasksConfig();
        Line(Line(Build(config, service).Lines, From).Children!, TasksStr.WidgetsC_Tasks_Settings_ThisPc.Text()).Action!();

        Assert.Equal(TaskSource.Local, config.Source);
        Assert.Equal(service.Local.Lists.Single().Id, config.LocalListId);
    }

    [Fact]
    public void S7516_the_account_lists_are_the_default_today_and_each_list()
    {
        var list = Line(Build(new TasksConfig(), Accounts()).Lines, ListLabel);

        Assert.Equal(
            [
                TasksStr.WidgetsC_Tasks_Settings_List_DefaultNamed.Format(("name", "Tasks")),
                TasksStr.WidgetsC_Tasks_Settings_List_Today.Text(),
                "Tasks",
                "Shopping",
            ],
            list.Children!.Select(c => c.Text));
        Assert.True(list.Children![0].Checked);
    }

    [Fact]
    public void S7516_picking_a_list_keeps_its_name_and_starts_its_order_afresh()
    {
        var config = new TasksConfig { TaskOrder = ["t1"] };
        Line(Line(Build(config, Accounts()).Lines, ListLabel).Children!, "Shopping").Action!();

        Assert.Equal("l2", config.ListId);
        Assert.Equal("Shopping", config.ListName);
        Assert.Empty(config.TaskOrder);
    }

    [Fact]
    public void S7516_lists_that_could_not_be_read_say_why()
    {
        var service = Accounts(_ => Task.FromException<IReadOnlyList<TaskListInfo>>(new AccountException(AccountError.SignInRequired, "expired")));
        var note = Assert.Single(Line(Build(new TasksConfig(), service).Lines, ListLabel).Children!);

        Assert.True(note.Note);
        Assert.Equal(TasksStr.WidgetsC_Tasks_Settings_List_SignInAgain.Text(), note.Text);
    }

    [Fact]
    public void S7516_lists_on_this_pc_are_picked_added_renamed_and_deleted_after_asking()
    {
        var pool = new MemoryTaskLists([new("p1", "Home", []), new("p2", "Errands", [])]);
        var service = new Service(Work, pool, _ => Task.FromResult(WorkLists));
        var config = new TasksConfig { Source = TaskSource.Local, LocalListId = "p1" };

        var lists = Line(Build(config, service).Lines, ListLabel).Children!;
        Line(lists, "Errands").Action!();
        Assert.Equal("p2", config.LocalListId);

        Line(Line(Build(config, service).Lines, ListLabel).Children!, TasksStr.WidgetsC_Tasks_RenameListEllipsis.Text()).Action!();
        _asked!.Done(" Shop ");
        Assert.Equal("Shop", pool.Find("p2")!.Name);

        Line(Line(Build(config, service).Lines, ListLabel).Children!, TasksStr.WidgetsC_Tasks_Settings_DeleteList.Text() + "…").Action!();
        Assert.NotNull(pool.Find("p2"));
        _confirm!.Done();
        Assert.Null(pool.Find("p2"));
        Assert.Empty(config.LocalListId);

        Line(Line(Build(config, service).Lines, ListLabel).Children!, TasksStr.WidgetsC_Tasks_Settings_AddList.Text()).Action!();
        Assert.Equal(2, pool.Lists.Count);
        Assert.Equal(pool.Lists[^1].Id, config.LocalListId);
    }

    [Fact]
    public void S7516_the_words_filter_refuses_more_than_it_holds_and_an_empty_box_clears_it()
    {
        var config = new TasksConfig { TextFilter = "milk" };
        var filters = Line(Build(config, Accounts()).Lines, TasksStr.WidgetsC_Tasks_Settings_Filter.Text()).Children!;
        Line(filters, TasksStr.WidgetsC_Tasks_Settings_TextFilter_Label.Text() + "…").Action!();

        Assert.NotNull(_asked!.Refuse(new string('x', TasksConfig.TextFilterMax + 1)));
        Assert.Null(_asked.Refuse("bread"));
        Assert.True(_asked.AllowEmpty);

        _asked.Done(string.Empty);
        Assert.Empty(config.TextFilter);
        Assert.Equal(1, _saves);
    }

    [Fact]
    public void S7516_the_title_lines_are_one_to_six_dots()
    {
        var config = new TasksConfig();
        var titles = Line(Build(config, Accounts()).Lines, TasksStr.Todo_TitleLines_Label.Text());

        Assert.Equal(TaskRules.TitleLinesMax - TaskRules.TitleLinesMin + 1, titles.Children!.Count);
        Line(titles.Children!, LocaleFormat.Integer(4)).Action!();
        Assert.Equal(4, config.TitleLines);
    }
}
