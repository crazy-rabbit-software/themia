using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Accounts;
using Themia.Core.Localization;
using Themia.Core.Sync;
using Themia.Core.Tasks;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Widget.Tasks;

namespace Themia.Core.Model;

/// <summary>[S-6074] The Tasks rows that only an account's tasks use: a list on this PC has no due dates or importance.</summary>
public enum TasksSetting
{
    /// <summary>Show due dates.</summary>
    DueDate,

    /// <summary>Filter by due date.</summary>
    DueFilter,

    /// <summary>Important only.</summary>
    ImportantOnly,
}

/// <summary>
/// [S-590] A task list: from a Microsoft or Google account through Themia's own sign-in, or a list
/// kept on this PC.
/// <para>
/// Empty account and list ids are not "nothing chosen": they mean the first account that has tasks
/// and its default list, so a widget dropped on the desktop of someone already signed in shows their
/// tasks without a trip to settings. A named account that has since been signed out is <i>not</i>
/// quietly swapped for another — that widget says so instead.
/// </para>
/// </summary>
public sealed class TasksConfig : WidgetConfig, IStyled, IShownSettings<TasksSetting>, ISetupRule, IInterests
{
    /// <summary>[S-2608] What the background sync keeps fresh for it.</summary>
    public void AddInterests(InterestsBuilder interests)
    {
        if (Source == TaskSource.Account)
        {
            interests.Of<TaskInterest>().Add(new TaskInterest(AccountId ?? string.Empty, ListId ?? string.Empty, ShowCompleted, interests.AccountInterval));
        }
    }

    /// <summary>A list kept on this PC needs no account; it starts from the widget itself ([S-604]).</summary>
    public bool NeedsSetup(FeatureSet signedIn) => Source == TaskSource.Account && !signedIn.Has(HostFeatures.Tasks);

    private List<string> _taskOrder = [];

    public TaskSource Source { get; set; } = TaskSource.Account;

    /// <summary>The account, or empty for the first account with tasks.</summary>
    public string AccountId { get; set; } = string.Empty;

    /// <summary>[S-592] The list on that account, <see cref="TaskRules.TodayListId"/>, or empty for its default list.</summary>
    public string ListId { get; set; } = string.Empty;

    /// <summary>The list's name when it was picked: the header's title while the lists load.</summary>
    public string ListName { get; set; } = string.Empty;

    /// <summary>[S-603] Which list on this PC, when <see cref="Source"/> is <see cref="TaskSource.Local"/>.</summary>
    public string LocalListId { get; set; } = string.Empty;

    /// <summary>[S-595] List finished tasks alongside the open ones.</summary>
    public bool ShowCompleted { get; set; }

    /// <summary>Show each task's due date.</summary>
    public bool ShowDueDate { get; set; } = true;

    /// <summary>[S-602] [S-6400] Due date under the title, or beside it: the widget's style.</summary>
    public TasksStyle Style { get; set; } = TasksStyle.List;

    private static readonly StyleOption[] TasksStyles =
    [
        StyleOption.Of(TasksStyle.List, TasksStr.WidgetsC_Tasks_Settings_Layout_List.Local(), 0xE8FD),
        StyleOption.Of(TasksStyle.Compact, TasksStr.WidgetsC_Tasks_Settings_Layout_Compact.Local(), 0xEA37),
    ];

