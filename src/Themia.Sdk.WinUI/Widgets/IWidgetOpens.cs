using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// [S-2000] [S-2001] A view whose widget is about something that opens: a plain click on the card
/// outside edit mode, one none of the view's own buttons, rows, links or controls took, opens
/// <see cref="Opens"/> at once through the host's launcher (<see cref="Themia.Sdk.WinUI.IShellLauncher.OpenCard"/>).
/// <para>
/// Read on the click, so it can follow what the view shows now (the playing app, the folder it is
/// in). Null opens nothing: the widget is not set up, its data is not there, or nothing fits. A view
/// with nothing to open at all does not implement this. The decision is the widget's own; there is
/// no table of kinds in the host.
/// </para>
/// </summary>
public interface IWidgetOpens
{
    CardOpen? Opens { get; }
}
