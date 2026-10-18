using Themia.Core.Localization;
using Themia.Core.Tasks;
using Themia.Sdk.Kit;

namespace Themia.Widget.Tasks;

/// <summary>
/// [S-616] Tasks on the kit: a task's actions as the kit's one menu builder and key map read them
/// ([S-7011], [S-7020]). Pure, so the menu's lines are tested here and not in a view.
/// </summary>
public static class TaskSpec
{
    /// <summary>The action ids the view's handler carries out.</summary>
    public const string ToggleId = "toggle";

    public const string RenameId = "rename";
    public const string DeleteId = "delete";

    /// <summary>The done group's key: the finished tasks, under their heading ([S-784]).</summary>
    public const string DoneGroup = "done";

    /// <summary>[S-616] The kit's check look ([S-7037]), its add row's words ([S-3850]).</summary>
    public static KitSpec Spec { get; } = new(KitRowLook.Check, KitShow.All, () => Str.WidgetsC_Tasks_UntitledTask.Text(), () => string.Empty, KitTime.None)
    {
        AddLabel = () => TasksStr.WidgetsC_Tasks_AddTask.Text(),
        AddPlaceholder = () => TasksStr.WidgetsC_Tasks_TaskName.Text(),
    };

    /// <summary>
    /// [S-616] [S-780] A task as a row: its check (the ring in the critical tone while it is late and
    /// due dates show), its title, important as the strong title with the "!" mark ([S-785]), the list
    /// it is from ([S-789]), its due day in its words ([S-186]: a finished task that was due says its
    /// date), the critical tone while late, the done group under its heading, and its tooltip.
    /// </summary>
    /// <param name="listName">The list it is from, for Today's rows; null otherwise.</param>
    /// <param name="doneCount">How many finished tasks the list shows, for the heading.</param>
    public static KitItem Item(TaskItem task, DateOnly today, bool showDueDate, string? listName, int doneCount)
    {
        ArgumentNullException.ThrowIfNull(task);

        var due = TaskDue.Resolve(task.DueRaw, today, TimeZoneInfo.Local);
        var late = due is { IsPast: true } && !task.Completed;

        KitDue? words = due is not { } resolved ? null
            : resolved.IsPast && task.Completed ? new KitDue(TaskDue.ShortDate(resolved.Day), TaskDue.ShortDate(resolved.Day), false)
            : new KitDue(TaskDue.Text(resolved), TaskDue.WideText(resolved), TaskDue.IsUrgent(resolved));

        return new KitItem(
            task.Id,
            task.DisplayTitle,
            listName,
            Lead: KitLead.Check(task.Completed, late && showDueDate ? KitTone.Critical : KitTone.Neutral),
            Tone: late ? KitTone.Critical : KitTone.Neutral,
            Emphasis: task.Important,
            Badges: task.Important ? [new KitBadge("!", KitTone.Critical)] : null,
            Group: task.Completed ? new KitGroup(DoneGroup, TasksStr.WidgetsC_Tasks_CompletedHeading.Format(("count", doneCount))) : null,
            Tooltip: Tooltip(task, due),
            Due: words);
    }

    private static string Tooltip(TaskItem task, Due? due)
    {
        var lines = new List<string> { task.DisplayTitle, (task.Completed ? TasksStr.WidgetsC_Tasks_StateCompleted : TasksStr.WidgetsC_Tasks_StateOpen).Text() };

        if (due is { } resolved)
        {
            lines.Add((resolved.IsPast && !task.Completed ? TasksStr.WidgetsC_Tasks_DueOnOverdue : TasksStr.WidgetsC_Tasks_DueOn).Format(("date", TaskDue.LongText(resolved))));
        }

        if (task.Important)
        {
            lines.Add(TasksStr.WidgetsC_Tasks_HighPriority.Text());
        }

        lines.Add(TasksStr.WidgetsC_Tasks_RowHint.Text());
        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// [S-597] [S-600] A task's actions: tick it off (or open it again), rename it — a task opens for
    /// editing, so this is its double-click and Enter ([S-7011]) — and delete it. A task the provider
    /// has not confirmed yet cannot be renamed until it has.
    /// </summary>
    /// <param name="renamable">The task can be renamed now.</param>
    public static IReadOnlyList<KitAction> Actions(TaskItem task, bool renamable)
    {
        ArgumentNullException.ThrowIfNull(task);

        return
        [
            new(KitActionKind.Toggle, task.Completed ? TasksStr.WidgetsC_Tasks_MarkNotDone.Text() : Str.WidgetsC_Tasks_MarkDone.Text(), task.Completed ? KitIcon.Undo : KitIcon.Check, Id: ToggleId),
            new(KitActionKind.Rename, Str.Common_Rename.Text(), Disabled: !renamable, Id: RenameId),
            new(KitActionKind.Delete, Str.Common_Delete.Text(), Id: DeleteId),
        ];
    }
}
