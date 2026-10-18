using Themia.Core.Localization;
using Themia.Core.Tasks;
using Themia.Sdk.Kit;

namespace Themia.Widget.Tasks;

/// <summary>
/// [S-608] Tasks on the kit: a task's actions as the kit's one menu builder and key map read them
/// ([S-7011], [S-7020]). Pure, so the menu's lines are tested here and not in a view.
/// </summary>
public static class TaskSpec
{
    /// <summary>The action ids the view's handler carries out.</summary>
    public const string ToggleId = "toggle";

    public const string RenameId = "rename";
    public const string DeleteId = "delete";

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
