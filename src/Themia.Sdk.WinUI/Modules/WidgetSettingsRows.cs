using Microsoft.UI.Xaml;

namespace Themia.Sdk.WinUI;

/// <summary>
/// [S-6904] What a module's settings page is built into: the rows (from <c>FluentLayout</c> only),
/// the save to call after a change, and the one place a page may send the user, the Accounts page.
/// The host lays the rows out (the danger section last, empty headings dropped).
/// </summary>
public sealed class WidgetSettingsRows(List<FrameworkElement> rows, Action save, Action showAccounts)
{
    public List<FrameworkElement> Rows { get; } = rows ?? throw new ArgumentNullException(nameof(rows));

    public Action Save { get; } = save ?? throw new ArgumentNullException(nameof(save));

    /// <summary>Opens Settings › Accounts (a page that needs a signed-in account offers it).</summary>
    public Action ShowAccounts { get; } = showAccounts ?? throw new ArgumentNullException(nameof(showAccounts));
}
