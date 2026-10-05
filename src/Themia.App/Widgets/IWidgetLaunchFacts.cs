using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// [S-1923] A view that knows, while it runs, part of what a click on its widget opens: the app
/// that is playing, the folder it is showing, the site of its feed. Read by the card on hover and on
/// click; <see cref="WidgetLaunch"/> checks every field again, so a view only reports what it has.
/// </summary>
public interface IWidgetLaunchFacts
{
    WidgetLiveLaunch LaunchFacts { get; }
}

/// <summary>
/// [S-1925] A custom element that takes its own clicks from raw pointer events (a seek bar), so a
/// click on it is never a click on the widget. Standard controls (buttons, sliders, boxes, list
/// items) are recognised by type and need no marker.
/// </summary>
public interface IOwnsClicks
{
}
