#if DEBUG
using Microsoft.UI.Xaml.Automation;
using Themia.App.Shell.Gallery;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>Debug renders only: the states a render cannot reach on this PC ([S-1917], [S-3909]).</summary>
public sealed partial class QuickToggleView : IWidgetPreviewState
{
    /// <summary>
    /// The states, and [S-16702] one per target (its key, <c>bluetooth</c>, <c>doNotDisturb</c>: that setting on), so the
    /// checks draw every target's glyph and name; any target in another state is <c>--config {"target":"…"}</c>.
    /// </summary>
    public IReadOnlyList<string> PreviewStates { get; } =
        ["on", "off", "unavailable", "unknown", "hover", "pressed", "refused", .. QuickToggleRules.All.Skip(1).Select(QuickToggleRules.Key)];

    public Task ShowPreviewStateAsync(string state)
    {
        if (QuickToggleRules.OfKey(state) is { } target)
        {
            _toggle = target;
            AutomationProperties.SetName(this, QuickToggleRules.Name(_toggle));
            return ShowSampleAsync();
        }

        switch (state)
        {
            case "on":
                return ShowSampleAsync();

            case "off":
                _preview = ToggleReading.Of(false);
                break;

            case "unavailable":
                _preview = ToggleReading.Blocked(QuickToggleRules.Target(_toggle).Unavailable);
                break;

            case "unknown":
                _preview = ToggleReading.Pending;
                break;

            case "hover":
                _hover = true;
                break;

            case "pressed":
                _pressed = true;
                break;

            case "refused":
                _preview = ToggleReading.Of(false);
                if (_switch.Press(_preview, DateTimeOffset.UtcNow) is not null)
                {
                    _switch.Settled(succeeded: false);
                }

                break;

            default:
                return Task.CompletedTask;
        }

        Lay();
        return Task.CompletedTask;
    }
}
#endif
