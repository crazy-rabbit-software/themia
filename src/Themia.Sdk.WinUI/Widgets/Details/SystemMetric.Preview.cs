#if DEBUG
namespace Themia.App.Widgets.Details;

/// <summary>
/// Debug renders only ([S-1917], [S-14605]): <c>unavailable</c> draws the read of a thing not on this machine where the
/// kind can be absent ([S-1942]: a battery on a desktop, no graphics adapter); the figures themselves are the sample
/// machine's (<c>--machine sample</c>, [S-1931]).
/// </summary>
public sealed partial class SystemMetricView
{
    protected override IReadOnlyList<string> OwnPreviewStates => _part.Absent() is null ? [] : ["unavailable"];

    protected override Task ShowOwnPreviewStateAsync(string state)
    {
        if (state == "unavailable" && _part.Absent() is { } absent)
        {
            _part.Detach();
            Present(absent);
        }

        return Task.CompletedTask;
    }
}
#endif
