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
    /// (a shortcut's drop target) as well as on its page: the card then shows no Set up row over it
    /// and the view draws its own waiting look.
    /// </summary>
    bool SetsUpInPlace => false;

    /// <summary>
    /// [S-1862] The sentence the Set up status says beside its button ("Choose a place"), in the user's
    /// language; null for the card's own (an account to add, or what the widget needs set).
    /// </summary>
    string? SetupSentence => null;
}
