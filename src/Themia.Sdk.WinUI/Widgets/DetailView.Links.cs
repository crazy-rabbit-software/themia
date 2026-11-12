using Themia.Core.Links;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI.Kit;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11930] A detail list's place on its view's links, where its part names the topics
/// (<see cref="Details.DetailPart.AppLinks"/>): a row is its app (<see cref="DetailRow.App"/>). The pointer
/// or the keyboard cursor on a row publishes the app ([S-3307]); a row lights for the app pointed at
/// (the item layer's hover fill) or picked (the selected fill and the accent pill) anywhere on the view,
/// in place: the rows are lit again as they are filled, never built for it. A click keeps its own
/// meaning (the ring); the list never picks.
/// </summary>
public sealed partial class DetailView : IWidgetSurfaceAware
{
    private readonly WidgetLinks _links = new();
    private LinkLights<AppLink>? _apps;
    private IWidgetSurface? _surface;

    public void Bind(IWidgetSurface surface) => _surface = surface;

    private void FollowLinks()
    {
        if (_part.AppLinks is { } topics)
        {
            _apps = new LinkLights<AppLink>(_links, topics.Hovered, topics.Picked);
        }
    }

    private void JoinLinks()
    {
        if (_apps is not null)
        {
            _links.Join(_surface?.Links);
        }
    }

    private void LeaveLinks()
    {
        _links.Leave();
        _apps?.Clear();
    }

    /// <summary>The rows as they are filled: each shown row is its app, a hidden one nothing.</summary>
    private void LinkRows(int count)
    {
        if (_apps is not { } apps)
        {
            return;
        }

        for (var i = 0; i < _rows.Count; i++)
        {
            if (i < count && i < _sheet.Rows.Count)
            {
                apps.Row(_rows[i].Block, AppOf(_sheet.Rows[i]));
            }
            else
            {
                apps.Remove(_rows[i].Block);
            }
        }

        apps.Apply();
    }

    /// <summary>[S-3307] The keyboard cursor publishes its row's app as the hover; none when it lets go.</summary>
    private void PointCursor(int index)
        => _apps?.Point(index >= 0 && index < ListCount && index < _sheet.Rows.Count ? AppOf(_sheet.Rows[index]) : null);

    private static AppLink? AppOf(DetailRow row) => row.App is { } key ? new AppLink(key) : null;
}
