#if DEBUG
using Themia.App.Shell.Gallery;

namespace Themia.App.Widgets;

/// <summary>Debug renders only ([S-1917], [S-2053]): <c>sample</c> is the contacts' sample (<see cref="ShowSampleAsync"/>), so a render can be matched with its mockup.</summary>
internal sealed partial class ContactsView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample"];

    public Task ShowPreviewStateAsync(string state)
        => state == "sample" ? ShowSampleAsync() : Task.CompletedTask;
}
#endif
