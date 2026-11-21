using Themia.App.Shell;

namespace Themia.Widget.Icon;

/// <summary>Debug renders only ([S-1917], [S-15006]): <c>sample</c> the default star; <c>picked</c> another icon picked (the sun).</summary>
internal sealed partial class IconWidgetView
{
    protected override IReadOnlyList<string> OwnPreviewStates { get; } = ["sample", "picked"];

    protected override Task ShowOwnPreviewStateAsync(string state)
    {
        if (state == "picked")
        {
            Show(Glyphs.Sun);
            return Task.CompletedTask;
        }

        return ShowSampleAsync();
    }
}
