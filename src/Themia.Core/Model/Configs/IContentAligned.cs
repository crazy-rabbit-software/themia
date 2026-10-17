namespace Themia.Core.Model;

/// <summary>
/// [S-6581] A widget whose content is one block (a readout, a sentence, a face) that may sit Left,
/// Center or Right of its card. Implemented by its config, so the rule is the widget's own and
/// <see cref="Themia.Core.Widgets.ContentAlignment"/> names no kind.
/// </summary>
public interface IContentAligned
{
    /// <summary>Whether its current style is such a block: an exchange rate's chart, say, fills the card instead.</summary>
    bool AlignApplies => true;

    /// <summary>[S-6582] Where the block sits when nothing was chosen.</summary>
    ContentAlign DefaultAlign => ContentAlign.Left;
}