    IReadOnlyList<StyleOption> IStyled.Styles => TasksStyles;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (TasksStyle)value;
    }

    /// <summary>[S-601] Lines a title wraps to before it is cut. Clamped on read.</summary>
    public int TitleLines { get; set; } = TaskRules.TitleLinesDefault;

    /// <summary>[S-610] A task added here, or one this widget has not seen before, goes to the top rather than the bottom.</summary>
    public bool NewOnTop { get; set; }

    /// <summary>[S-611] Which due dates the list keeps.</summary>
    public TaskDueFilter DueFilter { get; set; } = TaskDueFilter.Any;

    /// <summary>[S-611] Only important tasks (Microsoft To Do's star).</summary>
    public bool ImportantOnly { get; set; }

    /// <summary>[S-611] Only tasks whose title holds these words, in any case; empty for every task.</summary>
    public string TextFilter { get; set; } = string.Empty;

    /// <summary>
    /// [S-598] The user's own row order for an account list, as task ids. Neither provider stores a
    /// hand-sorted order, so it lives here. A list on this PC keeps its order in the list itself.
    /// </summary>
    public List<string> TaskOrder
    {
        get => _taskOrder;

        // A hand-edited "taskOrder": null must not leave the widget holding a null list.
        set => _taskOrder = value ?? [];
    }

    [JsonIgnore]
    public int EffectiveTitleLines => TaskRules.ClampTitleLines(TitleLines);

    /// <summary>[S-6074] The due date and importance rows: an account's tasks only (<see cref="Filter"/>).</summary>
    [JsonIgnore]
    public IReadOnlyList<TasksSetting> ShownSettings => Source == TaskSource.Account
        ? [TasksSetting.DueDate, TasksSetting.DueFilter, TasksSetting.ImportantOnly]
        : [];

    /// <summary>The most a text filter holds: a few words, not a document.</summary>
    public const int TextFilterMax = 100;

    /// <summary>
    /// [S-595] [S-611] [S-612] What the list keeps. Under Today ([S-596]) "Show completed" means the
    /// tasks finished today, as To Do's own My Day does. A list on this PC has no due dates or
    /// importance, so only its text filter applies: a due filter left from an account cannot empty it.
    /// An account without importance (<paramref name="importance"/> false, <see cref="HasImportance"/>)
    /// drops "Important only" the same way, so a value kept from another account cannot empty it.
    /// </summary>
    public TaskFilter Filter(bool today, bool importance) => Source == TaskSource.Local
        ? new(ShowCompleted, Text: TextFilter)
        : new(ShowCompleted, DueFilter, ImportantOnly && importance, TextFilter, FinishedTodayOnly: today);

    /// <summary>
    /// [S-7516] Whether an account's tasks can be important: Microsoft To Do's star (Graph's
    /// importance). Google Tasks has no such field, so "Important only" there would only ever empty
    /// the list. An account that is gone (null) is given the benefit of the doubt.
    /// </summary>
    public static bool HasImportance(ProviderId? provider) => provider is not { } known || known == HostProviders.Microsoft;

    /// <summary>[S-591] [S-3780] How often an account list is asked: the shared accounts cadence (Settings › Widgets).</summary>
    [JsonIgnore]
    public TimeSpan RefreshInterval => Sync.UpdateCadence.Interval(Sync.HostCadences.Accounts);

    /// <summary>
    /// Fold a hand-edited or corrupt config back inside the invariants: nulls a hand edit can put
    /// where the model promises a value, an unknown enum, a broken row order.
    /// </summary>
    public override IReadOnlyList<string> Repair()
    {
        var repairs = new List<string>();

        // System.Text.Json writes a JSON null straight through a non-nullable string property.
        AccountId ??= string.Empty;
        ListId ??= string.Empty;
        ListName ??= string.Empty;
        LocalListId ??= string.Empty;
        TextFilter ??= string.Empty;

        if (TextFilter.Length > TextFilterMax)
        {
            TextFilter = TextFilter[..TextFilterMax];
            repairs.Add("a Tasks widget's text filter was cut to its length");
        }

        if (!Enum.IsDefined(DueFilter))
        {
            DueFilter = TaskDueFilter.Any;
            repairs.Add("a Tasks widget named an unknown due filter; it shows every due date again");
        }

        if (!Enum.IsDefined(Source))
        {
            Source = TaskSource.Account;
            repairs.Add("a Tasks widget named an unknown source; it reads an account again");
        }

        var order = TaskRules.CleanOrder(TaskOrder);
        if (order.Count != TaskOrder.Count)
        {
            TaskOrder = order;
            repairs.Add("a Tasks widget's row order held blank or repeated entries");
        }

        return repairs;
    }

    public override WidgetConfig Clone() => new TasksConfig
    {
        Source = Source,
        AccountId = AccountId,
        ListId = ListId,
        ListName = ListName,
        LocalListId = LocalListId,
        ShowCompleted = ShowCompleted,
        ShowDueDate = ShowDueDate,
        Style = Style,
        TitleLines = TitleLines,
        NewOnTop = NewOnTop,
        DueFilter = DueFilter,
        ImportantOnly = ImportantOnly,
        TextFilter = TextFilter,
        TaskOrder = [.. TaskOrder],
    };
}
