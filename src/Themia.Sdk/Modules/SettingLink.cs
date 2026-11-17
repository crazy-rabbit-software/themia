using Themia.Core.Localization;

namespace Themia.Sdk;

/// <summary>
/// [S-3749] Somewhere a widget's menu sends the user to do something the widget cannot (add or manage
/// an account): the only link rows a menu has. A setting a widget merely follows (the time format, the
/// start of the week, the temperature unit, an update interval) gets no link row; the widget just
/// follows it ([S-3590]). A closed set.
/// </summary>
public enum OwnedSetting
{
    /// <summary>[S-3550] ⚙ › Accounts: the accounts an account widget reads.</summary>
    Accounts,
}

/// <summary>[S-3749] Where a link row of a widget's menu leads.</summary>
public sealed record SettingLink(OwnedSetting Setting)
{
    /// <summary>[S-3749] The owner's name, for the row's "Opens … in Settings".</summary>
    public string Owner => Str.Shell_Settings_Nav_Accounts.Text();
}
