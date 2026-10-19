using Themia.Core.Accounts;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>
/// [S-1860] [S-6906] When a widget has nothing to show until it is set up: what <c>WidgetSetup</c>
/// and <c>WidgetLiveness</c> ask. Implemented by its config, so the rule is the widget's own and
/// the host names no kind; a config without it never waits for setup.
/// </summary>
public interface ISetupRule
{
    /// <summary>Whether it waits for setup, given the features the signed-in accounts offer.</summary>
    bool NeedsSetup(FeatureSet signedIn);

    /// <summary>
    /// [S-1881] Whether it runs while it waits, because its setup happens in the widget itself
    /// (the shortcuts' drop target) rather than on its properties menu.
    /// </summary>
    bool SetsUpInPlace => false;
}
