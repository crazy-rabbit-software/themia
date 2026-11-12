#if DEBUG
namespace Themia.App.Widgets;

/// <summary>The linked states a render can ask for.</summary>
public static class ScreenTimePreviewStates
{
    public static void Apply(ScreenTimeLinks links, string state)
    {
        switch (state)
        {
            case "hovered":
                links.Hover(SampleScreenTime.HoveredApp);
                break;
            case "selected":
                links.Select(SampleScreenTime.SelectedApp);
                break;
            default:
                break;
        }
    }
}
#endif
