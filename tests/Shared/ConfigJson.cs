using Themia.Core.Model;
using Themia.Core.Storage;

namespace Themia.Core.Tests;

/// <summary>
/// Read one widget config the way the app does: inside a state file, through the store's parser.
/// Shared by both test projects (a moved widget's tests read their config as they did in Core).
/// </summary>
internal static class ConfigJson
{
    public static WidgetConfig Read(string widgetKind, string configJson)
    {
        var json = $$"""
            {
              "schemaVersion": 1,
              "views": [ { "id": "v1", "name": "Desktop", "widgets": [
                { "id": "w", "kind": "{{widgetKind}}", "x": 0.1, "y": 0.1, "w": 0.2, "h": 0.2, "config": {{configJson}} }
              ] } ],
              "activeViewId": "v1",
              "monitorViews": {}
            }
            """;

        var state = AppStateStore.Parse(json).State ?? throw new InvalidOperationException("the state did not parse");
        return state.Views[0].Find("w")!.Config;
    }
}
