using Themia.App.Shell.Gallery;

namespace Themia.Widget.Date;

/// <summary>
/// Debug renders only ([S-10804]): <c>sample</c> is the sample (<see cref="ShowSampleAsync"/>); <c>longest</c> a day
/// whose words run long in most languages (a Wednesday in late September), to see the face hold its widest.
/// </summary>
internal sealed partial class DateView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "longest"];

    public Task ShowPreviewStateAsync(string state)
    {
        if (state == "longest")
        {
            Freeze(new DateOnly(2026, 9, 30));
            return Task.CompletedTask;
        }

        return state == "sample" ? ShowSampleAsync() : Task.CompletedTask;
    }
}
