#if DEBUG
using Themia.App.Shell.Gallery;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>Debug renders only: the states a render cannot click its way into ([S-1917]).</summary>
public sealed partial class PowerButtonView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["hover", "pressed", "armed", "failed", "unavailable"];

    public async Task ShowPreviewStateAsync(string state)
    {
        var now = DateTimeOffset.UtcNow;

        switch (state)
        {
            case "hover":
                _hover = true;
                break;

            case "pressed":
                _pressed = true;
                break;

            case "armed":
                // The inert backend: arming runs nothing, and an action that never arms does not show it.
                if (_button is { } button && PowerActionRules.NeedsArming(_action))
                {
                    await button.PressAsync(now).ConfigureAwait(true);
                    _ringTimer?.Start();
                }

                break;

            case "failed":
                _button = new PowerButton(_action, new PreviewBackend(canHibernate: true, PowerActionRules.SuspendOutcome(false, hibernate: _action == PowerAction.Hibernate)));
                if (PowerActionRules.NeedsArming(_action))
                {
                    await _button.PressAsync(now).ConfigureAwait(true);
                }

                await _button.PressAsync(now + TimeSpan.FromSeconds(1)).ConfigureAwait(true);
                break;

            case "unavailable":
                _button = new PowerButton(_action, new PreviewBackend(canHibernate: false, PowerOutcome.Ok));
                break;

            default:
                return;
        }

        Lay();
    }

    /// <summary>A backend that answers what the preview asks for and touches nothing.</summary>
    private sealed class PreviewBackend(bool canHibernate, PowerOutcome outcome) : IPowerBackend
    {
        public bool CanHibernate => canHibernate;

        public PowerOutcome Run(PowerAction action) => outcome;
    }
}
#endif
